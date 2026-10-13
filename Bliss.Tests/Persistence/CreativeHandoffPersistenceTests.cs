using Bliss.Api.Operations;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class CreativeHandoffPersistenceTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory factory;
    public CreativeHandoffPersistenceTests(BlissApiFactory factory) => this.factory = factory;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_and_handoff_evidence_cannot_be_updated_or_deleted(bool handoff)
    {
        await using var db = TestDb.CreateContext();
        object row = handoff ? new CreativeCampaignHandoff { Id = Guid.NewGuid(), ActorLabel = "Original" }
            : new CandidateApprovalDecision { Id = Guid.NewGuid(), ActorLabel = "Original" };
        db.Add(row); await db.SaveChangesAsync();
        db.Entry(row).Property("ActorLabel").CurrentValue = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(row).State = EntityState.Unchanged; db.Remove(row);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_plans_create_one_complete_lineage_and_never_orphan_placements(bool sameKey)
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var schema = "handoff_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString).Options;
            var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
            CreativeHandoffService Service(BlissDbContext db) => new(db, new AcademyProductionService(db, new BlueprintPhaseService(db, environment)), new CampaignPlacementService(db));
            CreativeHandoffFixture fixture; PlanCreativeHandoff request; int originalPlacements;
            await using (var setup = new BlissDbContext(options))
            {
                await setup.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateTablesAsync();
                fixture = await CreativeHandoffTestData.Seed(setup, environment);
                request = await CreativeHandoffTestData.Approve(setup, environment, fixture);
                originalPlacements = await setup.CampaignPlacements.CountAsync();
            }
            async Task<string> Submit(int index)
            {
                await using var db = new BlissDbContext(options);
                try
                {
                    var result = await Service(db).Plan(fixture.Workspace, request with { IdempotencyKey = sameKey ? "concurrent-plan" : $"concurrent-plan-{index}" },
                        "fixture-planner", "Fixture operator", true, default);
                    return result.Replay ? "REPLAY" : "CREATED";
                }
                catch (WeddingPlannerConflictException) { return "CONFLICT"; }
            }
            var outcomes = await Task.WhenAll(Submit(0), Submit(1));
            Assert.Single(outcomes, x => x == "CREATED");
            Assert.Single(outcomes, x => x is "REPLAY" or "CONFLICT");
            if (sameKey) Assert.Equal("REPLAY", await Submit(0));
            await using var verify = new BlissDbContext(options);
            var handoff = Assert.Single(await verify.CreativeCampaignHandoffs.ToListAsync());
            Assert.Equal(originalPlacements + 1, await verify.CampaignPlacements.CountAsync());
            var run = Assert.Single(await verify.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
            Assert.Equal(handoff.CampaignPlacementRunId, run.Id); Assert.Equal(handoff.CampaignPlacementId, run.CampaignPlacementId);
            Assert.Single(await verify.WeddingPlannerAuditEvents.Where(x => x.Action == "CREATIVE_HANDOFF_PLANNED").ToListAsync());
            Assert.Equal("DRAFT", (await verify.Campaigns.SingleAsync(x => x.Id == fixture.Campaign)).Status);
            Assert.True((await verify.AdInventorySlots.SingleAsync(x => x.Id == CreativeHandoffFixture.Slot)).IsAvailable);
            async Task<string> Revoke(int index)
            {
                await using var db = new BlissDbContext(options);
                try
                {
                    var result = await Service(db).Decide(fixture.Workspace, fixture.Candidate,
                        CreativeHandoffTestData.Approval(fixture, "ADVERTISER", sameKey ? "concurrent-revoke" : $"concurrent-revoke-{index}", 1, "WITHHOLD"),
                        "fixture-client", "Fixture client", true, default);
                    return result.Replay ? "REPLAY" : "CREATED";
                }
                catch (WeddingPlannerConflictException) { return "CONFLICT"; }
            }
            var revocations = await Task.WhenAll(Revoke(0), Revoke(1));
            Assert.Single(revocations, x => x == "CREATED");
            Assert.Single(revocations, x => x == (sameKey ? "REPLAY" : "CONFLICT"));
            Assert.Equal(2, await verify.CandidateApprovalDecisions.CountAsync(x => x.Stage == "ADVERTISER"));
            Assert.Equal(4, await verify.WeddingPlannerAuditEvents.CountAsync(x => x.Action == "CANDIDATE_APPROVAL_RECORDED"));
            Assert.Single(await verify.CreativeCampaignHandoffs.ToListAsync());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
