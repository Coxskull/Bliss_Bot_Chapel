using System.Text.RegularExpressions;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// AGM-COMM-015: read-only SYNTHETIC consent scope inspection. This does NOT
// authenticate an OIDC principal, grant actual consent, or allow transcript
// release. It is deliberately NOT registered in application DI.
public sealed record AskAlphaStagingConsentScope(
    Guid OwnershipId, Guid WorkspaceId, string Issuer, string SubjectSha256,
    string Purpose, string NoticeVersion);

public sealed record AskAlphaStagingConsentSnapshot(
    string Status, bool SyntheticOptInSeen = false, bool SyntheticWithdrawn = false,
    bool RealVisitorAuthenticated = false, bool AuthenticatedConsentRecorded = false,
    bool TranscriptExportAuthorized = false, bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false, bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public sealed class AskAlphaVisitorStagingConsentReadback
{
    private readonly NpgsqlDataSource data;

    public AskAlphaVisitorStagingConsentReadback(NpgsqlDataSource source)
    {
        var cs = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        if (!((cs.Host == "127.0.0.1" && cs.Database == "planner_lineage_ci") ||
              (cs.Host == "localhost" && cs.Database == "bliss_ci")) ||
            string.IsNullOrWhiteSpace(cs.SearchPath) ||
            !Regex.IsMatch(cs.SearchPath, @"\Aagm_j_[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
            throw new InvalidOperationException(
                "Readback requires a dedicated loopback database and disposable planner test schema.");
        data = source;
    }

    public async Task<AskAlphaStagingConsentSnapshot> ReadAsync(
        AskAlphaStagingConsentScope scope, CancellationToken ct = default)
    {
        Validate(scope);
        await using var connection = await data.OpenConnectionAsync(ct);
        // One SQL statement sees one PostgreSQL MVCC snapshot. The caller
        // cannot supply a session or advertiser ID as proof of ownership.
        await using var cmd = new NpgsqlCommand("""
            SELECT
              EXISTS (
                SELECT 1 FROM "AskAlphaVisitorConsentStaging" c
                WHERE c."OwnershipId"=o."Id"
                  AND c."WorkspaceId"=o."WorkspaceId"
                  AND c."Issuer"=o."Issuer"
                  AND c."SubjectSha256"=o."SubjectSha256"
                  AND c."Action"='OPT_IN'
                  AND c."Purpose"=@purpose AND c."NoticeVersion"=@version
              ),
              EXISTS (
                SELECT 1 FROM "AskAlphaVisitorConsentStaging" c
                WHERE c."OwnershipId"=o."Id"
                  AND c."WorkspaceId"=o."WorkspaceId"
                  AND c."Issuer"=o."Issuer"
                  AND c."SubjectSha256"=o."SubjectSha256"
                  AND c."Action"='REVOKE'
                  AND c."Purpose"=@purpose AND c."NoticeVersion"=@version
              )
            FROM "AskAlphaVisitorOwnershipStaging" o
            JOIN "WeddingPlannerPlanningSessions" s ON s."Id"=o."SessionId"
            JOIN "WeddingPlannerWorkspaces" w ON w."Id"=o."WorkspaceId"
            WHERE o."Id"=@owner AND o."WorkspaceId"=@workspace
              AND o."Issuer"=@issuer AND o."SubjectSha256"=@digest
              AND s."WorkspaceId"=o."WorkspaceId"
              AND s."AdvertiserId"=o."AdvertiserId"
              AND s."SourceSystem"='STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
              AND s."Status"='OPEN'
              AND w."Status"='ACTIVE'
              AND w."AdvertiserId"=o."AdvertiserId"
            """, connection);
        cmd.Parameters.AddWithValue("owner", scope.OwnershipId);
        cmd.Parameters.AddWithValue("workspace", scope.WorkspaceId);
        cmd.Parameters.AddWithValue("issuer", scope.Issuer);
        cmd.Parameters.AddWithValue("digest", scope.SubjectSha256);
        cmd.Parameters.AddWithValue("purpose", scope.Purpose);
        cmd.Parameters.AddWithValue("version", scope.NoticeVersion);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new AskAlphaStagingConsentSnapshot("SYNTHETIC_STAGING_SCOPE_NOT_AUTHORIZED");
        var optedIn = reader.GetBoolean(0);
        var withdrawn = reader.GetBoolean(1);
        // Even opt-in stays UNVERIFIED and has zero downstream authority.
        return new AskAlphaStagingConsentSnapshot(
            withdrawn ? "SYNTHETIC_STAGING_WITHDRAWN_NO_EXPORT" :
            optedIn ? "SYNTHETIC_STAGING_OPT_IN_UNVERIFIED_NO_EXPORT" :
            "SYNTHETIC_STAGING_NO_OPT_IN",
            SyntheticOptInSeen: optedIn,
            SyntheticWithdrawn: withdrawn);
    }

    private static void Validate(AskAlphaStagingConsentScope scope)
    {
        if (scope is null || scope.OwnershipId == Guid.Empty ||
            scope.WorkspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(scope.Issuer) || scope.Issuer.Length > 200 ||
            !Uri.TryCreate(scope.Issuer, UriKind.Absolute, out var issuer) ||
            issuer.Scheme != Uri.UriSchemeHttps || issuer.UserInfo.Length != 0 ||
            issuer.Query.Length != 0 || issuer.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(scope.SubjectSha256) ||
            !Regex.IsMatch(scope.SubjectSha256, @"\A[0-9A-F]{64}\z") ||
            scope.Purpose != AskAlphaVisitorStagingConsentLedger.Purpose ||
            scope.NoticeVersion != AskAlphaVisitorStagingConsentLedger.NoticeVersion)
            throw new ArgumentException("Exact synthetic ownership, notice and purpose required.");
    }
}
