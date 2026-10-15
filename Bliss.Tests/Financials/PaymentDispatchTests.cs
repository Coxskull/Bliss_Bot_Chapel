using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class PaymentDispatchTests
{
    private static ClaimsPrincipal User(string subject) => new(new ClaimsIdentity([new Claim("sub", subject)], "fixture"));
    private static NpgsqlDataSource Data() => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!);
    private static async Task<(ClaimsPrincipal User, PreparedPaymentOperation Operation)> Prepare(NpgsqlDataSource data, FinancialModule module = FinancialModule.BlissBot)
    {
        var subject = "dispatch-" + Guid.NewGuid(); var user = User(subject);
        var quote = new AuthorizedQuoteSnapshot(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), module, Guid.NewGuid()), subject, Guid.NewGuid(), 14225, "USD");
        var store = new PaymentOperationStore(data); await store.FreezeQuote(quote);
        return (user, await store.Prepare(user, quote.Id, "fixture"));
    }
    private sealed class ProviderHandler(PreparedPaymentOperation operation) : HttpMessageHandler
    {
        public int Calls; public bool FailOnce; public List<string> Keys = []; public bool Succeeded;
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource? Release;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
            var body = await request.Content!.ReadAsStringAsync(ct); Assert.Contains("amount=14225", body); Assert.Contains("currency=usd", body);
            Entered.TrySetResult(); if (Release is not null) await Release.Task.WaitAsync(ct);
            if (FailOnce && Calls == 1) throw new HttpRequestException("synthetic uncertain provider outcome");
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
            {
                id = "pi_fixture_" + operation.Id.ToString("N"), @object = "payment_intent", livemode = false,
                status = Succeeded ? "succeeded" : "requires_payment_method", amount = operation.AmountMinor, currency = "usd",
                metadata = new { merchant_id = operation.Scope.MerchantId, campaign_id = operation.Scope.CampaignId,
                    module = operation.Scope.Module.ToString(), funding_reference = operation.QuoteId.ToString("N") }
            })) };
        }
    }
    private static StripePaymentDispatcher Dispatcher(NpgsqlDataSource data, ProviderHandler handler) =>
        new(new(data), new(new HttpClient(handler), "sk_test_fixture_not_a_credential"), (_, _, _) => Task.CompletedTask);

    [FinancialPostgresFact]
    public async Task Concurrent_dispatch_calls_provider_once_replays_bound_identity_and_never_books_cash()
    {
        await using var data = Data();
        foreach (var module in Enum.GetValues<FinancialModule>())
        {
            var prepared = await Prepare(data, module); var handler = new ProviderHandler(prepared.Operation) { Succeeded = true, Release = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            var dispatcher = Dispatcher(data, handler); var first = dispatcher.Dispatch(prepared.User, prepared.Operation.Id);
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var followers = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => dispatcher.Dispatch(prepared.User, prepared.Operation.Id)));
            Assert.All(followers, state => Assert.Equal("IN_FLIGHT", state)); handler.Release.SetResult(); Assert.Equal("BOUND", await first);
            Assert.Equal("BOUND", await dispatcher.Dispatch(prepared.User, prepared.Operation.Id)); Assert.Equal(1, handler.Calls);
            await using var journal = data.CreateCommand("SELECT count(*) FROM financials.journals WHERE merchant_id=@m");
            journal.Parameters.AddWithValue("m", prepared.Operation.Scope.MerchantId); Assert.Equal(0L, await journal.ExecuteScalarAsync());
        }
    }

    [FinancialPostgresFact]
    public async Task Unknown_response_survives_worker_recreation_and_reuses_exact_key()
    {
        await using var data = Data(); var prepared = await Prepare(data); var handler = new ProviderHandler(prepared.Operation) { FailOnce = true };
        Assert.Equal("UNKNOWN", await Dispatcher(data, handler).Dispatch(prepared.User, prepared.Operation.Id));
        Assert.Equal("BOUND", await Dispatcher(data, handler).Dispatch(prepared.User, prepared.Operation.Id));
        Assert.Equal(2, handler.Calls); Assert.Equal(handler.Keys[0], handler.Keys[1]);
        await using var results = data.CreateCommand("SELECT state FROM financials.payment_dispatch_results WHERE operation_id=@i ORDER BY recorded_at");
        results.Parameters.AddWithValue("i", prepared.Operation.Id); await using var reader = await results.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync()); Assert.Equal("UNKNOWN", reader.GetString(0)); Assert.True(await reader.ReadAsync()); Assert.Equal("BOUND", reader.GetString(0));
    }

    [FinancialPostgresFact]
    public async Task Cancellation_after_send_records_unknown_using_independent_cleanup_token()
    {
        await using var data = Data(); var prepared = await Prepare(data);
        var handler = new ProviderHandler(prepared.Operation) { Release = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var cancellation = new CancellationTokenSource();
        var request = Dispatcher(data, handler).Dispatch(prepared.User, prepared.Operation.Id, cancellation.Token);
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); cancellation.Cancel();
        Assert.Equal("UNKNOWN", await request);
        handler.Release = null;
        Assert.Equal("BOUND", await Dispatcher(data, handler).Dispatch(prepared.User, prepared.Operation.Id));
        Assert.Equal(handler.Keys[0], handler.Keys[1]);
    }

    [FinancialPostgresFact]
    public async Task Ownership_revocation_and_changed_result_never_authorize_payment()
    {
        await using var data = Data(); var prepared = await Prepare(data); var handler = new ProviderHandler(prepared.Operation); var store = new PaymentDispatchStore(data);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Claim(User("other"), prepared.Operation.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => store.Claim(new ClaimsPrincipal(), prepared.Operation.Id));
        var denied = new StripePaymentDispatcher(store, new(new HttpClient(handler), "sk_test_fixture_not_a_credential"), (_, _, _) => throw new UnauthorizedAccessException("Current approval revoked"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.Dispatch(prepared.User, prepared.Operation.Id)); Assert.Equal(0, handler.Calls);
        var claim = await store.Claim(prepared.User, prepared.Operation.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.Complete(claim, new("pi_bad", "succeeded", 1, "USD")));
        Assert.False(await store.Complete(claim with { Operation = claim.Operation with { RecipientAccountId = Guid.NewGuid() } }, new("pi_bad", "succeeded", 14225, "USD")));
        Assert.True(await store.Complete(claim, null)); Assert.False(await store.Complete(claim, null));
    }

    [FinancialPostgresFact]
    public async Task Expired_attempt_is_fenced_and_old_idempotency_window_requires_manual_review()
    {
        await using var data = Data(); var prepared = await Prepare(data); var expired = Guid.NewGuid();
        await using (var seed = data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '3 minutes',clock_timestamp()-interval '1 minute')"))
        { seed.Parameters.AddWithValue("a", expired); seed.Parameters.AddWithValue("i", prepared.Operation.Id); await seed.ExecuteNonQueryAsync(); }
        var store = new PaymentDispatchStore(data); var claim = await store.Claim(prepared.User, prepared.Operation.Id); Assert.Equal("CLAIMED", claim.State);
        Assert.False(await store.Complete(claim with { AttemptId = expired }, new("pi_stale", "succeeded", 14225, "USD")));
        Assert.True(await store.Complete(claim, null));
        var aged = await Prepare(data);
        await using (var seed = data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '21 hours',clock_timestamp()-interval '20 hours')"))
        { seed.Parameters.AddWithValue("a", Guid.NewGuid()); seed.Parameters.AddWithValue("i", aged.Operation.Id); await seed.ExecuteNonQueryAsync(); }
        var handler = new ProviderHandler(aged.Operation); Assert.Equal("MANUAL_REVIEW", await Dispatcher(data, handler).Dispatch(aged.User, aged.Operation.Id)); Assert.Equal(0, handler.Calls);
    }

    [FinancialPostgresFact]
    public async Task Provider_identity_cannot_cross_operations_and_attempt_evidence_is_immutable()
    {
        await using var data = Data(); var first = await Prepare(data); var second = await Prepare(data, FinancialModule.WeddingPlanner); var store = new PaymentDispatchStore(data);
        var one = await store.Claim(first.User, first.Operation.Id); var two = await store.Claim(second.User, second.Operation.Id); var pi = "pi_unique_" + Guid.NewGuid().ToString("N");
        Assert.True(await store.Complete(one, new(pi, "processing", 14225, "USD")));
        await Assert.ThrowsAsync<PostgresException>(() => store.Complete(two, new(pi, "processing", 14225, "USD")));
        foreach (var table in new[] { "payment_dispatch_attempts", "payment_dispatch_results" })
        {
            await using var mutation = data.CreateCommand("UPDATE financials." + table + " SET operation_id=operation_id");
            await Assert.ThrowsAsync<PostgresException>(async () => await mutation.ExecuteNonQueryAsync());
            await using var truncate = data.CreateCommand("TRUNCATE financials." + table + " CASCADE");
            await Assert.ThrowsAsync<PostgresException>(async () => await truncate.ExecuteNonQueryAsync());
        }
    }
}
