using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;

namespace Bliss.Infrastructure.Persistence;

// AGM-LAUNCH-015 Stage B: synthetic enrollment commitment only.
// The fixture cannot retroactively prove ownership of historical Ask Alpha turns.
// There is no DI registration or HTTP route. Do NOT pass client-supplied secrets.
public sealed record AskAlphaOwnershipFixtureScope(
    Guid WorkspaceId, Guid SessionId, string SourceSlug, string Issuer);
public sealed record AskAlphaOwnershipFixtureReceipt(
    Guid ReceiptId, AskAlphaOwnershipFixtureScope Scope, string SubjectSha256,
    string SignatureSha256, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt = null);
public sealed record AskAlphaOwnershipFixtureReading(
    string Status, bool SignedFixtureRecordMatches = false,
    bool HistoricalSessionOwnershipVerified = false,
    bool RealVisitorAuthenticated = false, bool VisitorConsentRecorded = false,
    bool TranscriptLinked = false, bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false, bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public static class AskAlphaOwnershipFixtureSignature
{
    public static string SubjectDigest(string issuer, string subject)
    {
        var canonical = $"AGM-VISITOR-FIXTURE-SUBJECT/1|{issuer}|{subject}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static AskAlphaOwnershipFixtureReceipt SignSyntheticAtCreation(
        AskAlphaOwnershipFixtureScope scope, string syntheticSubject,
        DateTimeOffset now, ReadOnlySpan<byte> fixtureKey)
    {
        ValidateScope(scope);
        if (string.IsNullOrEmpty(syntheticSubject) ||
            !Regex.IsMatch(syntheticSubject, @"\Afixture-visitor-[a-z0-9-]{8,108}\z",
                RegexOptions.CultureInvariant) || fixtureKey.Length < 32 || fixtureKey.Length > 128)
            throw new ArgumentException("Only trusted synthetic visitor fixtures and strong fixture-only keys are accepted.");
        var record = new AskAlphaOwnershipFixtureReceipt(
            Guid.NewGuid(),scope,SubjectDigest(scope.Issuer,syntheticSubject),
            "",now.ToUniversalTime(),now.ToUniversalTime().AddHours(1));
        return record with { SignatureSha256 = Signature(record,fixtureKey) };
    }

    public static bool Verify(AskAlphaOwnershipFixtureReceipt? record,
        AskAlphaOwnershipFixtureScope expected, string syntheticSubject,
        DateTimeOffset now, ReadOnlySpan<byte> fixtureKey)
    {
        if (record is null || fixtureKey.Length < 32 || fixtureKey.Length > 128 ||
            record.ReceiptId == Guid.Empty || record.Scope != expected ||
            record.RevokedAt is not null || record.IssuedAt.Offset != TimeSpan.Zero ||
            record.ExpiresAt.Offset != TimeSpan.Zero ||
            record.IssuedAt > now.ToUniversalTime() ||
            record.ExpiresAt <= now.ToUniversalTime() ||
            record.ExpiresAt <= record.IssuedAt ||
            record.ExpiresAt > record.IssuedAt.AddHours(1) ||
            string.IsNullOrEmpty(record.SignatureSha256) ||
            !Regex.IsMatch(record.SignatureSha256, @"\A[0-9A-F]{64}\z") ||
            string.IsNullOrEmpty(record.SubjectSha256) ||
            !Regex.IsMatch(record.SubjectSha256, @"\A[0-9A-F]{64}\z"))
            return false;
        try { ValidateScope(expected); }
        catch (ArgumentException) { return false; }
        if (string.IsNullOrEmpty(syntheticSubject) ||
            !Regex.IsMatch(syntheticSubject, @"\Afixture-visitor-[a-z0-9-]{8,108}\z",
                RegexOptions.CultureInvariant))
            return false;
        var expectedSubject = SubjectDigest(expected.Issuer,syntheticSubject);
        var storedSubject = Convert.FromHexString(record.SubjectSha256);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expectedSubject), storedSubject))
            return false;
        var expectedSig = Signature(record,fixtureKey);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedSig), Convert.FromHexString(record.SignatureSha256));
    }

    private static string Signature(AskAlphaOwnershipFixtureReceipt r, ReadOnlySpan<byte> key)
    {
        var s = r.Scope;
        var canonical = string.Join("|", "AGM-SESSION-AT-CREATION-FIXTURE/1",
            r.ReceiptId.ToString("N"),s.WorkspaceId.ToString("N"),s.SessionId.ToString("N"),
            s.SourceSlug,s.Issuer,r.SubjectSha256,
            r.IssuedAt.ToUnixTimeMilliseconds(),r.ExpiresAt.ToUnixTimeMilliseconds());
        return Convert.ToHexString(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(canonical)));
    }

    public static void ValidateScope(AskAlphaOwnershipFixtureScope scope)
    {
        if (scope.WorkspaceId == Guid.Empty || scope.SessionId == Guid.Empty ||
            scope.SourceSlug is null || scope.SourceSlug.Length > 96 ||
            !Regex.IsMatch(scope.SourceSlug,@"\A[a-z0-9]+(?:-[a-z0-9]+)*\z",
                RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(scope.Issuer) || scope.Issuer.Length > 200 ||
            !Uri.TryCreate(scope.Issuer,UriKind.Absolute,out var url) ||
            url.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Exact workspace, session, source slug and HTTPS issuer required.");
    }
}

// This store is NOT resolved by Program.cs. It refuses all non-loopback databases.
// A synthetic test caller must explicitly provision its own isolated secret.
public sealed class AskAlphaOwnershipFixtureStore
{
    private readonly NpgsqlDataSource data;
    private readonly byte[] key;
    public AskAlphaOwnershipFixtureStore(NpgsqlDataSource source, byte[] fixtureKey)
    {
        var connection = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        if (connection.Host != "127.0.0.1" || connection.Database != "financial_test" ||
            fixtureKey is null || fixtureKey.Length < 32 || fixtureKey.Length > 128)
            throw new InvalidOperationException("Local financial_test plus a strong fixture key are required.");
        data = source;
        key = fixtureKey.ToArray();
    }

    public async Task<AskAlphaOwnershipFixtureReceipt> EnrollSyntheticAtCreationAsync(
        AskAlphaOwnershipFixtureScope scope, string syntheticSubject,
        DateTimeOffset now, CancellationToken ct=default)
    {
        var record = AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            scope,syntheticSubject,now,key);
        await using var connection=await data.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand("""
            INSERT INTO agm_ownership_fixture.receipts
              (receipt_id,workspace_id,session_id,source_slug,issuer,subject_sha256,
               signature_sha256,issued_at,expires_at)
            VALUES (@id,@workspace,@session,@slug,@issuer,@subject,@signature,@issued,@expires)
            """,connection);
        command.Parameters.AddWithValue("id",record.ReceiptId);
        command.Parameters.AddWithValue("workspace",scope.WorkspaceId);
        command.Parameters.AddWithValue("session",scope.SessionId);
        command.Parameters.AddWithValue("slug",scope.SourceSlug);
        command.Parameters.AddWithValue("issuer",scope.Issuer);
        command.Parameters.AddWithValue("subject",record.SubjectSha256);
        command.Parameters.AddWithValue("signature",record.SignatureSha256);
        command.Parameters.AddWithValue("issued",record.IssuedAt);
        command.Parameters.AddWithValue("expires",record.ExpiresAt);
        await command.ExecuteNonQueryAsync(ct);
        return record;
    }

    public async Task<AskAlphaOwnershipFixtureReceipt?> ReadAsync(Guid sessionId,
        CancellationToken ct=default)
    {
        await using var connection=await data.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand("""
            SELECT r.receipt_id,r.workspace_id,r.session_id,r.source_slug,r.issuer,
                   r.subject_sha256,r.signature_sha256,r.issued_at,r.expires_at,
                   v.revoked_at
            FROM agm_ownership_fixture.receipts r
            LEFT JOIN agm_ownership_fixture.revocations v ON v.receipt_id=r.receipt_id
            WHERE r.session_id=@session
            """,connection);
        command.Parameters.AddWithValue("session",sessionId);
        await using var reader=await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var scope=new AskAlphaOwnershipFixtureScope(reader.GetGuid(1),reader.GetGuid(2),
            reader.GetString(3),reader.GetString(4));
        return new AskAlphaOwnershipFixtureReceipt(reader.GetGuid(0),scope,
            reader.GetString(5).Trim(),reader.GetString(6).Trim(),
            reader.GetFieldValue<DateTimeOffset>(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.IsDBNull(9)?null:reader.GetFieldValue<DateTimeOffset>(9));
    }

    public async Task RevokeSyntheticAsync(Guid sessionId,string syntheticSubject,
        DateTimeOffset now,CancellationToken ct=default)
    {
        var record=await ReadAsync(sessionId,ct)
            ?? throw new InvalidOperationException("Unknown synthetic ownership receipt.");
        if (!AskAlphaOwnershipFixtureSignature.Verify(record,record.Scope,syntheticSubject,now,key))
            throw new InvalidOperationException("Missing, expired, tampered or already revoked synthetic receipt.");
        await using var connection=await data.OpenConnectionAsync(ct);
        await using var command=new NpgsqlCommand("""
            INSERT INTO agm_ownership_fixture.revocations
              (revocation_id,receipt_id,revoked_at,reason_code)
            VALUES (@id,@receipt,@at,'VISITOR_WITHDRAWAL')
            """,connection);
        command.Parameters.AddWithValue("id",Guid.NewGuid());
        command.Parameters.AddWithValue("receipt",record.ReceiptId);
        command.Parameters.AddWithValue("at",now);
        await command.ExecuteNonQueryAsync(ct);
    }
}
