using System.Text.RegularExpressions;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// AGM-COMM-015: application-table integration fixture; NOT real OIDC,
// a public enrollment API, a migration, or evidence of user consent.
// No hosted DI registration. Caller-supplied digest is synthetic test data.
public sealed record AskAlphaStagingEnrollmentRequest(
    Guid WorkspaceId, string Issuer, string SubjectSha256, string RequestKey);

public sealed record AskAlphaStagingEnrollmentResult(
    Guid SessionId, Guid OwnershipId, bool IsReplay = false,
    string Status = "SYNTHETIC_NEW_VISITOR_SESSION_HISTORY_UNVERIFIED",
    bool RealVisitorAuthenticated = false,
    bool VisitorConsentRecorded = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public sealed class AskAlphaVisitorStagingEnrollment
{
    private readonly NpgsqlDataSource data;

    public AskAlphaVisitorStagingEnrollment(NpgsqlDataSource source)
    {
        var cs = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        if (!((cs.Host == "127.0.0.1" && cs.Database == "planner_lineage_ci") ||
              (cs.Host == "localhost" && cs.Database == "bliss_ci")) ||
            string.IsNullOrWhiteSpace(cs.SearchPath) ||
            !Regex.IsMatch(cs.SearchPath, @"\Aagm_j_[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
            throw new InvalidOperationException(
                "Only a dedicated loopback planner test with a disposable schema is permitted.");
        data = source;
    }

    public async Task<AskAlphaStagingEnrollmentResult> CreateNewAsync(
        AskAlphaStagingEnrollmentRequest request, CancellationToken ct = default)
    {
        Validate(request);
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Serializes the exact synthetic identity/workspace/request-key scope.
        // Never accept a client-supplied session ID or use an existing session
        // with the same retry key as proof of ownership.
        await using (var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 5018))",
            connection, tx))
        {
            command.Parameters.AddWithValue("key", string.Join("|",
                request.WorkspaceId.ToString("N"), request.Issuer,
                request.SubjectSha256, request.RequestKey));
            await command.ExecuteNonQueryAsync(ct);
        }

        // Retry must resolve to an existing immutable ownership row and its
        // actual NEW synthetic planning session, never a staff-created row.
        await using (var query = new NpgsqlCommand("""
            SELECT o."Id",o."SessionId",s."SourceSystem",s."Status",
                   s."WorkspaceId",s."AdvertiserId",w."AdvertiserId",w."Status"
            FROM "AskAlphaVisitorOwnershipStaging" o
            JOIN "WeddingPlannerPlanningSessions" s ON s."Id"=o."SessionId"
            JOIN "WeddingPlannerWorkspaces" w ON w."Id"=s."WorkspaceId"
            WHERE o."WorkspaceId"=@ws AND o."Issuer"=@issuer
              AND o."SubjectSha256"=@digest AND o."RequestKey"=@retry
            """, connection, tx))
        {
            BindScope(query, request);
            await using var reader = await query.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var ownershipId = reader.GetGuid(0);
                var sessionId = reader.GetGuid(1);
                if (reader.GetString(2) != "STAGING_SYNTHETIC_VISITOR_NEW_SESSION" ||
                    reader.GetString(3) != "OPEN" ||
                    reader.GetGuid(4) != request.WorkspaceId ||
                    reader.GetGuid(5) != reader.GetGuid(6) ||
                    reader.GetString(7) != "ACTIVE")
                    throw new InvalidOperationException(
                        "Synthetic ownership replay is no longer valid.");
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return new AskAlphaStagingEnrollmentResult(sessionId, ownershipId, true);
            }
        }

        Guid advertiserId;
        await using (var scope = new NpgsqlCommand("""
            SELECT w."AdvertiserId"
            FROM "WeddingPlannerWorkspaces" w
            JOIN "Advertisers" a ON a."Id"=w."AdvertiserId"
            WHERE w."Id"=@ws AND w."Status"='ACTIVE'
            FOR SHARE OF w
            """, connection, tx))
        {
            scope.Parameters.AddWithValue("ws", request.WorkspaceId);
            var found = await scope.ExecuteScalarAsync(ct);
            if (found is not Guid validAdvertiser || validAdvertiser == Guid.Empty)
                throw new InvalidOperationException("Synthetic scoped workspace unavailable.");
            advertiserId = validAdvertiser;
        }

        // The planning session's idempotency key is randomized and generated
        // by the server fixture; the caller's key is stored only in immutable
        // visitor ownership. This avoids any cross-workspace adoption of staff
        // sessions whose idempotency keys are caller-controlled.
        var sessionIdNew = Guid.NewGuid();
        var ownershipIdNew = Guid.NewGuid();
        var created = DateTime.UtcNow;
        await using (var insertSession = new NpgsqlCommand("""
            INSERT INTO "WeddingPlannerPlanningSessions"
              ("Id","WorkspaceId","AdvertiserId","Status","SourceSystem",
               "IdempotencyKey","CreatedAt","UpdatedAt")
            VALUES (@session,@ws,@advertiser,'OPEN',
                    'STAGING_SYNTHETIC_VISITOR_NEW_SESSION',
                    @sessionKey,@created,@created)
            """, connection, tx))
        {
            insertSession.Parameters.AddWithValue("session", sessionIdNew);
            insertSession.Parameters.AddWithValue("ws", request.WorkspaceId);
            insertSession.Parameters.AddWithValue("advertiser", advertiserId);
            insertSession.Parameters.AddWithValue("sessionKey", Guid.NewGuid().ToString("N"));
            insertSession.Parameters.AddWithValue("created", created);
            await insertSession.ExecuteNonQueryAsync(ct);
        }
        await using (var insertOwner = new NpgsqlCommand("""
            INSERT INTO "AskAlphaVisitorOwnershipStaging"
              ("Id","WorkspaceId","SessionId","AdvertiserId",
               "Issuer","SubjectSha256","RequestKey","CreatedAt")
            VALUES (@owner,@ws,@session,@advertiser,
                    @issuer,@digest,@retry,@created)
            """, connection, tx))
        {
            insertOwner.Parameters.AddWithValue("owner", ownershipIdNew);
            insertOwner.Parameters.AddWithValue("ws", request.WorkspaceId);
            insertOwner.Parameters.AddWithValue("session", sessionIdNew);
            insertOwner.Parameters.AddWithValue("advertiser", advertiserId);
            insertOwner.Parameters.AddWithValue("issuer", request.Issuer);
            insertOwner.Parameters.AddWithValue("digest", request.SubjectSha256);
            insertOwner.Parameters.AddWithValue("retry", request.RequestKey);
            insertOwner.Parameters.AddWithValue("created", created);
            await insertOwner.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new AskAlphaStagingEnrollmentResult(sessionIdNew, ownershipIdNew);
    }

    private static void BindScope(NpgsqlCommand command,
        AskAlphaStagingEnrollmentRequest request)
    {
        command.Parameters.AddWithValue("ws", request.WorkspaceId);
        command.Parameters.AddWithValue("issuer", request.Issuer);
        command.Parameters.AddWithValue("digest", request.SubjectSha256);
        command.Parameters.AddWithValue("retry", request.RequestKey);
    }

    private static void Validate(AskAlphaStagingEnrollmentRequest request)
    {
        if (request is null || request.WorkspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.Issuer) ||
            request.Issuer.Length > 200 ||
            !Uri.TryCreate(request.Issuer, UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps || issuer.UserInfo.Length != 0 ||
            issuer.Query.Length != 0 || issuer.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(request.SubjectSha256) ||
            !Regex.IsMatch(request.SubjectSha256, @"\A[0-9A-F]{64}\z") ||
            string.IsNullOrWhiteSpace(request.RequestKey) ||
            !Regex.IsMatch(request.RequestKey, @"\A[A-Za-z0-9_-]{8,100}\z"))
            throw new ArgumentException(
                "Exact synthetic workspace, HTTPS issuer, digest and retry scope required.");
    }
}
