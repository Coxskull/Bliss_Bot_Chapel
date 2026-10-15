using System.Security.Cryptography;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class AskAlphaConsentBoundaryFixtureTests
{
    private const string Issuer="https://issuer.fixture.invalid";
    private const string Subject="fixture-visitor-12345678";

    [Theory]
    [InlineData(false,false,false,false,false,"OWNER_ENGINEERING_APPROVAL_NOT_RECORDED")]
    [InlineData(true,false,false,false,false,"VISITOR_CONSENT_PREREQUISITES_MISSING")]
    [InlineData(true,true,false,false,true,"VISITOR_CONSENT_PREREQUISITES_MISSING")]
    [InlineData(true,true,false,true,false,"VISITOR_CONSENT_PREREQUISITES_MISSING")]
    [InlineData(true,true,false,true,true,"SYNTHETIC_CONSENT_INTENT_RECORDED_UNVERIFIED")]
    [InlineData(true,true,true,true,true,"SYNTHETIC_CONSENT_REVOKED_NO_EXPORT")]
    public void Owner_approval_synthetic_intent_and_revocation_never_equal_real_visitor_consent(
        bool owner,bool optIn,bool revoked,bool purpose,bool signed,string expected)
    {
        var row=AskAlphaConsentBoundaryEvaluator.Inspect(owner,optIn,revoked,purpose,signed);
        Assert.Equal(expected,row.Status);
        Assert.Equal(owner,row.OwnerEngineeringApprovalRecorded);
        Assert.False(row.RealVisitorIdentityVerified);
        Assert.False(row.RealVisitorConsentAccepted);
        Assert.False(row.HistoricalSessionOwnershipVerified);
        Assert.False(row.TranscriptLinked);
        Assert.False(row.TranscriptExportAuthorized);
        Assert.False(row.PaymentAuthorized);
        Assert.False(row.CreatorPayoutAuthorized);
        Assert.False(row.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",row.Delivery);
    }

    [FinancialPostgresFact]
    public async Task Isolated_consent_events_require_exact_scope_optin_before_revoke_and_are_append_only()
    {
        var cs=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;
        var builder=new NpgsqlConnectionStringBuilder(cs);
        Assert.Equal("127.0.0.1",builder.Host);
        Assert.Equal("financial_test",builder.Database);
        await using var data=NpgsqlDataSource.Create(cs);
        var key=RandomNumberGenerator.GetBytes(32);
        var factory=new AskAlphaAtomicFixtureEnrollment(data,key);
        var workspace=Guid.NewGuid();
        var now=DateTimeOffset.UtcNow;
        var enrollment=await factory.EnrollAsync(new AskAlphaAtomicFixtureRequest(
            workspace,"fictional-consent",Issuer,Subject,"consentfixture_123456"),now);
        var digest=AskAlphaOwnershipFixtureSignature.SubjectDigest(Issuer,Subject);

        async Task Insert(string action,Guid ws,string issuer,string hash,string version,
            string purpose,string receiptKey,DateTimeOffset at)
        {
            await using var db=await data.OpenConnectionAsync();
            await using var command=new NpgsqlCommand("""
                INSERT INTO agm_ownership_fixture.consent_events
                (event_id,session_id,workspace_id,issuer,subject_sha256,
                 action,purpose,consent_version,receipt_key,occurred_at)
                VALUES(@id,@session,@workspace,@issuer,@subject,@action,
                       @purpose,@version,@retry,@at)
                """,db);
            command.Parameters.AddWithValue("id",Guid.NewGuid());
            command.Parameters.AddWithValue("session",enrollment.SessionId);
            command.Parameters.AddWithValue("workspace",ws);
            command.Parameters.AddWithValue("issuer",issuer);
            command.Parameters.AddWithValue("subject",hash);
            command.Parameters.AddWithValue("action",action);
            command.Parameters.AddWithValue("purpose",purpose);
            command.Parameters.AddWithValue("version",version);
            command.Parameters.AddWithValue("retry",receiptKey);
            command.Parameters.AddWithValue("at",at);
            await command.ExecuteNonQueryAsync();
        }

        Task Good(string action,string receiptKey,DateTimeOffset at) =>
            Insert(action,workspace,Issuer,digest,"AGM-CONSENT-FIXTURE-V1",
                "CREATIVE_PREVIEW_ONLY",receiptKey,at);
        async Task Denied(Func<Task> test)
        {
            var ex=await Assert.ThrowsAsync<PostgresException>(test);
            Assert.True(ex.SqlState is "23514" or "23505" or "23503");
        }

        await Denied(()=>Good("REVOKE","revoke_before_optin",now.AddSeconds(2)));
        await Denied(()=>Insert("OPT_IN",Guid.NewGuid(),Issuer,digest,
            "AGM-CONSENT-FIXTURE-V1","CREATIVE_PREVIEW_ONLY",
            "foreign_workspace",now.AddSeconds(2)));
        await Denied(()=>Insert("OPT_IN",workspace,"https://other.invalid",digest,
            "AGM-CONSENT-FIXTURE-V1","CREATIVE_PREVIEW_ONLY",
            "foreign_issuer",now.AddSeconds(2)));
        await Denied(()=>Insert("OPT_IN",workspace,Issuer,new string('A',64),
            "AGM-CONSENT-FIXTURE-V1","CREATIVE_PREVIEW_ONLY",
            "foreign_subject",now.AddSeconds(2)));
        await Denied(()=>Insert("OPT_IN",workspace,Issuer,digest,
            "AGM-CONSENT-UNAPPROVED","CREATIVE_PREVIEW_ONLY",
            "wrong_version",now.AddSeconds(2)));
        await Denied(()=>Insert("OPT_IN",workspace,Issuer,digest,
            "AGM-CONSENT-FIXTURE-V1","TRANSCRIPT_EXPORT",
            "wrong_purpose",now.AddSeconds(2)));
        await Denied(()=>Good("OPT_IN","before_creation",now.AddSeconds(-1)));

        await Good("OPT_IN","optin_fixture_12345",now.AddSeconds(2));
        await Denied(()=>Good("OPT_IN","second_optin_12345",now.AddSeconds(3)));
        await Denied(()=>Good("REVOKE","revocation_wrong_time",now.AddSeconds(1)));
        await Good("REVOKE","revoke_fixture_1234",now.AddSeconds(4));
        await Denied(()=>Good("OPT_IN","retroactive_reoptin",now.AddSeconds(5)));
        await Denied(()=>Good("REVOKE","duplicate_revoke",now.AddSeconds(6)));

        await using var verify=await data.OpenConnectionAsync();
        await using(var query=new NpgsqlCommand("""
            SELECT action FROM agm_ownership_fixture.consent_events
            WHERE session_id=@session ORDER BY occurred_at
            """,verify))
        {
            query.Parameters.AddWithValue("session",enrollment.SessionId);
            await using var rows=await query.ExecuteReaderAsync();
            var actions=new List<string>();
            while(await rows.ReadAsync())actions.Add(rows.GetString(0));
            Assert.Equal(new[]{"OPT_IN","REVOKE"},actions);
        }
        foreach(var sql in new[]{
            "UPDATE agm_ownership_fixture.consent_events SET action='OPT_IN' WHERE session_id=@session",
            "DELETE FROM agm_ownership_fixture.consent_events WHERE session_id=@session",
            "TRUNCATE agm_ownership_fixture.consent_events"
        })
        {
            await using var command=new NpgsqlCommand(sql,verify);
            if(sql.Contains("@session"))command.Parameters.AddWithValue("session",enrollment.SessionId);
            await Denied(async()=>{ await command.ExecuteNonQueryAsync(); });
        }
        var reading=AskAlphaConsentBoundaryEvaluator.Inspect(true,true,true,true,true);
        Assert.Equal("SYNTHETIC_CONSENT_REVOKED_NO_EXPORT",reading.Status);
        Assert.False(reading.TranscriptExportAuthorized);
        Assert.False(reading.CampaignDeliveryAuthorized);
    }
}
