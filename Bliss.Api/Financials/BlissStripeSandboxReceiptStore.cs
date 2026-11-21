using Bliss.Api.Financials;
using Npgsql;

namespace Bliss.Api.Financials;

/// <summary>
/// Isolated append-only staging journal. No dispatch, fulfillment or provider mutation.
/// Unique event IDs and immutable hashes prevent replay from authorizing another action.
/// </summary>
public sealed class BlissStripeSandboxReceiptStore(IConfiguration config)
{
    public async Task<bool> Record(BlissStripeVerifiedEvent verified, CancellationToken ct)
    {
        var connectionString = config["BlissStripeWebhook:JournalConnectionString"];
        var expectedDatabase = config["BlissStripeWebhook:ExpectedJournalDatabase"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(expectedDatabase))
            throw new InvalidOperationException("Bliss staging journal connection is not configured.");

        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        var localFixture = settings.Database == "bliss_ci"
            && settings.Host is "localhost" or "127.0.0.1";
        var isolatedStaging = settings.Database == "bliss_stripe_sandbox"
            && settings.Username == "bliss_stripe_ingest"
            && settings.SslMode == SslMode.VerifyFull
            && settings.Host is not ("localhost" or "127.0.0.1" or "::1")
            && !string.IsNullOrWhiteSpace(settings.Host)
            && settings.Host.Contains('.', StringComparison.Ordinal);
        if (!string.Equals(settings.Database, expectedDatabase, StringComparison.Ordinal)
            || (!localFixture && !isolatedStaging))
            throw new InvalidOperationException("Bliss webhook journal target is not an isolated sandbox database.");

        await using var connection = new NpgsqlConnection(settings.ConnectionString);
        await connection.OpenAsync(ct);
        await using var insert = new NpgsqlCommand("""
            INSERT INTO bliss_stripe_sandbox.event_receipts
                (event_id,event_type,object_id,payload_sha256,disposition)
            VALUES (@event,@type,@object,@sha,@disposition)
            ON CONFLICT (event_id) DO NOTHING
            RETURNING event_id
            """, connection);
        insert.Parameters.AddWithValue("event", verified.EventId);
        insert.Parameters.AddWithValue("type", verified.EventType);
        insert.Parameters.AddWithValue("object", verified.ObjectId);
        insert.Parameters.AddWithValue("sha", verified.PayloadSha256);
        insert.Parameters.AddWithValue("disposition", verified.Disposition);
        if (await insert.ExecuteScalarAsync(ct) is not null)
            return true;

        await using var previous = new NpgsqlCommand("""
            SELECT event_type,object_id,payload_sha256,disposition
              FROM bliss_stripe_sandbox.event_receipts WHERE event_id=@event
            """, connection);
        previous.Parameters.AddWithValue("event", verified.EventId);
        await using var reader = await previous.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)
            || reader.GetString(0) != verified.EventType
            || reader.GetString(1) != verified.ObjectId
            || reader.GetString(2) != verified.PayloadSha256
            || reader.GetString(3) != verified.Disposition)
            throw new InvalidOperationException("Conflicting Stripe event replay.");
        return false;
    }
}
