using System.Security.Claims;
using System.Security.Cryptography;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class AskAlphaOwnershipFixtureTests
{
    private const string Issuer="https://issuer.fixture.invalid";
    private const string Subject="fixture-visitor-12345678";
    private static AskAlphaOwnershipFixtureScope Scope() => new(
        Guid.NewGuid(),Guid.NewGuid(),"fictional-creator-visitor",Issuer);
    private static BlissAuthenticationOptions Options() => new()
    {
        Enabled=true,AskAlphaVisitorIssuer=Issuer
    };
    private static ClaimsPrincipal Principal(Guid session,string issuer=Issuer,
        string subject=Subject,string role="bliss.ask_alpha_visitor")
    {
        var id=new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role,role,ClaimValueTypes.String,issuer),
            new Claim("sub",subject,ClaimValueTypes.String,issuer),
            new Claim("ask_alpha_session_id",session.ToString("D"),ClaimValueTypes.String,issuer)
        },"synthetic-oidc");
        return new ClaimsPrincipal(id);
    }
    [Fact]
    public void Signed_synthetic_creation_receipt_cannot_grant_real_ownership_consent_or_export()
    {
        var scope=Scope();var key=RandomNumberGenerator.GetBytes(32);
        var now=DateTimeOffset.UtcNow;
        var receipt=AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            scope,Subject,now,key);
        var reading=AskAlphaVisitorOwnershipPreflight.Inspect(
            Principal(scope.SessionId),Options(),scope,receipt,now.AddMinutes(1),key);
        Assert.Equal("SYNTHETIC_AT_CREATION_RECEIPT_MATCHES_HISTORY_UNVERIFIED",reading.Status);
        Assert.True(reading.ClaimsMatchIssuerAndSession);
        Assert.False(reading.HistoricalSessionOwnershipVerified);
        Assert.False(reading.VisitorConsentRecorded);
        Assert.False(reading.TranscriptLinked);
        Assert.False(reading.TranscriptExportAuthorized);
        Assert.False(reading.PaymentAuthorized);
        Assert.False(reading.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",reading.Delivery);
    }

    [Fact]
    public void Missing_signed_receipt_and_modified_scope_claim_key_expiry_revocation_fail_closed()
    {
        var s=Scope();var key=RandomNumberGenerator.GetBytes(32);
        var now=DateTimeOffset.UtcNow;
        var receipt=AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(s,Subject,now,key);
        AskAlphaVisitorIdentityReadiness Check(
            AskAlphaOwnershipFixtureScope scope,AskAlphaOwnershipFixtureReceipt? item,
            DateTimeOffset instant,byte[] secret,string subject=Subject,string issuer=Issuer) =>
                AskAlphaVisitorOwnershipPreflight.Inspect(
                    Principal(s.SessionId,issuer,subject),Options(),scope,item,instant,secret);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",Check(s,null,now,key).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s,receipt with { SignatureSha256=new string('F',64) },now,key).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s,receipt with { SubjectSha256=new string('A',64) },now,key).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s,receipt with { Scope=s with { WorkspaceId=Guid.NewGuid() } },now,key).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s with { SourceSlug="different-source" },receipt,now,key).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s,receipt,now.AddHours(1),key).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s,receipt with { RevokedAt=now.AddSeconds(10) },now.AddMinutes(1),key).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s,receipt,now,RandomNumberGenerator.GetBytes(32)).Status);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",
            Check(s,receipt,now,key,"fixture-visitor-87654321").Status);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            Check(s,receipt,now,key,Subject,"https://other-issuer.invalid").Status);
        Assert.Equal("STORED_SESSION_ISSUER_MISMATCH",
            Check(s with { Issuer="https://other-issuer.invalid" },receipt,now,key).Status);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            AskAlphaVisitorOwnershipPreflight.Inspect(Principal(Guid.NewGuid()),Options(),s,receipt,now,key).Status);
    }

    [Fact]
    public void Issuance_refuses_unbounded_or_untrusted_fixture_input()
    {
        var s=Scope(),key=RandomNumberGenerator.GetBytes(32),now=DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(()=>AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            s,"advertiser-owner",now,key));
        Assert.Throws<ArgumentException>(()=>AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            s,"fixture-visitor-12345678",now,RandomNumberGenerator.GetBytes(10)));
        Assert.Throws<ArgumentException>(()=>AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            s with { WorkspaceId=Guid.Empty },"fixture-visitor-12345678",now,key));
        Assert.Throws<ArgumentException>(()=>AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            s with { Issuer="http://example.test" },"fixture-visitor-12345678",now,key));
        Assert.Throws<ArgumentException>(()=>AskAlphaOwnershipFixtureSignature.SignSyntheticAtCreation(
            s with { SourceSlug="../source" },"fixture-visitor-12345678",now,key));
    }

    [FinancialPostgresFact]
    public async Task Isolated_PostgreSQL_enrollment_is_unique_revoke_is_append_only_and_revocation_blocks_ownership()
    {
        var cs=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;
        var builder=new NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("127.0.0.1",builder.Host);
        Assert.Equal("financial_test",builder.Database);
        await using var data=NpgsqlDataSource.Create(cs);
        var key=RandomNumberGenerator.GetBytes(32);
        var store=new AskAlphaOwnershipFixtureStore(data,key);
        var scope=Scope();
        var at=DateTimeOffset.UtcNow;
        var first=await store.EnrollSyntheticAtCreationAsync(scope,Subject,at);
        var saved=await store.ReadAsync(scope.SessionId);
        Assert.NotNull(saved);
        Assert.Equal(first.SignatureSha256,saved.SignatureSha256);
        Assert.True(AskAlphaOwnershipFixtureSignature.Verify(saved,scope,Subject,at.AddMinutes(1),key));
        var evidence=AskAlphaVisitorOwnershipPreflight.Inspect(
            Principal(scope.SessionId),Options(),scope,saved,at.AddMinutes(1),key);
        Assert.True(evidence.ClaimsMatchIssuerAndSession);
        Assert.False(evidence.HistoricalSessionOwnershipVerified);
        Assert.False(evidence.VisitorConsentRecorded);
        var duplicate=await Assert.ThrowsAsync<PostgresException>(()=>
            store.EnrollSyntheticAtCreationAsync(scope,Subject,at));
        Assert.Equal("23505",duplicate.SqlState);
        var foreign=await store.ReadAsync(Guid.NewGuid());
        Assert.Null(foreign);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>
            store.RevokeSyntheticAsync(scope.SessionId,"fixture-visitor-87654321",at.AddMinutes(2)));
        await store.RevokeSyntheticAsync(scope.SessionId,Subject,at.AddMinutes(2));
        var revoked=await store.ReadAsync(scope.SessionId);
        Assert.NotNull(revoked);
        Assert.NotNull(revoked.RevokedAt);
        Assert.False(AskAlphaOwnershipFixtureSignature.Verify(
            revoked,scope,Subject,at.AddMinutes(3),key));
        var denied=AskAlphaVisitorOwnershipPreflight.Inspect(
            Principal(scope.SessionId),Options(),scope,revoked,at.AddMinutes(3),key);
        Assert.Equal("NO_VALID_AT_CREATION_FIXTURE_RECEIPT",denied.Status);
        Assert.False(denied.TranscriptExportAuthorized);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>
            store.RevokeSyntheticAsync(scope.SessionId,Subject,at.AddMinutes(4)));
        await using var db=await data.OpenConnectionAsync();
        foreach(var sql in new[]{
            "UPDATE agm_ownership_fixture.receipts SET source_slug='rewritten' WHERE session_id=@session",
            "DELETE FROM agm_ownership_fixture.receipts WHERE session_id=@session",
            "TRUNCATE agm_ownership_fixture.receipts",
            "UPDATE agm_ownership_fixture.revocations SET reason_code='FIXTURE_CLEANUP' WHERE receipt_id=@receipt",
            "DELETE FROM agm_ownership_fixture.revocations WHERE receipt_id=@receipt",
            "TRUNCATE agm_ownership_fixture.revocations"
        })
        {
            await using var cmd=new NpgsqlCommand(sql,db);
            if(sql.Contains("@session"))cmd.Parameters.AddWithValue("session",scope.SessionId);
            if(sql.Contains("@receipt"))cmd.Parameters.AddWithValue("receipt",first.ReceiptId);
            await Assert.ThrowsAsync<PostgresException>(async()=>await cmd.ExecuteNonQueryAsync());
        }
    }

    [Fact]
    public void Hosted_database_or_short_key_cannot_construct_fixture_store()
    {
        using var prod=NpgsqlDataSource.Create("Host=localhost;Database=postgres;Username=test;Password=test");
        using var local=NpgsqlDataSource.Create("Host=127.0.0.1;Database=financial_test;Username=test;Password=test");
        Assert.Throws<InvalidOperationException>(()=>new AskAlphaOwnershipFixtureStore(
            prod,RandomNumberGenerator.GetBytes(32)));
        Assert.Throws<InvalidOperationException>(()=>new AskAlphaOwnershipFixtureStore(
            local,RandomNumberGenerator.GetBytes(8)));
    }
}
