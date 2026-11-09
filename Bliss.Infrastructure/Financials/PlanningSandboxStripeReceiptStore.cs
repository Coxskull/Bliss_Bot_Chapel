using System.Data;
using Npgsql;

namespace Bliss.Infrastructure.Financials;

// Not registered in production DI. Stage only on an expressly isolated
// sandbox Postgres target after review of its migration and privileges.
// Never sends an HTTP response, message, refund, campaign or customer order.
public sealed class PlanningSandboxStripeReceiptStore(NpgsqlDataSource data)
{
    public async Task<PlanningSandboxReceiptDecision> ObserveSigned(
        byte[] rawBody, string signature, string signingSecret, DateTimeOffset now,
        CancellationToken ct = default)
    {
        // Verify the original bytes BEFORE opening a DB connection.
        var verified = StripePlanningSandboxWebhookVerifier.Verify(
            rawBody, signature, signingSecret, now);

        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        // Fixed lock order fences concurrent requests sharing event, checkout
        // or PaymentIntent identities without holding any network I/O lock.
        foreach (var key in new[] {
            "alpha346:event:" + verified.EventId,
            "alpha346:session:" + verified.SessionId,
            "alpha346:intent:" + verified.PaymentIntentId
        })
        {
            await using var gate = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", connection, transaction);
            gate.Parameters.AddWithValue("key", key);
            await gate.ExecuteNonQueryAsync(ct);
        }

        var prior = new List<PlanningSandboxReceiptEvidence>();
        await using (var read = new NpgsqlCommand("""
            SELECT event_id, checkout_session_id, payment_intent_id,
                   receipt_state, payload_sha256
            FROM alpha_sandbox.planning_checkout_webhook_receipts
            WHERE event_id=@event OR checkout_session_id=@session OR payment_intent_id=@intent
            FOR UPDATE
            """, connection, transaction))
        {
            read.Parameters.AddWithValue("event", verified.EventId);
            read.Parameters.AddWithValue("session", verified.SessionId);
            read.Parameters.AddWithValue("intent", verified.PaymentIntentId);
            await using var rows = await read.ExecuteReaderAsync(ct);
            while (await rows.ReadAsync(ct))
                prior.Add(new(
                    rows.GetString(0), rows.GetString(1), rows.GetString(2),
                    rows.GetString(3), rows.GetString(4)));
        }

        var decision = PlanningSandboxReceiptReview.Inspect(verified, prior);
        if (decision.RecordPending)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO alpha_sandbox.planning_checkout_webhook_receipts
                  (event_id, checkout_session_id, payment_intent_id, receipt_state, payload_sha256)
                VALUES (@event, @session, @intent, @state, @hash)
                """, connection, transaction);
            insert.Parameters.AddWithValue("event", verified.EventId);
            insert.Parameters.AddWithValue("session", verified.SessionId);
            insert.Parameters.AddWithValue("intent", verified.PaymentIntentId);
            insert.Parameters.AddWithValue("state", verified.ReceiptState);
            insert.Parameters.AddWithValue("hash", verified.FingerprintSha256);
            if (await insert.ExecuteNonQueryAsync(ct) != 1)
                throw new InvalidOperationException("Sandbox receipt not durably inserted.");
        }
        // An exception or missing table leaves the transaction uncommitted.
        // Future hosted HTTP adapters MUST NOT return 2xx on persistence errors.
        await transaction.CommitAsync(ct);
        return decision; // Both booleans remain false: no real money or fulfillment.
    }
}
