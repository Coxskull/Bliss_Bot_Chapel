using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// AGM-COMM-015 - disposable synthetic PostgreSQL ONLY.
// This is not a real OIDC, consent, retention, export, or production service.
// No route, DI registration, real transcript lookup, or database migration.
public sealed record AskAlphaStagingSnapshotRequest(
    Guid OwnershipId, Guid WorkspaceId, string Issuer, string SubjectSha256,
    string Purpose, string NoticeVersion, string RetryKey,
    byte[] FictionalContent, byte[] EphemeralEncryptionKey);

public sealed record AskAlphaStagingSnapshotScope(
    Guid OwnershipId, Guid WorkspaceId, string Issuer, string SubjectSha256,
    string Purpose, string NoticeVersion);

public sealed record AskAlphaStagingSnapshotDecision(
    Guid SnapshotId, string Status, bool IsReplay = false,
    bool RealVisitorAuthenticated = false,
    bool AuthenticatedConsentRecorded = false,
    bool ContentReadAuthorized = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public sealed class AskAlphaVisitorPrivateSnapshotStaging
{
    private readonly NpgsqlDataSource data;
    private const int MaxFictionalBytes = 65536;

    public AskAlphaVisitorPrivateSnapshotStaging(NpgsqlDataSource source)
    {
        var cs = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        if (!((cs.Host == "127.0.0.1" && cs.Database == "planner_lineage_ci") ||
              (cs.Host == "localhost" && cs.Database == "bliss_ci")) ||
            string.IsNullOrWhiteSpace(cs.SearchPath) ||
            !Regex.IsMatch(cs.SearchPath, @"\Aagm_j_[0-9a-f]{32}\z",
                RegexOptions.CultureInvariant))
            throw new InvalidOperationException(
                "Private snapshot fixture requires a disposable loopback PostgreSQL test schema.");
        data = source;
    }

    public async Task<AskAlphaStagingSnapshotDecision> SaveFictionalAsync(
        AskAlphaStagingSnapshotRequest request, CancellationToken ct = default)
    {
        ValidateScope(request);
        if (request.FictionalContent is not { Length: > 0 and <= MaxFictionalBytes } ||
            request.EphemeralEncryptionKey is not { Length: 32 } ||
            string.IsNullOrWhiteSpace(request.RetryKey) ||
            !Regex.IsMatch(request.RetryKey, @"\A[A-Za-z0-9_-]{8,100}\z"))
            throw new ArgumentException(
                "Bounded fictional content, 256-bit ephemeral key and retry key required.");

        var digest = Convert.ToHexString(SHA256.HashData(request.FictionalContent));
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        // Same advisory locks and ordering as the staging enrollment and
        // consent ledger: withdrawal and snapshot creation cannot race.
        await using (var guard = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@identity, 5018))",
            connection, tx))
        {
            guard.Parameters.AddWithValue("identity", string.Join("|",
                request.WorkspaceId.ToString("N"), request.Issuer, request.SubjectSha256));
            await guard.ExecuteNonQueryAsync(ct);
        }
        await using (var guard = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@owner, 5017))",
            connection, tx))
        {
            guard.Parameters.AddWithValue("owner", request.OwnershipId.ToString("D"));
            await guard.ExecuteNonQueryAsync(ct);
        }

        await using (var withdrawn = new NpgsqlCommand("""
            SELECT EXISTS(
                SELECT 1 FROM "AskAlphaVisitorOwnershipStaging" prior
                JOIN "AskAlphaVisitorConsentStaging" c
                  ON c."OwnershipId"=prior."Id"
                WHERE prior."WorkspaceId"=@workspace
                  AND prior."Issuer"=@issuer
                  AND prior."SubjectSha256"=@subject
                  AND c."Action"='REVOKE')
            """, connection, tx))
        {
            withdrawn.Parameters.AddWithValue("workspace", request.WorkspaceId);
            withdrawn.Parameters.AddWithValue("issuer", request.Issuer);
            withdrawn.Parameters.AddWithValue("subject", request.SubjectSha256);
            if ((bool)(await withdrawn.ExecuteScalarAsync(ct) ?? false))
                throw new InvalidOperationException(
                    "Synthetic withdrawal permanently denies snapshot storage and replay.");
        }

        // Exact replay must also require an OPEN synthetic session and
        // ACTIVE workspace. The database trigger checks these on INSERT
        // but never runs for an already-stored snapshot replay. Lock both
        // authoritative fixture rows to prevent a concurrent close after
        // this check and before the transaction commits.
        // This DOES NOT establish real signed OIDC visitor ownership.
        await using (var eligibility = new NpgsqlCommand("""
            SELECT 1 FROM "AskAlphaVisitorOwnershipStaging" o
            JOIN "WeddingPlannerPlanningSessions" s ON s."Id"=o."SessionId"
            JOIN "WeddingPlannerWorkspaces" w ON w."Id"=o."WorkspaceId"
            WHERE o."Id"=@owner AND o."WorkspaceId"=@workspace
              AND o."Issuer"=@issuer AND o."SubjectSha256"=@subject
              AND s."WorkspaceId"=o."WorkspaceId"
              AND s."AdvertiserId"=o."AdvertiserId"
              AND s."SourceSystem"='STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
              AND s."Status"='OPEN'
              AND w."Status"='ACTIVE'
              AND w."AdvertiserId"=o."AdvertiserId"
            FOR SHARE OF s,w
            """, connection, tx))
        {
            eligibility.Parameters.AddWithValue("owner", request.OwnershipId);
            eligibility.Parameters.AddWithValue("workspace", request.WorkspaceId);
            eligibility.Parameters.AddWithValue("issuer", request.Issuer);
            eligibility.Parameters.AddWithValue("subject", request.SubjectSha256);
            if (await eligibility.ExecuteScalarAsync(ct) is null)
                throw new InvalidOperationException(
                    "Synthetic snapshot scope or active planning session unavailable.");
        }

        // Exact idempotency, including the content digest and all scope fields.
        await using (var replay = new NpgsqlCommand("""
            SELECT "Id","WorkspaceId","Issuer","SubjectSha256","Purpose",
                   "NoticeVersion","ContentSha256"
            FROM "AskAlphaVisitorSnapshotStaging"
            WHERE "OwnershipId"=@owner AND "RetryKey"=@retry
            """, connection, tx))
        {
            replay.Parameters.AddWithValue("owner", request.OwnershipId);
            replay.Parameters.AddWithValue("retry", request.RetryKey);
            await using var reader = await replay.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                if (reader.GetGuid(1) != request.WorkspaceId ||
                    reader.GetString(2) != request.Issuer ||
                    reader.GetString(3).Trim() != request.SubjectSha256 ||
                    reader.GetString(4) != request.Purpose ||
                    reader.GetString(5) != request.NoticeVersion ||
                    reader.GetString(6).Trim() != digest)
                    throw new InvalidOperationException(
                        "Synthetic snapshot retry key is bound to different content or scope.");
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return new AskAlphaStagingSnapshotDecision(
                    id, "SYNTHETIC_ENCRYPTED_SNAPSHOT_REPLAY_NO_READ", IsReplay: true);
            }
        }

        // AES-GCM ciphertext only; caller retains key outside the database.
        // No read/decrypt API exists and plaintext is never returned.
        var snapshotId = Guid.NewGuid();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[request.FictionalContent.Length];
        var aad = Encoding.UTF8.GetBytes(string.Join("|",
            snapshotId.ToString("N"), request.OwnershipId.ToString("N"),
            request.WorkspaceId.ToString("N"), request.Issuer, request.SubjectSha256,
            request.Purpose, request.NoticeVersion, request.RetryKey, digest));
        using (var aes = new AesGcm(request.EphemeralEncryptionKey, tag.Length))
            aes.Encrypt(nonce, request.FictionalContent, ciphertext, tag, aad);

        // The SQL trigger independently proves the consent opt-in and
        // immutable new-session scope. A caller-supplied ownership ID is NOT
        // enough; a missing/revoked opt-in fails closed in PostgreSQL.
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO "AskAlphaVisitorSnapshotStaging"
              ("Id","OwnershipId","WorkspaceId","Issuer","SubjectSha256",
               "Purpose","NoticeVersion","RetryKey","ContentSha256",
               "Ciphertext","Nonce","Tag","CreatedAt")
            VALUES
              (@id,@owner,@ws,@issuer,@digest,@purpose,@version,@retry,
               @contentDigest,@ciphertext,@nonce,@tag,clock_timestamp())
            """, connection, tx))
        {
            insert.Parameters.AddWithValue("id", snapshotId);
            insert.Parameters.AddWithValue("owner", request.OwnershipId);
            insert.Parameters.AddWithValue("ws", request.WorkspaceId);
            insert.Parameters.AddWithValue("issuer", request.Issuer);
            insert.Parameters.AddWithValue("digest", request.SubjectSha256);
            insert.Parameters.AddWithValue("purpose", request.Purpose);
            insert.Parameters.AddWithValue("version", request.NoticeVersion);
            insert.Parameters.AddWithValue("retry", request.RetryKey);
            insert.Parameters.AddWithValue("contentDigest", digest);
            insert.Parameters.AddWithValue("ciphertext", ciphertext);
            insert.Parameters.AddWithValue("nonce", nonce);
            insert.Parameters.AddWithValue("tag", tag);
            await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new AskAlphaStagingSnapshotDecision(
            snapshotId, "SYNTHETIC_ENCRYPTED_SNAPSHOT_STORED_NO_READ");
    }

    // Metadata-only: no ciphertext, plaintext, digest or identity data leave
    // the service. After revocation any snapshot is permanently denied.
    public async Task<AskAlphaStagingSnapshotDecision> InspectAsync(
        AskAlphaStagingSnapshotScope scope, CancellationToken ct = default)
    {
        ValidateScope(scope);
        await using var connection = await data.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT s."Id", EXISTS(
                SELECT 1 FROM "AskAlphaVisitorOwnershipStaging" prior
                JOIN "AskAlphaVisitorConsentStaging" c
                  ON c."OwnershipId"=prior."Id"
                WHERE prior."WorkspaceId"=s."WorkspaceId"
                  AND prior."Issuer"=s."Issuer"
                  AND prior."SubjectSha256"=s."SubjectSha256"
                  AND c."Action"='REVOKE')
            FROM "AskAlphaVisitorSnapshotStaging" s
            JOIN "AskAlphaVisitorOwnershipStaging" o ON o."Id"=s."OwnershipId"
            JOIN "WeddingPlannerPlanningSessions" ps ON ps."Id"=o."SessionId"
            JOIN "WeddingPlannerWorkspaces" w ON w."Id"=o."WorkspaceId"
            WHERE s."OwnershipId"=@owner AND s."WorkspaceId"=@ws
              AND s."Issuer"=@issuer AND s."SubjectSha256"=@digest
              AND s."Purpose"=@purpose AND s."NoticeVersion"=@version
              AND ps."WorkspaceId"=o."WorkspaceId"
              AND ps."AdvertiserId"=o."AdvertiserId"
              AND ps."SourceSystem"='STAGING_SYNTHETIC_VISITOR_NEW_SESSION'
              AND ps."Status"='OPEN' AND w."Status"='ACTIVE'
              AND w."AdvertiserId"=o."AdvertiserId"
            """, connection);
        cmd.Parameters.AddWithValue("owner", scope.OwnershipId);
        cmd.Parameters.AddWithValue("ws", scope.WorkspaceId);
        cmd.Parameters.AddWithValue("issuer", scope.Issuer);
        cmd.Parameters.AddWithValue("digest", scope.SubjectSha256);
        cmd.Parameters.AddWithValue("purpose", scope.Purpose);
        cmd.Parameters.AddWithValue("version", scope.NoticeVersion);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return new AskAlphaStagingSnapshotDecision(
                Guid.Empty, "SYNTHETIC_SNAPSHOT_SCOPE_DENIED");
        var id = reader.GetGuid(0);
        return new AskAlphaStagingSnapshotDecision(
            id, reader.GetBoolean(1)
                ? "SYNTHETIC_WITHDRAWN_SNAPSHOT_PERMANENTLY_DENIED"
                : "SYNTHETIC_ENCRYPTED_SNAPSHOT_PRESENT_NO_READ");
    }

    private static void ValidateScope(AskAlphaStagingSnapshotScope scope)
    {
        if (scope is null || scope.OwnershipId == Guid.Empty ||
            scope.WorkspaceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(scope.Issuer) || scope.Issuer.Length > 200 ||
            !Uri.TryCreate(scope.Issuer, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            string.IsNullOrWhiteSpace(scope.SubjectSha256) ||
            !Regex.IsMatch(scope.SubjectSha256, @"\A[0-9A-F]{64}\z") ||
            scope.Purpose != AskAlphaVisitorStagingConsentLedger.Purpose ||
            scope.NoticeVersion != AskAlphaVisitorStagingConsentLedger.NoticeVersion)
            throw new ArgumentException("Exact synthetic ownership and consent scope required.");
    }

    private static void ValidateScope(AskAlphaStagingSnapshotRequest request) =>
        ValidateScope(new AskAlphaStagingSnapshotScope(
            request.OwnershipId, request.WorkspaceId, request.Issuer,
            request.SubjectSha256, request.Purpose, request.NoticeVersion));
}
