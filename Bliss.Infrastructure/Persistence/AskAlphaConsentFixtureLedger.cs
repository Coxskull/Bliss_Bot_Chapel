using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// Test-only evidence of a synthetic visitor's stated preference.
// Caller-provided actor flags are NOT an identity verification implementation.
// No API route or DI binding is provided: real visitor authentication is still missing.
public sealed record AskAlphaConsentScope(
    Guid AdvertiserId, Guid WorkspaceId, Guid HandoffId, Guid SessionId,
    Guid QuoteId, string SourceSlug);
public sealed record AskAlphaConsentActor(
    string Subject, bool Authenticated, bool VisitorVerified, bool SyntheticFixture);
public sealed record AskAlphaConsentIntent(
    AskAlphaConsentScope Scope, AskAlphaConsentActor Actor, string Decision,
    string TranscriptSha256, int TurnCount, int ExpectedVersion, string RetryKey);
public sealed record AskAlphaConsentEvent(
    Guid Id, AskAlphaConsentScope Scope, string ActorSubject, string Decision,
    string TranscriptSha256, int TurnCount, int Version, int ExpectedVersion,
    string RetryKey, string PreviousSha256, string EventSha256, DateTime RecordedAt,
    bool IsReplay = false);
public sealed record AskAlphaConsentEvidenceState(
    string Status, int Version, string CurrentEvidenceSha256,
    IReadOnlyList<AskAlphaConsentEvent> Events,
    bool IdentityIndependentlyVerified = false, bool TranscriptLinked = false,
    bool TranscriptExportAuthorized = false, bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false, bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public static class AskAlphaConsentFixturePolicy
{
    public const string Zero = "0000000000000000000000000000000000000000000000000000000000000000";
    public static void Validate(AskAlphaConsentIntent intent)
    {
        var scope = intent.Scope;
        if (scope.AdvertiserId == Guid.Empty || scope.WorkspaceId == Guid.Empty ||
            scope.HandoffId == Guid.Empty || scope.SessionId == Guid.Empty || scope.QuoteId == Guid.Empty ||
            scope.SourceSlug is null ||
            !Regex.IsMatch(scope.SourceSlug, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z") ||
            scope.SourceSlug.Length > 96)
            throw new ArgumentException("Exact synthetic campaign, session, source and quote scope required.");
        if (!intent.Actor.Authenticated || !intent.Actor.VisitorVerified || !intent.Actor.SyntheticFixture ||
            intent.Actor.Subject is null ||
            !Regex.IsMatch(intent.Actor.Subject, @"\Afixture-visitor-[a-z0-9-]{1,108}\z") ||
            intent.Actor.Subject.Length > 128)
            throw new UnauthorizedAccessException("Verified synthetic visitor fixture required; advertiser authority is insufficient.");
        if (intent.Decision is not ("OPT_IN" or "REVOKE") ||
            intent.TranscriptSha256 is null ||
            !Regex.IsMatch(intent.TranscriptSha256, @"\A[0-9A-F]{64}\z") ||
            intent.TurnCount is < 1 or > 10000 ||
            intent.ExpectedVersion is < 0 or > 99 ||
            intent.RetryKey is null || intent.RetryKey.Length is < 8 or > 100 ||
            !Regex.IsMatch(intent.RetryKey, @"\A[a-zA-Z0-9_-]+\z"))
            throw new ArgumentException("Bounded, exact synthetic consent intent required.");
    }
    public static string ComputeHash(AskAlphaConsentEvent record)
    {
        // Fields are fixed UUID, uppercase hex, bounded fixture codes, digits and
        // alphanumeric/dash/underscore; separator ambiguity is excluded by Validate.
        var scope = record.Scope;
        var canon = string.Join("|", "AGM-CONSENT-FIXTURE/1", record.Id.ToString("N"),
            scope.AdvertiserId.ToString("N"), scope.WorkspaceId.ToString("N"),
            scope.HandoffId.ToString("N"), scope.SessionId.ToString("N"),
            scope.QuoteId.ToString("N"), scope.SourceSlug, record.ActorSubject,
            record.Decision, record.TranscriptSha256, record.TurnCount,
            record.Version, record.ExpectedVersion, record.RetryKey, record.PreviousSha256);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canon)));
    }
    public static AskAlphaConsentEvidenceState Evaluate(
        AskAlphaConsentScope scope, string actorSubject, IReadOnlyList<AskAlphaConsentEvent> records)
    {
        string prior = Zero;
        var version = 0;
        string state = "UNRECORDED";
        foreach (var entry in records)
        {
            if (entry.Scope != scope || entry.ActorSubject != actorSubject ||
                entry.Version != ++version || entry.ExpectedVersion != version - 1 ||
                entry.PreviousSha256 != prior ||
                entry.EventSha256 != ComputeHash(entry) ||
                (version == 1 && entry.Decision != "OPT_IN") ||
                (version == 2 && entry.Decision != "REVOKE") ||
                version > 2 ||
                (version == 2 && (entry.TranscriptSha256 != records[0].TranscriptSha256 ||
                                  entry.TurnCount != records[0].TurnCount)))
                throw new InvalidOperationException("Consent fixture evidence chain is invalid.");
            prior = entry.EventSha256;
            state = entry.Decision == "REVOKE" ? "REVOKED" : "OPT_IN_RECORDED_UNVERIFIED";
        }
        return new AskAlphaConsentEvidenceState(state, version, prior, records);
    }
}

// Isolated PostgreSQL fixture only. Initialization is an explicit CI script;
// neither this store nor its schema is registered in the hosted application.
public sealed class AskAlphaConsentFixtureLedger
{
    private readonly NpgsqlDataSource data;
    public AskAlphaConsentFixtureLedger(NpgsqlDataSource source)
    {
        var connection = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        if (connection.Host != "127.0.0.1" || connection.Database != "financial_test")
            throw new InvalidOperationException("Consent fixtures require explicit loopback financial_test only.");
        data = source;
    }

    public async Task<AskAlphaConsentEvent> AppendAsync(AskAlphaConsentIntent intent,
        CancellationToken ct = default)
    {
        AskAlphaConsentFixturePolicy.Validate(intent);
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using (var guard = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@scope, 0))", connection, transaction))
        {
            guard.Parameters.AddWithValue("scope", intent.Scope.HandoffId.ToString("N"));
            await guard.ExecuteNonQueryAsync(ct);
        }
        var current = await Load(connection, transaction, intent.Scope.HandoffId, ct);
        var state = AskAlphaConsentFixturePolicy.Evaluate(intent.Scope, intent.Actor.Subject, current);
        var replay = current.SingleOrDefault(x => x.ActorSubject == intent.Actor.Subject &&
            x.RetryKey == intent.RetryKey);
        if (replay is not null)
        {
            if (replay.Decision != intent.Decision || replay.ExpectedVersion != intent.ExpectedVersion ||
                replay.TranscriptSha256 != intent.TranscriptSha256 || replay.TurnCount != intent.TurnCount)
                throw new InvalidOperationException("Idempotency key belongs to different consent intent.");
            await transaction.CommitAsync(ct);
            return replay with { IsReplay = true };
        }
        if (intent.ExpectedVersion != state.Version)
            throw new InvalidOperationException("Stale consent evidence version.");
        if ((intent.Decision == "OPT_IN" && state.Version != 0) ||
            (intent.Decision == "REVOKE" && (state.Version != 1 ||
              current[0].TranscriptSha256 != intent.TranscriptSha256 ||
              current[0].TurnCount != intent.TurnCount)))
            throw new InvalidOperationException("Consent revocation/opt-in transition not permitted.");

        var now = DateTime.UtcNow;
        var record = new AskAlphaConsentEvent(Guid.NewGuid(), intent.Scope, intent.Actor.Subject,
            intent.Decision, intent.TranscriptSha256, intent.TurnCount, state.Version + 1,
            intent.ExpectedVersion, intent.RetryKey, state.CurrentEvidenceSha256, "", now);
        record = record with { EventSha256 = AskAlphaConsentFixturePolicy.ComputeHash(record) };
        await using (var save = new NpgsqlCommand("""
            INSERT INTO agm_consent_fixture.events(
              id,advertiser_id,workspace_id,handoff_id,session_id,quote_id,source_slug,
              actor_subject,decision,transcript_sha256,turn_count,version,expected_version,
              retry_key,previous_sha256,event_sha256,recorded_at)
            VALUES (@id,@a,@w,@h,@s,@q,@slug,@actor,@choice,@digest,@turns,@v,@expected,
                    @retry,@previous,@hash,@time)
            """, connection, transaction))
        {
            save.Parameters.AddWithValue("id", record.Id);
            save.Parameters.AddWithValue("a", record.Scope.AdvertiserId);
            save.Parameters.AddWithValue("w", record.Scope.WorkspaceId);
            save.Parameters.AddWithValue("h", record.Scope.HandoffId);
            save.Parameters.AddWithValue("s", record.Scope.SessionId);
            save.Parameters.AddWithValue("q", record.Scope.QuoteId);
            save.Parameters.AddWithValue("slug", record.Scope.SourceSlug);
            save.Parameters.AddWithValue("actor", record.ActorSubject);
            save.Parameters.AddWithValue("choice", record.Decision);
            save.Parameters.AddWithValue("digest", record.TranscriptSha256);
            save.Parameters.AddWithValue("turns", record.TurnCount);
            save.Parameters.AddWithValue("v", record.Version);
            save.Parameters.AddWithValue("expected", record.ExpectedVersion);
            save.Parameters.AddWithValue("retry", record.RetryKey);
            save.Parameters.AddWithValue("previous", record.PreviousSha256);
            save.Parameters.AddWithValue("hash", record.EventSha256);
            save.Parameters.AddWithValue("time", record.RecordedAt);
            await save.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return record;
    }

    public async Task<AskAlphaConsentEvidenceState> ReadAsync(
        AskAlphaConsentScope scope, AskAlphaConsentActor actor, CancellationToken ct = default)
    {
        // Subject scope and synthetic visitor control apply equally to reads.
        AskAlphaConsentFixturePolicy.Validate(new(scope, actor, "OPT_IN",
            AskAlphaConsentFixturePolicy.Zero, 1, 0, "fixture_read"));
        await using var connection = await data.OpenConnectionAsync(ct);
        var records = await Load(connection, null, scope.HandoffId, ct);
        return AskAlphaConsentFixturePolicy.Evaluate(scope, actor.Subject, records);
    }

    private static async Task<List<AskAlphaConsentEvent>> Load(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid handoff, CancellationToken ct)
    {
        await using var query = new NpgsqlCommand("""
            SELECT id,advertiser_id,workspace_id,handoff_id,session_id,quote_id,source_slug,
                   actor_subject,decision,transcript_sha256,turn_count,version,expected_version,
                   retry_key,previous_sha256,event_sha256,recorded_at
              FROM agm_consent_fixture.events WHERE handoff_id=@handoff ORDER BY version
            """, connection, transaction);
        query.Parameters.AddWithValue("handoff", handoff);
        await using var reader = await query.ExecuteReaderAsync(ct);
        var result = new List<AskAlphaConsentEvent>();
        while (await reader.ReadAsync(ct))
        {
            var scope = new AskAlphaConsentScope(reader.GetGuid(1), reader.GetGuid(2),
                reader.GetGuid(3), reader.GetGuid(4), reader.GetGuid(5), reader.GetString(6));
            result.Add(new AskAlphaConsentEvent(reader.GetGuid(0),scope,reader.GetString(7),
                reader.GetString(8),reader.GetString(9).Trim(),reader.GetInt32(10),
                reader.GetInt32(11),reader.GetInt32(12),reader.GetString(13),
                reader.GetString(14).Trim(),reader.GetString(15).Trim(),reader.GetDateTime(16)));
        }
        return result;
    }
}
