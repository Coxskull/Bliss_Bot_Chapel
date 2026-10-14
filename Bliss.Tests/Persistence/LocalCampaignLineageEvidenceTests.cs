using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class LocalCampaignLineageEvidenceTests
{
    private static LocalLineageGrant Grant => new(true, "fixture-reviewer", Phase2DataSeeder.Advertiser2Id, true);

    internal static async Task<LocalLineageSelection> Seed(BlissDbContext db)
    {
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        var intake = await new CreatorIngestionService(db).IngestAsync(new("LOCAL_LINEAGE_FIXTURE", "intake", "YouTube",
            "local-only-channel", "Synthetic intake creator", CountryCode: "PH", PrimaryLanguage: "English", AudienceSize: 100000,
            SourceUrl: "https://example.test/synthetic-observation"));
        var creator = await db.Creators.SingleAsync(x => x.Id == intake.CreatorId);
        Assert.Equal(RecordAuthorityStatuses.Unverified, creator.AuthorityStatus);
        // Explicit test-only fixture classification, not a verification API or real consent.
        creator.AuthorityStatus = RecordAuthorityStatuses.SyntheticFixture;
        var content = new ContentItem { Id = Guid.NewGuid(), CreatorId = creator.Id, Title = "Synthetic inventory", CreatedAt = DateTime.UtcNow };
        var slot = new AdInventorySlot { Id = Guid.NewGuid(), ContentItemId = content.Id, SlotType = InventorySlotTypes.MidRoll, IsAvailable = true };
        var campaign = new Campaign { Id = Guid.NewGuid(), Name = "Synthetic lineage rehearsal", AdvertiserOpportunityId = Phase2DataSeeder.OpportunityPhId,
            AuthorityStatus = RecordAuthorityStatuses.SyntheticFixture, CreatedAt = DateTime.UtcNow };
        db.AddRange(content, slot, campaign); await db.SaveChangesAsync();
        var formation = await new MatchFormationService(db, new MatchRuleEvaluationService(db)).FormAsync(new("LOCAL_LINEAGE_FIXTURE", "formation",
            creator.Id, Phase2DataSeeder.OpportunityPhId, Phase2DataSeeder.RuleVersion2Id, true));
        Assert.Equal(EntityStatuses.Approved, formation.MatchStatus);
        var placement = await new CampaignPlacementService(db).BindAsync(new("LOCAL_LINEAGE_FIXTURE", "placement", "Fixture operator",
            formation.BlissMatchId, campaign.Id, content.Id, slot.Id));
        db.ChangeTracker.Clear();
        return new(intake.RunId, formation.RunId, placement.RunId);
    }

    [Fact]
    public async Task Explicit_selected_intake_formation_and_planning_export_deterministically_without_writes()
    {
        await using var db = TestDb.CreateContext(); var selection = await Seed(db);
        var service = new LocalCampaignLineageEvidence(db);
        var first = await service.ReadAsync(Grant, selection); var second = await service.ReadAsync(Grant, selection);
        Assert.Equal(first, second); Assert.False(db.ChangeTracker.HasChanges()); Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Json))).ToLowerInvariant(), first.Sha256);
        using var doc = JsonDocument.Parse(first.Json); var report = doc.RootElement;
        Assert.True(report.GetProperty("traceVerified").GetBoolean());
        foreach (var name in new[] { "researchTruthVerified", "consentVerified", "creativeRightsVerified", "paymentAuthorized", "activationAuthorized", "independentQaAccepted" })
            Assert.False(report.GetProperty(name).GetBoolean());
        Assert.Equal("NOT_SENT", report.GetProperty("delivery").GetString());
        Assert.DoesNotContain("example.test", first.Json); Assert.DoesNotContain("Synthetic intake creator", first.Json);
    }

    [Theory]
    [InlineData("unauthenticated")]
    [InlineData("ungranted")]
    [InlineData("foreign-advertiser")]
    [InlineData("seeded-formation")]
    [InlineData("foreign-intake")]
    [InlineData("unverified")]
    [InlineData("missing-provenance")]
    [InlineData("later-intake")]
    [InlineData("changed-graph")]
    [InlineData("changed-status")]
    public async Task Missing_scope_provenance_or_graph_authority_is_refused(string scenario)
    {
        await using var db = TestDb.CreateContext(); var selection = await Seed(db); var grant = Grant;
        var intake = await db.CreatorIngestionRuns.SingleAsync(x => x.Id == selection.IngestionRunId);
        var placement = await db.CampaignPlacementRuns.SingleAsync(x => x.Id == selection.PlacementRunId);
        switch (scenario)
        {
            case "unauthenticated": grant = grant with { Authenticated = false }; break;
            case "ungranted": grant = grant with { EvidenceReadGranted = false }; break;
            case "foreign-advertiser": grant = grant with { AdvertiserId = Phase1DataSeeder.AdvertiserId }; break;
            case "seeded-formation": selection = selection with { FormationRunId = Phase2DataSeeder.MatchBrazilApprovedId }; break;
            case "foreign-intake": intake.CreatorId = Phase1DataSeeder.CreatorId; break;
            case "unverified": (await db.Creators.SingleAsync(x => x.Id == intake.CreatorId)).AuthorityStatus = RecordAuthorityStatuses.Unverified; break;
            case "missing-provenance": db.DataProvenances.RemoveRange(await db.DataProvenances.Where(x => x.EntityId == intake.CreatorId).ToListAsync()); break;
            case "later-intake": intake.CompletedAt = placement.StartedAt.AddDays(1); break;
            case "changed-graph": (await db.CampaignPlacements.SingleAsync(x => x.Id == placement.CampaignPlacementId)).ContentItemId = Phase1DataSeeder.ContentItem1Id; break;
            case "changed-status": (await db.Campaigns.SingleAsync(x => x.Id == placement.CampaignId)).Status = "ACTIVE"; break;
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LocalCampaignLineageEvidence(db).ReadAsync(grant, selection));
    }

    [CampaignAuthorityPostgresFact]
    public async Task PostgreSql_repeatable_read_report_uses_actual_lineage_and_does_not_write()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")!;
        var schema = "local_lineage_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString).Options;
            await using var db = new BlissDbContext(options);
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            var selection = await Seed(db); var events = await db.CampaignPlacementRuns.CountAsync();
            var report = await new LocalCampaignLineageEvidence(db).ReadAsync(Grant, selection);
            Assert.Equal(events, await db.CampaignPlacementRuns.CountAsync()); Assert.Empty(db.ChangeTracker.Entries());
            Assert.Equal(report, await new LocalCampaignLineageEvidence(db).ReadAsync(Grant, selection));
            var path = Environment.GetEnvironmentVariable("AGM_CAMPAIGN_LINEAGE_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(path)) await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report));
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
