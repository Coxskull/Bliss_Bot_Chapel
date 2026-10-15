using Bliss.Infrastructure.Persistence;
using Npgsql;

namespace Bliss.Tests.Financials;

public sealed class AskAlphaConsentFixtureTests
{
    private static (AskAlphaConsentScope Scope, AskAlphaConsentActor Actor,
        AskAlphaConsentIntent First) Sample()
    {
        var scope = new AskAlphaConsentScope(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            Guid.NewGuid(),Guid.NewGuid(),"fictional-automotive-advertiser");
        var actor = new AskAlphaConsentActor("fixture-visitor-"+Guid.NewGuid().ToString("N"),
            true,true,true);
        var command = new AskAlphaConsentIntent(scope,actor,"OPT_IN",
            new string('A',64),2,0,"optin-fixture-001");
        return (scope,actor,command);
    }

    [Fact]
    public void Fixture_policy_requires_visitor_identity_constrained_scope_and_exact_digest()
    {
        var (_, actor, first) = Sample();
        AskAlphaConsentFixturePolicy.Validate(first);
        Assert.Throws<UnauthorizedAccessException>(() => AskAlphaConsentFixturePolicy.Validate(
            first with { Actor = actor with { VisitorVerified = false } }));
        Assert.Throws<UnauthorizedAccessException>(() => AskAlphaConsentFixturePolicy.Validate(
            first with { Actor = actor with { SyntheticFixture = false } }));
        Assert.Throws<UnauthorizedAccessException>(() => AskAlphaConsentFixturePolicy.Validate(
            first with { Actor = actor with { Subject = "advertiser-owner" } }));
        Assert.Throws<ArgumentException>(() => AskAlphaConsentFixturePolicy.Validate(
            first with { TranscriptSha256 = "not-evidence" }));
        Assert.Throws<ArgumentException>(() => AskAlphaConsentFixturePolicy.Validate(
            first with { Scope = first.Scope with { SourceSlug = "../../private" } }));
        Assert.Throws<ArgumentException>(() => AskAlphaConsentFixturePolicy.Validate(
            first with { Scope = first.Scope with { WorkspaceId = Guid.Empty } }));
        Assert.Throws<ArgumentException>(() => AskAlphaConsentFixturePolicy.Validate(
            first with { RetryKey = "x" }));
    }

    [Fact]
    public void Immutable_chain_policy_detects_reordering_changed_scope_digest_and_unapproved_authority()
    {
        var (scope,actor,first) = Sample();
        var receipt1 = new AskAlphaConsentEvent(Guid.NewGuid(),scope,actor.Subject,
            "OPT_IN",first.TranscriptSha256,first.TurnCount,1,0,first.RetryKey,
            AskAlphaConsentFixturePolicy.Zero,"",DateTime.UtcNow);
        receipt1 = receipt1 with { EventSha256=AskAlphaConsentFixturePolicy.ComputeHash(receipt1) };
        var receipt2 = new AskAlphaConsentEvent(Guid.NewGuid(),scope,actor.Subject,
            "REVOKE",first.TranscriptSha256,first.TurnCount,2,1,"revoke-fixture-001",
            receipt1.EventSha256,"",DateTime.UtcNow);
        receipt2 = receipt2 with { EventSha256=AskAlphaConsentFixturePolicy.ComputeHash(receipt2) };
        var active = AskAlphaConsentFixturePolicy.Evaluate(scope,actor.Subject,[receipt1]);
        Assert.Equal("OPT_IN_RECORDED_UNVERIFIED",active.Status);
        Assert.False(active.IdentityIndependentlyVerified);
        Assert.False(active.TranscriptLinked);
        Assert.False(active.TranscriptExportAuthorized);
        Assert.False(active.PaymentAuthorized);
        Assert.False(active.CreatorPayoutAuthorized);
        Assert.False(active.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",active.Delivery);
        var revoked = AskAlphaConsentFixturePolicy.Evaluate(scope,actor.Subject,[receipt1,receipt2]);
        Assert.Equal("REVOKED",revoked.Status);
        Assert.Equal(2,revoked.Version);
        Assert.False(revoked.TranscriptExportAuthorized);
        Assert.False(revoked.CampaignDeliveryAuthorized);
        Assert.Throws<InvalidOperationException>(() => AskAlphaConsentFixturePolicy.Evaluate(
            scope,actor.Subject,[receipt2,receipt1]));
        Assert.Throws<InvalidOperationException>(() => AskAlphaConsentFixturePolicy.Evaluate(
            scope,actor.Subject,[receipt1,receipt2 with { TranscriptSha256=new string('B',64) }]));
        Assert.Throws<InvalidOperationException>(() => AskAlphaConsentFixturePolicy.Evaluate(
            scope with { AdvertiserId=Guid.NewGuid() },actor.Subject,[receipt1,receipt2]));
        Assert.Throws<InvalidOperationException>(() => AskAlphaConsentFixturePolicy.Evaluate(
            scope,actor.Subject,[receipt1,receipt2 with { EventSha256=new string('C',64) }]));
    }

    [FinancialPostgresFact]
    public async Task Real_isolated_Postgres_enforces_concurrent_optin_retry_revocation_and_immutability()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;
        var guard = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1",guard.Host);
        Assert.Equal("financial_test",guard.Database);
        await using var data = NpgsqlDataSource.Create(connection);
        var store = new AskAlphaConsentFixtureLedger(data);
        var (scope,actor,first) = Sample();

        var copies = await Task.WhenAll(Enumerable.Range(0,8).Select(_ => store.AppendAsync(first)));
        Assert.Single(copies.Select(x=>x.Id).Distinct());
        Assert.Equal(1,copies.Count(x=>!x.IsReplay));
        Assert.Equal(1,(await store.ReadAsync(scope,actor)).Version);
        Assert.Equal("OPT_IN_RECORDED_UNVERIFIED",(await store.ReadAsync(scope,actor)).Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendAsync(first with { TranscriptSha256=new string('B',64) }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendAsync(first with { ExpectedVersion=1, RetryKey="different-retry-001" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendAsync(first with { Decision="REVOKE",ExpectedVersion=0,RetryKey="early-revoke-001" }));

        var revoke = first with { Decision="REVOKE",ExpectedVersion=1,RetryKey="revoke-fixture-001" };
        var revoked = await Task.WhenAll(Enumerable.Range(0,8).Select(_ => store.AppendAsync(revoke)));
        Assert.Single(revoked.Select(x=>x.Id).Distinct());
        Assert.Equal(1,revoked.Count(x=>!x.IsReplay));
        Assert.Equal("REVOKED",(await store.ReadAsync(scope,actor)).Status);
        Assert.Equal(2,(await store.ReadAsync(scope,actor)).Version);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendAsync(first with { ExpectedVersion=2,RetryKey="new-optin-001" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendAsync(revoke with { TranscriptSha256=new string('B',64),RetryKey="changed-revoke-001" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.ReadAsync(scope with { AdvertiserId=Guid.NewGuid() },actor));

        await using var db = await data.OpenConnectionAsync();
        foreach(var sql in new[] {
            "UPDATE agm_consent_fixture.events SET decision='OPT_IN' WHERE handoff_id=@id",
            "DELETE FROM agm_consent_fixture.events WHERE handoff_id=@id",
            "TRUNCATE agm_consent_fixture.events"
        })
        {
            await using var cmd = new NpgsqlCommand(sql,db);
            if(sql.Contains("@id"))cmd.Parameters.AddWithValue("id",scope.HandoffId);
            await Assert.ThrowsAsync<PostgresException>(async()=>await cmd.ExecuteNonQueryAsync());
        }
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM agm_consent_fixture.events WHERE handoff_id=@id",db);
        count.Parameters.AddWithValue("id",scope.HandoffId);
        Assert.Equal(2L,await count.ExecuteScalarAsync());
    }
}
