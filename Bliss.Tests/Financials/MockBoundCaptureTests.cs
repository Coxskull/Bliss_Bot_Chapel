using System.Security.Claims;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;

namespace Bliss.Tests.Financials;
public sealed class MockBoundCaptureTests
{
    private static NpgsqlDataSource Data() => NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!);
    private static async Task<(ClaimsPrincipal User, PreparedPaymentOperation Operation)> Prepare(NpgsqlDataSource data)
    {
        var subject = "bound-store-" + Guid.NewGuid(); var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "fixture"));
        var quote = new AuthorizedQuoteSnapshot(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid(), FinancialModule.BlissBot, Guid.NewGuid()), subject, Guid.NewGuid(), 500, "USD");
        var store = new PaymentOperationStore(data); await store.FreezeQuote(quote); return (user, await store.Prepare(user, quote.Id, "fixture"));
    }
    private static async Task<string> Bind(NpgsqlDataSource data, ClaimsPrincipal user, PreparedPaymentOperation operation)
    {
        var dispatch = new PaymentDispatchStore(data); var claim = await dispatch.Claim(user, operation.Id); var pi = "pi_fixture_" + operation.Id.ToString("N");
        Assert.True(await dispatch.Complete(claim, new(pi, "requires_payment_method", operation.AmountMinor, "USD"))); return pi;
    }
    [FinancialPostgresFact]
    public async Task Mock_profile_payer_owner_and_event_evidence_cannot_be_rebound_or_mutated()
    {
        await using var data = Data(); var prepared = await Prepare(data); var pi = await Bind(data, prepared.User, prepared.Operation);
        var store = new MockPaymentCaptureStore(data); var payer = Guid.NewGuid(); var owner = Guid.NewGuid();
        var payment = new VerifiedSandboxPayment("evt_" + Guid.NewGuid().ToString("N"), pi, 500, "USD"); var fingerprint = new string('A', 64);
        var journal = await store.CaptureBound(prepared.Operation, payment, payer, owner, "mock-alpha", fingerprint);
        Assert.Equal(journal, await store.CaptureBound(prepared.Operation, payment, payer, owner, "mock-alpha", fingerprint));
        foreach (var changed in new[] { (Payer: Guid.NewGuid(), Owner: owner, Profile: "mock-alpha"), (Payer: payer, Owner: Guid.NewGuid(), Profile: "mock-alpha"), (Payer: payer, Owner: owner, Profile: "mock-other") })
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.CaptureBound(prepared.Operation, payment, changed.Payer, changed.Owner, changed.Profile, fingerprint));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CaptureBound(prepared.Operation, payment, payer, owner, "mock-alpha", new string('B', 64)));
        var foreign = await Prepare(data); var foreignPi = await Bind(data, foreign.User, foreign.Operation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CaptureBound(foreign.Operation, payment with { PaymentIntentId = foreignPi }, payer, owner, "mock-alpha", fingerprint));
        Assert.Null(await store.Captured(foreign.Operation.Id));
        var foreignJournal = await store.CaptureBound(foreign.Operation, payment with { EventId = "evt_" + Guid.NewGuid().ToString("N"), PaymentIntentId = foreignPi }, payer, owner, "mock-alpha", fingerprint);
        await using (var forged = data.CreateCommand("INSERT INTO financials.mock_provider_events(event_id,operation_id,fingerprint,journal_id) VALUES(@e,@i,@f,@j)"))
        { forged.Parameters.AddWithValue("e", "evt_" + Guid.NewGuid().ToString("N")); forged.Parameters.AddWithValue("i", prepared.Operation.Id); forged.Parameters.AddWithValue("f", fingerprint); forged.Parameters.AddWithValue("j", foreignJournal); await Assert.ThrowsAsync<PostgresException>(async () => await forged.ExecuteNonQueryAsync()); }
        foreach (var table in new[] { "mock_provider_bindings", "mock_provider_events" })
        {
            await using var mutation = data.CreateCommand("UPDATE financials." + table + " SET operation_id=operation_id"); await Assert.ThrowsAsync<PostgresException>(async () => await mutation.ExecuteNonQueryAsync());
            await using var delete = data.CreateCommand("DELETE FROM financials." + table); await Assert.ThrowsAsync<PostgresException>(async () => await delete.ExecuteNonQueryAsync());
            await using var truncate = data.CreateCommand("TRUNCATE financials." + table + " CASCADE"); await Assert.ThrowsAsync<PostgresException>(async () => await truncate.ExecuteNonQueryAsync());
        }
    }
    [FinancialPostgresFact]
    public async Task Unbound_or_colliding_callbacks_roll_back_profile_receipt_and_capture_together()
    {
        await using var data = Data(); var prepared = await Prepare(data); var store = new MockPaymentCaptureStore(data); var payer = Guid.NewGuid(); var owner = Guid.NewGuid();
        var payment = new VerifiedSandboxPayment("evt_" + Guid.NewGuid().ToString("N"), "pi_fixture_" + prepared.Operation.Id.ToString("N"), 500, "USD");
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.BoundMockIntent(prepared.Operation.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CaptureBound(prepared.Operation, payment, payer, owner, "mock-alpha", new string('A', 64)));
        var legacy = await Prepare(data); var legacyPayment = payment with { PaymentIntentId = "pi_mock_" + legacy.Operation.Id.ToString("N") };
        var legacyJournal = await store.Capture(legacy.Operation, legacyPayment, payer, owner);
        await Bind(data, prepared.User, prepared.Operation);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CaptureBound(prepared.Operation, payment, payer, owner, "mock-alpha", new string('A', 64)));
        Assert.Null(await store.Captured(prepared.Operation.Id)); Assert.Equal(legacyJournal, await store.Captured(legacy.Operation.Id));
        await using var count = data.CreateCommand("SELECT count(*) FROM financials.mock_provider_bindings WHERE operation_id=@i"); count.Parameters.AddWithValue("i", prepared.Operation.Id); Assert.Equal(0L, await count.ExecuteScalarAsync());
        var notMock = await Prepare(data); var dispatch = new PaymentDispatchStore(data); var claim = await dispatch.Claim(notMock.User, notMock.Operation.Id);
        Assert.True(await dispatch.Complete(claim, new("pi_realformat_" + notMock.Operation.Id.ToString("N"), "processing", 500, "USD")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.BoundMockIntent(notMock.Operation.Id));
    }
}
