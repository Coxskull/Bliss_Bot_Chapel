using System.Text.RegularExpressions;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// Stage E, synthetic database fixture only: zero public routes / hosted DI.
// Caller assertion of synthetic identity is NOT a real OIDC token validation.
// Enrolls an entirely NEW server-generated session + signed receipt atomically.
public sealed record AskAlphaAtomicFixtureRequest(
    Guid WorkspaceId, string SourceSlug, string Issuer,
    string SyntheticSubject, string IdempotencyKey);
public sealed record AskAlphaAtomicFixtureResult(
    Guid SessionId, Guid ReceiptId, bool IsReplay,
    string Status = "SYNTHETIC_ATOMIC_ENROLLMENT_HISTORY_UNVERIFIED",
    bool RealVisitorAuthenticated = false,
    bool HistoricalSessionOwnershipVerified = false,
    bool VisitorConsentRecorded = false,
    bool TranscriptLinked = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

// The connection must point at the GitHub Actions loopback financial_test
// fixture with agm_ownership_fixture installed via explicit CI SQL.
public sealed class AskAlphaAtomicFixtureEnrollment
{
    private readonly NpgsqlDataSource data;
    private readonly byte[] key;
    public AskAlphaAtomicFixtureEnrollment(NpgsqlDataSource source, byte[] fixtureKey)
    {
        var connection = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        if (connection.Host != "127.0.0.1" || connection.Database != "financial_test" ||
            fixtureKey is null || fixtureKey.Length is < 32 or > 128)
            throw new InvalidOperationException("Only disposable loopback financial_test and strong fixture key permitted.");
        data=source;
        key=fixtureKey.ToArray();
    }

    public async Task<AskAlphaAtomicFixtureResult> EnrollAsync(
        AskAlphaAtomicFixtureRequest request, DateTimeOffset now,
        CancellationToken ct = default)
    {
        if (request.WorkspaceId == Guid.Empty ||
            string.IsNullOrEmpty(request.SyntheticSubject) ||
            string.IsNullOrEmpty(request.IdempotencyKey) ||
            request.IdempotencyKey.Length is < 8 or > 100 ||
            !Regex.IsMatch(request.IdempotencyKey,@"\A[A-Za-z0-9_-]{8,100}\z",
                RegexOptions.CultureInvariant))
            throw new ArgumentException("Exact synthetic workspace/subject and bounded retry key required.");
        // Stage B validates source + HTTPS issuer, subject fixture code and key.
        var candidateScope=new AskAlphaOwnershipFixtureScope(
            request.WorkspaceId, Guid.NewGuid(), request.SourceSlug,request.Issuer);
        var candidate=AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            candidateScope,request.SyntheticSubject,now,key);
        var digest=candidate.SubjectSha256;

        await using var connection=await data.OpenConnectionAsync(ct);
        await using var tx=await connection.BeginTransactionAsync(ct);
        // Serialize retries by identity+workspace+key; never lock on client
        // session UUID. PostgreSQL advisory lock is scoped to this transaction.
        await using(var lockCommand=new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@lock, 0))",connection,tx))
        {
            lockCommand.Parameters.AddWithValue("lock",string.Join("|",
                request.WorkspaceId.ToString("N"),request.Issuer,digest,request.IdempotencyKey));
            await lockCommand.ExecuteNonQueryAsync(ct);
        }
        await using(var select=new NpgsqlCommand("""
            SELECT s.session_id,s.receipt_id,s.source_slug,
                   r.signature_sha256,r.issued_at,r.expires_at,
                   CASE WHEN v.receipt_id IS NULL THEN false ELSE true END AS revoked
            FROM agm_ownership_fixture.new_sessions s
            JOIN agm_ownership_fixture.receipts r ON r.receipt_id=s.receipt_id
            LEFT JOIN agm_ownership_fixture.revocations v ON v.receipt_id=s.receipt_id
            WHERE s.workspace_id=@workspace AND s.issuer=@issuer
              AND s.subject_sha256=@subject AND s.idempotency_key=@retry
            """,connection,tx))
        {
            select.Parameters.AddWithValue("workspace",request.WorkspaceId);
            select.Parameters.AddWithValue("issuer",request.Issuer);
            select.Parameters.AddWithValue("subject",digest);
            select.Parameters.AddWithValue("retry",request.IdempotencyKey);
            await using var reader=await select.ExecuteReaderAsync(ct);
            if(await reader.ReadAsync(ct))
            {
                var sessionId=reader.GetGuid(0);
                var receiptId=reader.GetGuid(1);
                var sourceSlug=reader.GetString(2);
                var signature=reader.GetString(3).Trim();
                var issued=reader.GetFieldValue<DateTimeOffset>(4);
                var expires=reader.GetFieldValue<DateTimeOffset>(5);
                var revoked=reader.GetBoolean(6);
                await reader.DisposeAsync();
                var existingScope=candidateScope with { SessionId=sessionId };
                var existing=candidate with {
                    ReceiptId=receiptId, Scope=existingScope,
                    SignatureSha256=signature, IssuedAt=issued, ExpiresAt=expires,
                    RevokedAt=revoked ? now : null
                };
                if(sourceSlug != request.SourceSlug ||
                    !AskAlphaOwnershipFixtureSignature.Verify(
                        existing,existingScope,request.SyntheticSubject,now,key))
                    throw new InvalidOperationException("Retry scope changed or synthetic enrollment expired/revoked.");
                await tx.CommitAsync(ct);
                return new AskAlphaAtomicFixtureResult(sessionId,receiptId,true);
            }
        }
        // Two inserts, ONE transaction: any error rolls back the receipt and
        // the new session together. SessionId is server generated, never input.
        await using(var receipt=new NpgsqlCommand("""
            INSERT INTO agm_ownership_fixture.receipts
            (receipt_id,workspace_id,session_id,source_slug,issuer,
             subject_sha256,signature_sha256,issued_at,expires_at)
            VALUES (@id,@workspace,@session,@slug,@issuer,
                    @subject,@signature,@issued,@expires)
            """,connection,tx))
        {
            receipt.Parameters.AddWithValue("id",candidate.ReceiptId);
            receipt.Parameters.AddWithValue("workspace",candidate.Scope.WorkspaceId);
            receipt.Parameters.AddWithValue("session",candidate.Scope.SessionId);
            receipt.Parameters.AddWithValue("slug",candidate.Scope.SourceSlug);
            receipt.Parameters.AddWithValue("issuer",candidate.Scope.Issuer);
            receipt.Parameters.AddWithValue("subject",candidate.SubjectSha256);
            receipt.Parameters.AddWithValue("signature",candidate.SignatureSha256);
            receipt.Parameters.AddWithValue("issued",candidate.IssuedAt);
            receipt.Parameters.AddWithValue("expires",candidate.ExpiresAt);
            await receipt.ExecuteNonQueryAsync(ct);
        }
        await using(var session=new NpgsqlCommand("""
            INSERT INTO agm_ownership_fixture.new_sessions
            (session_id,receipt_id,workspace_id,issuer,subject_sha256,
             source_slug,idempotency_key,created_at)
            VALUES (@session,@receipt,@workspace,@issuer,@subject,
                    @slug,@retry,@created)
            """,connection,tx))
        {
            session.Parameters.AddWithValue("session",candidate.Scope.SessionId);
            session.Parameters.AddWithValue("receipt",candidate.ReceiptId);
            session.Parameters.AddWithValue("workspace",candidate.Scope.WorkspaceId);
            session.Parameters.AddWithValue("issuer",candidate.Scope.Issuer);
            session.Parameters.AddWithValue("subject",candidate.SubjectSha256);
            session.Parameters.AddWithValue("slug",candidate.Scope.SourceSlug);
            session.Parameters.AddWithValue("retry",request.IdempotencyKey);
            session.Parameters.AddWithValue("created",candidate.IssuedAt);
            await session.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new AskAlphaAtomicFixtureResult(candidate.Scope.SessionId,candidate.ReceiptId,false);
    }
}
