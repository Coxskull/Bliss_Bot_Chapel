using System.Text.Json;
using System.Text.Json.Nodes;
using Bliss.Api.Operations;
using Bliss.Domain.Economics;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class LocalCreativeHandoffEvidenceTests(BlissApiFactory factory) : IClassFixture<BlissApiFactory>
{
    [CampaignAuthorityPostgresFact]
    public async Task Actual_service_handoff_correlates_only_its_exact_selected_lineage_without_authority()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")!; var schema = "handoff_evidence_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString;
            await using var db = new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scoped).Options);
            await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(); await db.Database.ExecuteSqlRawAsync(LocalLineageProposalStore.FixtureSchemaSql);
            await new Phase1DataSeeder(db).SeedAsync(); await new Phase2DataSeeder(db).SeedAsync();
            var intake = await new CreatorIngestionService(db).IngestAsync(new("LOCAL_HANDOFF_EVIDENCE", "intake", "YouTube", "CRT-TEST-BR-001", "Test Creator Brazil"));
            var formed = await new MatchFormationService(db, new MatchRuleEvaluationService(db)).FormAsync(new("LOCAL_HANDOFF_EVIDENCE", "formation", intake.CreatorId, Phase2DataSeeder.OpportunityBrId, Phase2DataSeeder.RuleVersion2Id, true));
            var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
            var fixture = await CreativeHandoffTestData.Seed(db, environment);
            var recommendation = (await new RateRecommendationService(db).GenerateAsync(new(intake.CreatorId, CreativeHandoffFixture.Slot,
                EconomicsDataSeeder.ManilaId, "CPM", 60, Phase2DataSeeder.OpportunityBrId, formed.BlissMatchId,
                "CREATOR_TOOLS", "Synthetic handoff evidence", "LOCAL_HANDOFF_EVIDENCE", "rate"))).Recommendation;
            var quotes = new QuoteService(db);
            var quote = await quotes.CreateAsync(new(Phase2DataSeeder.OpportunityBrId, "Fixture operator", "Synthetic quote for exact formed match",
                [new QuoteLineCommand(recommendation.Id, "Synthetic placement", 1, recommendation.RangeTarget)], "LOCAL_HANDOFF_EVIDENCE", "quote"));
            var version = quote.NewQuoteVersionId!.Value;
            await quotes.DecideApprovalAsync(new(quote.Quote.Id, version, QuoteApprovalDecisions.Approved, "Fixture reviewer", "Fictional only", "LOCAL_HANDOFF_EVIDENCE", "approval"));
            await quotes.RecordOutcomeAsync(new(quote.Quote.Id, version, QuoteOutcomeResponses.Accepted, "Fixture owner", "Fictional only", null, "LOCAL_HANDOFF_EVIDENCE", "accept"));
            var line = quote.Quote.Versions.Single(x => x.Id == version).LineItems.Single();
            var intent = (await CreativeHandoffTestData.Approve(db, environment, fixture)) with { BlissMatchId = formed.BlissMatchId, QuoteLineItemId = line.Id };
            var handoffs = new CreativeHandoffService(db, new AcademyProductionService(db, new BlueprintPhaseService(db, environment)), new CampaignPlacementService(db));
            var handoff = (await handoffs.Plan(fixture.Workspace, intent, "fixture-planner", "Fixture operator", true, default)).Row;
            db.ChangeTracker.Clear();
            var author = new LocalLineageProposalGrant(new(true, "fixture-author", Phase2DataSeeder.Advertiser2Id, true), true);
            var selected = new LocalLineageSelection(intake.RunId, formed.RunId, handoff.CampaignPlacementRunId);
            var captured = await new LocalCampaignLineageEvidence(db).ReadAsync(author.Reader, selected);
            var proposal = await new LocalLineageProposalStore(db).ProposeAsync(author, new(Guid.NewGuid(), "AGM-EV-" + Guid.NewGuid(), selected, captured.Sha256));
            var grant = new LocalLineageInspectionGrant(author.Reader with { Subject = "fixture-inspector" }, true);
            var candidate = await db.AcademyProductionCandidates.AsNoTracking().SingleAsync(x => x.Id == handoff.CandidateId);
            var creative = new LocalCreativeSelection(fixture.Workspace.WorkspaceId, candidate.Id, candidate.Version, candidate.ImageSha256);
            var service = new LocalCreativeHandoffEvidence(db); var first = await service.ReadAsync(grant, proposal.Id, creative, handoff.Id);
            Assert.Equal(first, await service.ReadAsync(grant, proposal.Id, creative, handoff.Id)); Assert.Empty(db.ChangeTracker.Entries());
            using var doc = JsonDocument.Parse(first.Json); var report = doc.RootElement;
            Assert.True(report.GetProperty("storedHandoffCorrelationVerified").GetBoolean());
            Assert.Equal(handoff.CampaignPlacementRunId, report.GetProperty("campaignPlacementRunId").GetGuid());
            foreach (var flag in new[] { "handoffPolicyRevalidated", "creativeRightsVerified", "creatorConsentVerified", "advertiserAuthorityVerified", "paymentAuthorized", "activationAuthorized", "independentQaAccepted" }) Assert.False(report.GetProperty(flag).GetBoolean());
            Assert.Equal("NOT_SENT", report.GetProperty("delivery").GetString());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, proposal.Id, creative, Guid.NewGuid()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant with { ReviewInspectionGranted = false }, proposal.Id, creative, handoff.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant with { Reader = grant.Reader with { AdvertiserId = Phase1DataSeeder.AdvertiserId } }, proposal.Id, creative, handoff.Id));
            var unrelated = await LocalCampaignLineageEvidenceTests.Seed(db);
            var otherCapture = await new LocalCampaignLineageEvidence(db).ReadAsync(author.Reader, unrelated);
            var otherProposal = await new LocalLineageProposalStore(db).ProposeAsync(author, new(Guid.NewGuid(), "AGM-EV-" + Guid.NewGuid(), unrelated, otherCapture.Sha256));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, otherProposal.Id, creative, handoff.Id));
            var artifact = Environment.GetEnvironmentVariable("AGM_HANDOFF_EVIDENCE_PATH");
            if (!string.IsNullOrWhiteSpace(artifact)) await File.WriteAllTextAsync(artifact, JsonSerializer.Serialize(first));
            var altered = JsonNode.Parse(handoff.SnapshotJson)!.AsObject(); altered["imageSha256"] = new string('0',64);
            await using var raw = new NpgsqlConnection(scoped); await raw.OpenAsync();
            await using (var tamper = new NpgsqlCommand("UPDATE \"CreativeCampaignHandoffs\" SET \"SnapshotJson\"=@snapshot WHERE \"Id\"=@id", raw))
            { tamper.Parameters.AddWithValue("snapshot", altered.ToJsonString()); tamper.Parameters.AddWithValue("id", handoff.Id); await tamper.ExecuteNonQueryAsync(); }
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadAsync(grant, proposal.Id, creative, handoff.Id));
            Assert.Equal(1, await db.CreativeCampaignHandoffs.CountAsync()); Assert.Empty(db.ChangeTracker.Entries());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
