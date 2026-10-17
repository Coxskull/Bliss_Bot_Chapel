using System.Text.RegularExpressions;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// AGM-COMM-015: disposable PostgreSQL integration proof, NEVER hosted consent.
// No DI registration, endpoint, migration, or real identity assertion. The
// caller's subject digest is only a synthetic test scope, not verified OIDC.
public sealed record AskAlphaStagingConsentRequest(
    Guid OwnershipId, Guid WorkspaceId, string Issuer, string SubjectSha256,
    string Action, string Purpose, string NoticeVersion, string RetryKey);

public sealed record AskAlphaStagingConsentResult(
    Guid EventId, string Status, bool IsReplay = false,
    bool RealVisitorIdentityVerified = false,
    bool AuthenticatedVisitorConsentRecorded = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public sealed class AskAlphaVisitorStagingConsentLedger
{
    public const string Purpose = "CREATIVE_PREVIEW_ONLY";
    public const string NoticeVersion = "STAGING-NOT-LEGAL-V1";
    private readonly NpgsqlDataSource data;

    public AskAlphaVisitorStagingConsentLedger(NpgsqlDataSource source)
    {
        var connection = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        // Only random schemas created by the isolated planner CI PostgreSQL
        // test are writable. Reject unscoped connections and other databases.
        if (!((connection.Host == "127.0.0.1" &&
               connection.Database == "planner_lineage_ci") ||
              (connection.Host == "localhost" &&
               connection.Database == "bliss_ci")) ||
            string.IsNullOrEmpty(connection.SearchPath) ||
            !Regex.IsMatch(connection.SearchPath, @"\Aagm_j_[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
            throw new InvalidOperationException(
                "Only an isolated, explicitly scoped planner PostgreSQL test is permitted.");
        data = source;
    }

    public async Task<AskAlphaStagingConsentResult> AppendAsync(
        AskAlphaStagingConsentRequest request, CancellationToken ct = default)
    {
        Validate(request);
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Acquire the same workspace + synthetic identity lock used by
        // enrollment. Identity withdrawal and new sessions cannot race.
        await using (var identityLock = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@identity, 5018))",
            connection, tx))
        {
            identityLock.Parameters.AddWithValue("identity", string.Join("|",
                request.WorkspaceId.ToString("N"), request.Issuer,
                request.SubjectSha256));
            await identityLock.ExecuteNonQueryAsync(ct);
        }

        // Serialize with the staging table's consent transition trigger.
        // A same-key retry sees the committed result after the lock is acquired.
        await using (var guard = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@owner, 5017))",
            connection, tx))
        {
            guard.Parameters.AddWithValue("owner", request.OwnershipId.ToString("D"));
            await guard.ExecuteNonQueryAsync(ct);
        }

        var revoked = false;
        await using (var query = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM "AskAlphaVisitorConsentStaging"
                WHERE "OwnershipId"=@owner AND "Action"='REVOKE')
            """, connection, tx))
        {
            query.Parameters.AddWithValue("owner", request.OwnershipId);
            revoked = (bool)(await query.ExecuteScalarAsync(ct) ?? false);
        }
        // An OPT_IN (including same-key replay) must refer to a genuine
        // still-OPEN synthetic new session and ACTIVE workspace. Hold row
        // share locks through commit to serialize an external close.
        // A REVOKE intentionally remains possible even after closure.
        if (request.Action == "OPT_IN")
        {
            await using var eligible = new NpgsqlCommand("""
                SELECT 1 FROM "AskAlphaVisitorOwnershipStaging" o
                JOIN "WeddingPlannerPlanningSessions" s ON s."Id"=o."SessionId"
                JOIN "WeddingPlannerWorkspaces" w ON w."Id"=o."WorkspaceId"
                WHERE o."Id"=@owner AND o."WorkspaceId"=@ws
                  AND o."Issuer"=@issuer AND o."SubjectSha256"=@digest
                  AND s."WorkspaceId"=o."WorkspaceId"
                  AND s."AdvertiserId"=o."AdvertiserId"
                  AND s."SourceSystem"='STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
                  AND s."Status"='OPEN' AND w."Status"='ACTIVE'
                  AND w."AdvertiserId"=o."AdvertiserId"
                FOR SHARE OF s,w
                """, connection, tx);
            eligible.Parameters.AddWithValue("owner", request.OwnershipId);
            eligible.Parameters.AddWithValue("ws", request.WorkspaceId);
            eligible.Parameters.AddWithValue("issuer", request.Issuer);
            eligible.Parameters.AddWithValue("digest", request.SubjectSha256);
            if (await eligible.ExecuteScalarAsync(ct) is null)
                throw new InvalidOperationException(
                    "Synthetic opt-in requires an active scoped NEW session.");
        }

        // An identity-wide withdrawal in the same workspace is terminal:
        // switching to another OwnershipId must not make a new OPT_IN valid.
        if (request.Action == "OPT_IN")
        {
            await using var tombstone = new NpgsqlCommand("""
                SELECT EXISTS (
                    SELECT 1
                    FROM "AskAlphaVisitorOwnershipStaging" o
                    JOIN "AskAlphaVisitorConsentStaging" c
                      ON c."OwnershipId"=o."Id"
                    WHERE o."WorkspaceId"=@ws AND o."Issuer"=@issuer
                      AND o."SubjectSha256"=@digest AND c."Action"='REVOKE'
                )
                """, connection, tx);
            tombstone.Parameters.AddWithValue("ws", request.WorkspaceId);
            tombstone.Parameters.AddWithValue("issuer", request.Issuer);
            tombstone.Parameters.AddWithValue("digest", request.SubjectSha256);
            if ((bool)(await tombstone.ExecuteScalarAsync(ct) ?? false))
                throw new InvalidOperationException(
                    "Revoked synthetic identity cannot opt in under another ownership record.");
        }
        // Even a historically valid opt-in retry cannot reactivate consent.
        if (revoked && request.Action == "OPT_IN")
            throw new InvalidOperationException("Revoked staging opt-in cannot be replayed.");

        await using (var query = new NpgsqlCommand("""
            SELECT "Id","WorkspaceId","Issuer","SubjectSha256",
                   "Action","Purpose","NoticeVersion"
            FROM "AskAlphaVisitorConsentStaging"
            WHERE "OwnershipId"=@owner AND "RetryKey"=@retry
            """, connection, tx))
        {
            query.Parameters.AddWithValue("owner", request.OwnershipId);
            query.Parameters.AddWithValue("retry", request.RetryKey);
            await using var reader = await query.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                if (reader.GetGuid(1) != request.WorkspaceId ||
                    reader.GetString(2) != request.Issuer ||
                    reader.GetString(3).Trim() != request.SubjectSha256 ||
                    reader.GetString(4) != request.Action ||
                    reader.GetString(5) != request.Purpose ||
                    reader.GetString(6) != request.NoticeVersion)
                    throw new InvalidOperationException(
                        "Staging consent retry key is bound to another intent.");
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return Result(id, request.Action, true);
            }
        }

        // PostgreSQL assigns event time, not the request or browser clock.
        // Scope FK, one-opt-in/one-revoke, transition and append-only triggers
        // remain the authoritative guards in the disposable schema.
        var eventId = Guid.NewGuid();
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO "AskAlphaVisitorConsentStaging"
              ("Id","OwnershipId","WorkspaceId","Issuer","SubjectSha256",
               "Action","Purpose","NoticeVersion","RetryKey","RecordedAt")
            VALUES
              (@id,@owner,@ws,@issuer,@digest,@action,@purpose,@version,
               @retry,clock_timestamp())
            """, connection, tx))
        {
            insert.Parameters.AddWithValue("id", eventId);
            insert.Parameters.AddWithValue("owner", request.OwnershipId);
            insert.Parameters.AddWithValue("ws", request.WorkspaceId);
            insert.Parameters.AddWithValue("issuer", request.Issuer);
            insert.Parameters.AddWithValue("digest", request.SubjectSha256);
            insert.Parameters.AddWithValue("action", request.Action);
            insert.Parameters.AddWithValue("purpose", request.Purpose);
            insert.Parameters.AddWithValue("version", request.NoticeVersion);
            insert.Parameters.AddWithValue("retry", request.RetryKey);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Result(eventId, request.Action, false);
    }

    private static AskAlphaStagingConsentResult Result(
        Guid id, string action, bool replay) =>
        new(id, action == "REVOKE"
            ? "SYNTHETIC_STAGING_REVOCATION_RECORDED_NO_EXPORT"
            : "SYNTHETIC_STAGING_OPT_IN_RECORDED_UNVERIFIED", replay);

    private static void Validate(AskAlphaStagingConsentRequest request)
    {
        if (request is null || request.OwnershipId == Guid.Empty ||
            request.WorkspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Issuer) ||
            !Uri.TryCreate(request.Issuer, UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps || issuer.UserInfo.Length != 0 ||
            issuer.Query.Length != 0 || issuer.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(request.SubjectSha256) ||
            !Regex.IsMatch(request.SubjectSha256, @"\A[0-9A-F]{64}\z") ||
            request.Action is not ("OPT_IN" or "REVOKE") ||
            request.Purpose != Purpose ||
            request.NoticeVersion != NoticeVersion ||
            string.IsNullOrWhiteSpace(request.RetryKey) ||
            !Regex.IsMatch(request.RetryKey, @"\A[A-Za-z0-9_-]{8,100}\z"))
            throw new ArgumentException("Exact synthetic ownership, purpose, notice and retry scope required.");
    }
}
