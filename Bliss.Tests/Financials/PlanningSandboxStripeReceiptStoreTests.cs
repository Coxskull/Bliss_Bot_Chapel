using Bliss.Infrastructure.Financials;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class PlanningSandboxStripeReceiptStoreTests
{
    private static readonly DateTimeOffset Now = StripePlanningSandboxWebhookTests.Now;
    private const string Secret = StripePlanningSandboxWebhookTests.Secret;

    private static NpgsqlDataSource Database()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Disposable PostgreSQL fixture required.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", settings.Host);
        Assert.Equal("financial_test", settings.Database);
        return NpgsqlDataSource.Create(connection);
    }
    private static (byte[] Body, string Signature, string EventId, string SessionId, string IntentId) Fixture(
        string? eventId = null, string? sessionId = null, string? intentId = null,
        string type = "checkout.session.completed", string status = "paid")
    {
        var evt = eventId ?? "evt_synthetic_" + Guid.NewGuid().ToString("N");
        var session = sessionId ?? "cs_test_" + Guid.NewGuid().ToString("N");
        var intent = intentId ?? "pi_synthetic_" + Guid.NewGuid().ToString("N");
        var body = StripePlanningSandboxWebhookTests.Body(
            id:evt, sessionId:session, intentId:intent,
            type:type, paymentStatus:status);
        return (body, StripePlanningSandboxWebhookTests.Signature(body), evt, session, intent);
    }

    [FinancialPostgresFact]
    public async Task Concurrent_signed_replays_create_one_durable_test_receipt_and_no_fulfillment()
    {
        await using var data = Database();
        var store = new PlanningSandboxStripeReceiptStore(data);
        var fixture = Fixture();
        var responses = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => store.ObserveSigned(fixture.Body,fixture.Signature,Secret,Now)));
        Assert.Equal(1, responses.Count(x => x.RecordPending));
        Assert.Equal(11, responses.Count(x => x.Decision == "DUPLICATE_ALREADY_RECORDED"));
        Assert.All(responses, x => {
            Assert.False(x.FulfillmentAuthorized);
            Assert.False(x.RealRevenueRecognized);
        });
        await using var count = data.CreateCommand(
            "SELECT count(*) FROM alpha_sandbox.planning_checkout_webhook_receipts WHERE event_id=@e");
        count.Parameters.AddWithValue("e", fixture.EventId);
        Assert.Equal(1L, await count.ExecuteScalarAsync());

        // Immutable DB receipts: no mutation, no delete and no truncate.
        foreach (var sql in new[] {
            "UPDATE alpha_sandbox.planning_checkout_webhook_receipts SET receipt_state=receipt_state WHERE event_id=@e",
            "DELETE FROM alpha_sandbox.planning_checkout_webhook_receipts WHERE event_id=@e"
        }) {
            await using var mutation = data.CreateCommand(sql);
            mutation.Parameters.AddWithValue("e",fixture.EventId);
            await Assert.ThrowsAsync<PostgresException>(
                async () => await mutation.ExecuteNonQueryAsync());
        }
        await using var truncate = data.CreateCommand(
            "TRUNCATE alpha_sandbox.planning_checkout_webhook_receipts");
        await Assert.ThrowsAsync<PostgresException>(
            async () => await truncate.ExecuteNonQueryAsync());
    }

    [FinancialPostgresFact]
    public async Task Different_signed_event_same_payment_never_inserts_second_paid_receipt()
    {
        await using var data=Database();
        var store=new PlanningSandboxStripeReceiptStore(data);
        var first=Fixture();
        Assert.True((await store.ObserveSigned(first.Body,first.Signature,Secret,Now)).RecordPending);
        var second=Fixture(sessionId:first.SessionId,intentId:first.IntentId);
        var r=await store.ObserveSigned(second.Body,second.Signature,Secret,Now);
        Assert.Equal("SAME_PAYMENT_NO_DOUBLE_RECEIPT",r.Decision);
        Assert.False(r.RecordPending);
        await using var count=data.CreateCommand(
            "SELECT count(*) FROM alpha_sandbox.planning_checkout_webhook_receipts WHERE payment_intent_id=@p");
        count.Parameters.AddWithValue("p",first.IntentId);
        Assert.Equal(1L,await count.ExecuteScalarAsync());

        var differentSession=Fixture(sessionId:"cs_test_"+Guid.NewGuid().ToString("N"),intentId:first.IntentId);
        Assert.Equal("CONFLICT_HOLD",
            (await store.ObserveSigned(differentSession.Body,differentSession.Signature,Secret,Now)).Decision);
        var sameEventOtherType=Fixture(eventId:first.EventId,sessionId:first.SessionId,intentId:first.IntentId,
            type:"checkout.session.async_payment_succeeded");
        Assert.Equal("CONFLICT_HOLD",
            (await store.ObserveSigned(sameEventOtherType.Body,sameEventOtherType.Signature,Secret,Now)).Decision);
        Assert.Equal(1L,await count.ExecuteScalarAsync());
    }

    [FinancialPostgresFact]
    public async Task Bad_signature_is_rejected_before_database_write_and_pending_never_becomes_paid()
    {
        await using var data=Database();
        var store=new PlanningSandboxStripeReceiptStore(data);
        var failed=Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.ObserveSigned(failed.Body,"t=1800000000,v1=0000",Secret,Now));
        await using(var check=data.CreateCommand(
            "SELECT count(*) FROM alpha_sandbox.planning_checkout_webhook_receipts WHERE event_id=@e"))
        {
            check.Parameters.AddWithValue("e",failed.EventId);
            Assert.Equal(0L,await check.ExecuteScalarAsync());
        }

        var pending=Fixture(type:"checkout.session.completed",status:"unpaid");
        var first=await store.ObserveSigned(pending.Body,pending.Signature,Secret,Now);
        Assert.Equal("RECORD_SANDBOX_TEST_EVIDENCE_ONLY",first.Decision);
        var paid=Fixture(sessionId:pending.SessionId,intentId:pending.IntentId);
        Assert.Equal("STATE_TRANSITION_NEEDS_REVIEW",
            (await store.ObserveSigned(paid.Body,paid.Signature,Secret,Now)).Decision);
        await using var remaining=data.CreateCommand(
            "SELECT count(*) FROM alpha_sandbox.planning_checkout_webhook_receipts WHERE checkout_session_id=@s");
        remaining.Parameters.AddWithValue("s",pending.SessionId);
        Assert.Equal(1L,await remaining.ExecuteScalarAsync());
    }
}
