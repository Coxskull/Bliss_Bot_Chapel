using Npgsql;

namespace Bliss.Infrastructure.Financials;
public sealed record MockReconciliationReading(string State, bool RequiresReview, bool RecoveryRecorded);

// Trusted fixture observations, never real provider retrieval or client-controlled monetary authority.
public sealed class MockPaymentReconciliationStore(NpgsqlDataSource data)
{
    public async Task<Guid> Recover(PreparedPaymentOperation expected, string profile, string intent, string status, string actor, CancellationToken ct = default)
    {
        Validate(profile, intent);
        if (status is not ("requires_payment_method" or "requires_confirmation" or "requires_action" or "processing" or "requires_capture" or "canceled" or "succeeded") || string.IsNullOrWhiteSpace(actor) || actor.Length > 200)
            throw new ArgumentException("Fixture recovery status/actor required.");
        await using var c = await data.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await Gate(c, tx, "dispatch:" + expected.Id.ToString("N"), ct);
        await Gate(c, tx, "mock-intent:" + intent, ct);
        await using (var locked = new NpgsqlCommand("SELECT id FROM financials.payment_operations WHERE id=@i FOR UPDATE", c, tx))
        { locked.Parameters.AddWithValue("i", expected.Id); if (await locked.ExecuteScalarAsync(ct) is null) throw new InvalidOperationException("Unknown operation."); }
        var actual = await new MockPaymentCaptureStore(data).Operation(expected.Id, ct);
        if (actual != expected) throw new InvalidOperationException("Lookup conflicts with immutable funding authority.");
        await using (var prior = new NpgsqlCommand("SELECT id,profile_id,payment_intent_id,provider_status FROM financials.mock_payment_recoveries WHERE operation_id=@i", c, tx))
        {
            prior.Parameters.AddWithValue("i", expected.Id); await using var r = await prior.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) { if (r.GetString(1) != profile || r.GetString(2) != intent || r.GetString(3) != status) throw new InvalidOperationException("Recovery conflicts with original evidence."); return r.GetGuid(0); }
        }
        await using (var check = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts WHERE operation_id=@i AND started_at<=clock_timestamp()-interval '20 hours')
              AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts a WHERE operation_id=@i AND lease_until>clock_timestamp()
                AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results r WHERE r.attempt_id=a.id))
              AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE operation_id=@i AND state='BOUND')
              AND NOT EXISTS(SELECT 1 FROM financials.payment_captures WHERE operation_id=@i)
              AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE state='BOUND' AND payment_intent_id=@p)
              AND NOT EXISTS(SELECT 1 FROM financials.payment_captures WHERE payment_intent_id=@p)
            """, c, tx))
        { check.Parameters.AddWithValue("i", expected.Id); check.Parameters.AddWithValue("p", intent); if (await check.ExecuteScalarAsync(ct) is not true) throw new InvalidOperationException("Only aged unresolved fixture operations may be recovered."); }
        var id = Guid.NewGuid(); await using var insert = new NpgsqlCommand("INSERT INTO financials.mock_payment_recoveries(id,operation_id,profile_id,payment_intent_id,provider_status,actor_subject) VALUES(@id,@i,@profile,@p,@status,@actor)", c, tx);
        insert.Parameters.AddWithValue("id", id); insert.Parameters.AddWithValue("i", expected.Id); insert.Parameters.AddWithValue("profile", profile); insert.Parameters.AddWithValue("p", intent); insert.Parameters.AddWithValue("status", status); insert.Parameters.AddWithValue("actor", actor);
        await insert.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return id;
    }
    public async Task Observe(PreparedPaymentOperation expected, string profile, string eventId, string intent, string state, string fingerprint, CancellationToken ct = default)
    {
        Validate(profile, intent);
        if (state is not ("FAILED" or "CANCELED" or "PROCESSING") || string.IsNullOrWhiteSpace(eventId) || !eventId.StartsWith("evt_", StringComparison.Ordinal) || eventId.Length > 120 || eventId.Any(char.IsControl) || fingerprint.Length != 64 || fingerprint.Any(x => !Uri.IsHexDigit(x)))
            throw new ArgumentException("Bounded immutable mock status evidence required.");
        await using var c = await data.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await using (var locked = new NpgsqlCommand("SELECT id FROM financials.payment_operations WHERE id=@i FOR UPDATE", c, tx))
        { locked.Parameters.AddWithValue("i", expected.Id); if (await locked.ExecuteScalarAsync(ct) is null) throw new InvalidOperationException("Unknown operation."); }
        await Gate(c, tx, "stripe-sandbox:" + eventId, ct);
        var store = new MockPaymentCaptureStore(data);
        if (await store.Operation(expected.Id, ct) != expected || await store.BoundMockIntent(expected.Id, ct) != intent) throw new InvalidOperationException("Status conflicts with bound funding authority.");
        await using (var conflict = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM financials.mock_provider_events WHERE event_id=@e)
              OR EXISTS(SELECT 1 FROM financials.stripe_receipts WHERE event_id=@e)
              OR EXISTS(SELECT 1 FROM financials.mock_provider_bindings WHERE operation_id=@i AND profile_id<>@profile)
              OR EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=@i AND profile_id<>@profile)
              OR EXISTS(SELECT 1 FROM financials.mock_payment_status_events WHERE operation_id=@i AND profile_id<>@profile)
            """, c, tx))
        { conflict.Parameters.AddWithValue("e", eventId); conflict.Parameters.AddWithValue("i", expected.Id); conflict.Parameters.AddWithValue("profile", profile); if (await conflict.ExecuteScalarAsync(ct) is true) throw new InvalidOperationException("Status event/profile conflicts with immutable attribution."); }
        await using (var prior = new NpgsqlCommand("SELECT operation_id,profile_id,payment_intent_id,state,fingerprint FROM financials.mock_payment_status_events WHERE event_id=@e", c, tx))
        { prior.Parameters.AddWithValue("e", eventId); await using var r = await prior.ExecuteReaderAsync(ct); if (await r.ReadAsync(ct)) { if (r.GetGuid(0) != expected.Id || r.GetString(1) != profile || r.GetString(2) != intent || r.GetString(3) != state || r.GetString(4) != fingerprint) throw new InvalidOperationException("Status replay changed original evidence."); return; } }
        await using var insert = new NpgsqlCommand("INSERT INTO financials.mock_payment_status_events(event_id,operation_id,profile_id,payment_intent_id,state,fingerprint) VALUES(@e,@i,@profile,@p,@state,@f)", c, tx);
        insert.Parameters.AddWithValue("e", eventId); insert.Parameters.AddWithValue("i", expected.Id); insert.Parameters.AddWithValue("profile", profile); insert.Parameters.AddWithValue("p", intent); insert.Parameters.AddWithValue("state", state); insert.Parameters.AddWithValue("f", fingerprint);
        await insert.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<MockReconciliationReading> Read(Guid operation, CancellationToken ct = default)
    {
        await using var c = data.CreateCommand("""
            SELECT EXISTS(SELECT 1 FROM financials.payment_captures WHERE operation_id=@i),
              (EXISTS(SELECT 1 FROM financials.mock_payment_status_events WHERE operation_id=@i AND state='CANCELED') OR EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=@i AND provider_status='canceled')),
              EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=@i),
              (SELECT state FROM financials.mock_payment_status_events WHERE operation_id=@i ORDER BY recorded_at DESC,event_id DESC LIMIT 1),
              EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts WHERE operation_id=@i AND started_at<=clock_timestamp()-interval '20 hours') AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE operation_id=@i AND state='BOUND') AND NOT EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=@i)
            """); c.Parameters.AddWithValue("i", operation); await using var r = await c.ExecuteReaderAsync(ct); await r.ReadAsync(ct);
        return new(r.GetBoolean(0) ? "CAPTURED_TEST_ONLY" : r.GetBoolean(1) ? "CANCELED_REVIEW_REQUIRED" : !r.IsDBNull(3) ? r.GetString(3) + "_TEST_ONLY" : r.GetBoolean(2) ? "RECOVERED_PENDING_CALLBACK" : r.GetBoolean(4) ? "MANUAL_REVIEW_TEST_ONLY" : "UNOBSERVED", r.GetBoolean(1)||r.GetBoolean(4), r.GetBoolean(2));
    }
    private static void Validate(string profile, string intent)
    { if (string.IsNullOrWhiteSpace(profile) || profile.Length > 64 || profile.Any(x => !char.IsAsciiLetterOrDigit(x) && x != '-' && x != '_') || string.IsNullOrWhiteSpace(intent) || !intent.StartsWith("pi_fixture_", StringComparison.Ordinal) || intent.Length > 120 || intent.Any(char.IsControl)) throw new ArgumentException("Synthetic profile/identity required."); }
    private static async Task Gate(NpgsqlConnection c, NpgsqlTransaction tx, string key, CancellationToken ct)
    { await using var gate = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@k,0))", c, tx); gate.Parameters.AddWithValue("k", key); await gate.ExecuteNonQueryAsync(ct); }
}
