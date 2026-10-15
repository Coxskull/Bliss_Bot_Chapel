using System.Security.Claims;
using Bliss.Domain.Financials;
using Npgsql;

namespace Bliss.Infrastructure.Financials;

public sealed record PaymentDispatchClaim(string State, PreparedPaymentOperation Operation, Guid? AttemptId = null,
    string? PaymentIntentId = null, string? ProviderStatus = null);

// Inactive foundation: no hosted worker, DI registration or public dispatch endpoint.
// Claim and completion are short transactions; provider I/O never holds a database lock.
public sealed class PaymentDispatchStore(NpgsqlDataSource data)
{
    private static async Task Lock(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid operation, CancellationToken ct)
    {
        await using var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@k,0))", connection, transaction);
        gate.Parameters.AddWithValue("k", "dispatch:" + operation.ToString("N"));
        await gate.ExecuteNonQueryAsync(ct);
    }

    public async Task<PaymentDispatchClaim> Claim(ClaimsPrincipal user, Guid operationId, CancellationToken ct = default)
    {
        var subject = PaymentOperationStore.AuthenticatedSubject(user);
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Lock(connection, transaction, operationId, ct);
        await using var query = new NpgsqlCommand("""
            SELECT o.quote_id,q.merchant_id,q.module,q.campaign_id,q.recipient_account,q.amount_minor,q.currency,o.provider_key
            FROM financials.payment_operations o JOIN financials.authorized_quotes q ON q.id=o.quote_id
            WHERE o.id=@i AND o.advertiser_subject=@s AND q.advertiser_subject=@s
            """, connection, transaction);
        query.Parameters.AddWithValue("i", operationId); query.Parameters.AddWithValue("s", subject);
        PreparedPaymentOperation operation;
        await using (var reader = await query.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) throw new UnauthorizedAccessException("Funding operation unavailable to principal.");
            operation = new(operationId, reader.GetGuid(0), new(reader.GetGuid(1), Enum.Parse<FinancialModule>(reader.GetString(2)), reader.GetGuid(3)),
                reader.GetGuid(4), reader.GetInt64(5), reader.GetString(6), reader.GetString(7));
        }
        await using (var bound = new NpgsqlCommand("SELECT payment_intent_id,provider_status FROM financials.payment_dispatch_results WHERE operation_id=@i AND state='BOUND' UNION ALL SELECT payment_intent_id,provider_status FROM financials.mock_payment_recoveries WHERE operation_id=@i", connection, transaction))
        {
            bound.Parameters.AddWithValue("i", operationId);
            await using var reader = await bound.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) return new("BOUND", operation, PaymentIntentId: reader.GetString(0), ProviderStatus: reader.GetString(1));
        }
        // Stripe may prune idempotency keys after 24h. Never recreate automatically near that boundary.
        await using (var attempts = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts WHERE operation_id=@i AND started_at <= clock_timestamp()-interval '20 hours'),
                   EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts a WHERE operation_id=@i AND lease_until>clock_timestamp()
                     AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results r WHERE r.attempt_id=a.id))
            """, connection, transaction))
        {
            attempts.Parameters.AddWithValue("i", operationId);
            await using var reader = await attempts.ExecuteReaderAsync(ct); await reader.ReadAsync(ct);
            if (reader.GetBoolean(0)) return new("MANUAL_REVIEW", operation);
            if (reader.GetBoolean(1)) return new("IN_FLIGHT", operation);
        }
        var attemptId = Guid.NewGuid();
        await using var insert = new NpgsqlCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id) VALUES(@a,@i)", connection, transaction);
        insert.Parameters.AddWithValue("a", attemptId); insert.Parameters.AddWithValue("i", operationId);
        await insert.ExecuteNonQueryAsync(ct); await transaction.CommitAsync(ct);
        return new("CLAIMED", operation, attemptId);
    }

    public async Task<bool> Complete(PaymentDispatchClaim claim, SandboxPaymentIntent? intent, CancellationToken ct = default)
    {
        if (claim.State != "CLAIMED" || claim.AttemptId is null) throw new ArgumentException("Claimed attempt required.");
        if (intent is not null && (intent.AmountMinor != claim.Operation.AmountMinor || intent.Currency != claim.Operation.Currency ||
            string.IsNullOrWhiteSpace(intent.Id) || !intent.Id.StartsWith("pi_", StringComparison.Ordinal) || intent.Id.Length > 120 || intent.Id.Any(char.IsControl) ||
            intent.Status is not ("requires_payment_method" or "requires_confirmation" or "requires_action" or "processing" or "requires_capture" or "canceled" or "succeeded")))
            throw new InvalidOperationException("Provider result conflicts with funding operation.");
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Lock(connection, transaction, claim.Operation.Id, ct);
        if (intent is not null)
        {
            await using var identity = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@k,0))", connection, transaction);identity.Parameters.AddWithValue("k", "mock-intent:" + intent.Id);await identity.ExecuteNonQueryAsync(ct);
        }
        // Fence stale responses, expired leases and altered snapshots against immutable persisted authority.
        await using var insert = new NpgsqlCommand("""
            INSERT INTO financials.payment_dispatch_results(attempt_id,operation_id,state,payment_intent_id,provider_status)
            SELECT a.id,a.operation_id,@state,@pi,@status FROM financials.payment_dispatch_attempts a
            JOIN financials.payment_operations o ON o.id=a.operation_id JOIN financials.authorized_quotes q ON q.id=o.quote_id
            WHERE NOT EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE payment_intent_id=@pi AND operation_id<>@i)
              AND a.id=@a AND a.operation_id=@i AND a.lease_until>clock_timestamp()
              AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts n WHERE n.operation_id=a.operation_id AND n.sequence>a.sequence)
              AND o.quote_id=@q AND o.provider_key=@k AND q.merchant_id=@m AND q.module=@module AND q.campaign_id=@c
              AND q.recipient_account=@recipient AND q.amount_minor=@amount AND q.currency=@currency
            ON CONFLICT(attempt_id) DO NOTHING RETURNING attempt_id
            """, connection, transaction);
        insert.Parameters.AddWithValue("a", claim.AttemptId.Value); insert.Parameters.AddWithValue("i", claim.Operation.Id);
        insert.Parameters.AddWithValue("state", intent is null ? "UNKNOWN" : "BOUND");
        insert.Parameters.AddWithValue("pi", NpgsqlTypes.NpgsqlDbType.Varchar, (object?)intent?.Id ?? DBNull.Value);
        insert.Parameters.AddWithValue("status", NpgsqlTypes.NpgsqlDbType.Varchar, (object?)intent?.Status ?? DBNull.Value);
        insert.Parameters.AddWithValue("q", claim.Operation.QuoteId); insert.Parameters.AddWithValue("k", claim.Operation.ProviderKey);
        insert.Parameters.AddWithValue("m", claim.Operation.Scope.MerchantId); insert.Parameters.AddWithValue("module", claim.Operation.Scope.Module.ToString());
        insert.Parameters.AddWithValue("c", claim.Operation.Scope.CampaignId); insert.Parameters.AddWithValue("recipient", claim.Operation.RecipientAccountId);
        insert.Parameters.AddWithValue("amount", claim.Operation.AmountMinor); insert.Parameters.AddWithValue("currency", claim.Operation.Currency);
        var inserted = await insert.ExecuteScalarAsync(ct) is Guid;
        await transaction.CommitAsync(ct); return inserted;
    }
}
