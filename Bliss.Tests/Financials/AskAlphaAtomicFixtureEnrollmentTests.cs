using System.Security.Claims;
using System.Security.Cryptography;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class AskAlphaAtomicFixtureEnrollmentTests
{
    private const string Issuer = "https://issuer.fixture.invalid";
    private const string Subject = "fixture-visitor-12345678";
    private static ClaimsPrincipal Principal(string role="bliss.ask_alpha_visitor",bool previous=false)
    {
        var id=new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role,role,ClaimValueTypes.String,Issuer),
            new Claim("sub",Subject,ClaimValueTypes.String,Issuer)
        },"signed-synthetic-cookie");
        if(previous)
            id.AddClaim(new Claim("ask_alpha_session_id",Guid.NewGuid().ToString("D")));
        return new ClaimsPrincipal(id);
    }
    private static BlissAuthenticationOptions Config(bool allow=true) => new()
    {
        Enabled=true,ClientId="fixture-client",Authority=Issuer,
        AskAlphaVisitorIssuer=Issuer,AskAlphaVisitorEnrollmentRequested=allow
    };
    private static AskAlphaAtomicFixtureRequest Request(Guid workspace) =>
        new(workspace,"fictional-campaign",Issuer,Subject,"fixture_req_12345");

    [Fact]
    public void Constructor_refuses_nonfixture_and_short_secret()
    {
        using var live=NpgsqlDataSource.Create(
            "Host=localhost;Database=postgres;Username=test;Password=test");
        using var fixture=NpgsqlDataSource.Create(
            "Host=127.0.0.1;Database=financial_test;Username=test;Password=test");
        Assert.Throws<InvalidOperationException>(()=>new AskAlphaAtomicFixtureEnrollment(
            live,RandomNumberGenerator.GetBytes(32)));
        Assert.Throws<InvalidOperationException>(()=>new AskAlphaAtomicFixtureEnrollment(
            fixture,RandomNumberGenerator.GetBytes(8)));
    }

    [Fact]
    public async Task Disabled_enrollment_and_privileged_or_historic_claims_never_open_database()
    {
        using var source=NpgsqlDataSource.Create(
            "Host=127.0.0.1;Database=financial_test;Username=test;Password=test");
        var fixture=new AskAlphaAtomicFixtureEnrollment(source,RandomNumberGenerator.GetBytes(32));
        var when=DateTimeOffset.UtcNow;
        var workspace=Guid.NewGuid();
        foreach(var (actor,config) in new[] {
            (Principal(),Config(false)),
            (Principal("bliss.advertiser"),Config()),
            (Principal("bliss.admin"),Config()),
            (Principal(previous:true),Config())
        })
        {
            var gate=new AskAlphaAtomicFixtureEnrollmentGate(fixture,config);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>
                gate.EnrollAsync(actor,workspace,"fictional-campaign","fixture_req_12345",when));
        }
    }

    [FinancialPostgresFact]
    public async Task Atomic_new_session_and_receipt_are_one_transaction_with_serialized_idempotency_and_revocation()
    {
        var cs=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;
        var builder=new NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("127.0.0.1",builder.Host);
        Assert.Equal("financial_test",builder.Database);
        await using var data=NpgsqlDataSource.Create(cs);
        var secret=RandomNumberGenerator.GetBytes(32);
        var fixture=new AskAlphaAtomicFixtureEnrollment(data,secret);
        var gate=new AskAlphaAtomicFixtureEnrollmentGate(fixture,Config());
        var workspace=Guid.NewGuid();
        var now=DateTimeOffset.UtcNow;
        var starts=await Task.WhenAll(Enumerable.Range(0,8)
            .Select(_=>gate.EnrollAsync(Principal(),workspace,
                "fictional-campaign","fixture_req_12345",now)));
        var first=starts[0];
        Assert.Single(starts.Select(x=>x.SessionId).Distinct());
        Assert.Single(starts.Select(x=>x.ReceiptId).Distinct());
        Assert.Equal(1,starts.Count(x=>!x.IsReplay));
        Assert.Equal(7,starts.Count(x=>x.IsReplay));
        Assert.NotEqual(Guid.Empty,first.SessionId);
        Assert.NotEqual(Guid.Empty,first.ReceiptId);
        Assert.Equal("SYNTHETIC_ATOMIC_ENROLLMENT_HISTORY_UNVERIFIED",first.Status);
        Assert.False(first.RealVisitorAuthenticated);
        Assert.False(first.HistoricalSessionOwnershipVerified);
        Assert.False(first.VisitorConsentRecorded);
        Assert.False(first.TranscriptLinked);
        Assert.False(first.TranscriptExportAuthorized);
        Assert.False(first.PaymentAuthorized);
        Assert.False(first.CreatorPayoutAuthorized);
        Assert.False(first.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",first.Delivery);

        var receipts=new AskAlphaOwnershipFixtureStore(data,secret);
        var stored=await receipts.ReadAsync(first.SessionId);
        Assert.NotNull(stored);
        Assert.Equal(first.ReceiptId,stored.ReceiptId);
        Assert.True(AskAlphaOwnershipFixtureSignature.Verify(stored,
            new AskAlphaOwnershipFixtureScope(workspace,first.SessionId,
                "fictional-campaign",Issuer),Subject,now.AddMinutes(1),secret));
        await using(var db=await data.OpenConnectionAsync())
        {
            await using var cmd=new NpgsqlCommand("""
                SELECT count(*)
                FROM agm_ownership_fixture.new_sessions s
                JOIN agm_ownership_fixture.receipts r
                   ON r.receipt_id=s.receipt_id AND r.session_id=s.session_id
                WHERE s.workspace_id=@workspace AND s.idempotency_key=@retry
                """,db);
            cmd.Parameters.AddWithValue("workspace",workspace);
            cmd.Parameters.AddWithValue("retry","fixture_req_12345");
            Assert.Equal(1L,await cmd.ExecuteScalarAsync());
        }

        var mismatch=await Assert.ThrowsAsync<InvalidOperationException>(()=>
            gate.EnrollAsync(Principal(),workspace,"another-campaign","fixture_req_12345",now));
        Assert.Contains("Retry",mismatch.Message);
        var fresh=await gate.EnrollAsync(Principal(),workspace,
            "fictional-campaign","fixture_next_12345",now);
        Assert.NotEqual(first.SessionId,fresh.SessionId);
        Assert.NotEqual(first.ReceiptId,fresh.ReceiptId);
        var otherWorkspace=await gate.EnrollAsync(Principal(),Guid.NewGuid(),
            "fictional-campaign","fixture_req_12345",now);
        Assert.NotEqual(first.SessionId,otherWorkspace.SessionId);

        await receipts.RevokeSyntheticAsync(first.SessionId,Subject,now.AddMinutes(1));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>
            gate.EnrollAsync(Principal(),workspace,"fictional-campaign",
                "fixture_req_12345",now.AddMinutes(2)));

        await using var verify=await data.OpenConnectionAsync();
        foreach(var sql in new[] {
            "UPDATE agm_ownership_fixture.new_sessions SET source_slug='modified' WHERE session_id=@id",
            "DELETE FROM agm_ownership_fixture.new_sessions WHERE session_id=@id",
            "TRUNCATE agm_ownership_fixture.new_sessions"
        })
        {
            await using var cmd=new NpgsqlCommand(sql,verify);
            if(sql.Contains("@id"))cmd.Parameters.AddWithValue("id",first.SessionId);
            await Assert.ThrowsAsync<PostgresException>(async()=>await cmd.ExecuteNonQueryAsync());
        }
    }

    [FinancialPostgresFact]
    public async Task Invalid_inputs_do_not_leave_partial_or_forged_receipts()
    {
        var cs=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;
        await using var source=NpgsqlDataSource.Create(cs);
        var fixture=new AskAlphaAtomicFixtureEnrollment(source,RandomNumberGenerator.GetBytes(32));
        var scope=Guid.NewGuid();
        var now=DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<ArgumentException>(()=>
            fixture.EnrollAsync(Request(scope) with { IdempotencyKey="../invalid" },now));
        await Assert.ThrowsAsync<ArgumentException>(()=>
            fixture.EnrollAsync(Request(scope) with { SyntheticSubject="advertiser-owner" },now));
        await Assert.ThrowsAsync<ArgumentException>(()=>
            fixture.EnrollAsync(Request(scope) with { WorkspaceId=Guid.Empty },now));
        await Assert.ThrowsAsync<ArgumentException>(()=>
            fixture.EnrollAsync(Request(scope) with { Issuer="http://insecure.invalid" },now));
        await using var db=await source.OpenConnectionAsync();
        await using var count=new NpgsqlCommand("""
            SELECT count(*) FROM agm_ownership_fixture.new_sessions WHERE workspace_id=@workspace
            """,db);
        count.Parameters.AddWithValue("workspace",scope);
        Assert.Equal(0L,await count.ExecuteScalarAsync());
    }
}
