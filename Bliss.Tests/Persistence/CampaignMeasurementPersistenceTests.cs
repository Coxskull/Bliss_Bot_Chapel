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

public sealed class CampaignMeasurementPersistenceTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory factory;
    public CampaignMeasurementPersistenceTests(BlissApiFactory factory) => this.factory = factory;
    [Theory]
    [InlineData("observation")]
    [InlineData("proposal")]
    [InlineData("review")]
    public async Task Measurement_and_learning_records_refuse_update_and_delete(string kind)
    {
        await using var db = TestDb.CreateContext();
        object row = kind switch {
            "observation" => new CampaignObservation { Id = Guid.NewGuid(), ActorLabel = "Original" },
            "proposal" => new CampaignLearningProposal { Id = Guid.NewGuid(), ActorLabel = "Original" },
            _ => new CampaignLearningReview { Id = Guid.NewGuid(), ActorLabel = "Original" }
        };
        db.Add(row); await db.SaveChangesAsync();
        db.Entry(row).Property("ActorLabel").CurrentValue = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(row).State = EntityState.Unchanged; db.Remove(row);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_observation_proposal_and_review_retries_preserve_single_records_and_audits(bool sameReviewKey)
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var schema = "measurement_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString).Options;
            var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
            CreativeHandoffFixture fixture; Guid handoff;
            await using (var setup = new BlissDbContext(options))
            {
                await setup.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateTablesAsync();
                fixture = await CreativeHandoffTestData.Seed(setup, environment);
                var request = await CreativeHandoffTestData.Approve(setup, environment, fixture);
                handoff = (await new CreativeHandoffService(setup, new AcademyProductionService(setup, new BlueprintPhaseService(setup, environment)), new CampaignPlacementService(setup))
                    .Plan(fixture.Workspace, request, "fixture-planner", "Fixture planner", true, default)).Row.Id;
            }
            var observationRequest = new RecordCampaignObservation(handoff, "LOCAL_VALIDATION", "TEST concurrency", "Fictional local observation", DateTime.UtcNow,
                null, null, null, null, null, false, "concurrent-observation");
            async Task<string> Submit(string kind, int index, Guid reference = default)
            {
                await using var db = new BlissDbContext(options); var service = new CampaignMeasurementService(db);
                try
                {
                    var replay = kind switch {
                        "observation" => (await service.Record(fixture.Workspace, observationRequest, "fixture-author", "Fixture author", default)).Replay,
                        "proposal" => (await service.Propose(fixture.Workspace, new(reference, "TEST hypothesis", "TEST controlled experiment", "TEST human review", false, "concurrent-proposal"),
                            "fixture-author", "Fixture author", default)).Replay,
                        _ => (await service.Review(fixture.Workspace, reference, new(0, "ACCEPT_FOR_RESEARCH", "TEST research only", false,
                            sameReviewKey ? "concurrent-review" : $"concurrent-review-{index}"), "fixture-reviewer", "Fixture reviewer", true, default)).Replay
                    };
                    return replay ? "REPLAY" : "CREATED";
                }
                catch (WeddingPlannerConflictException) { return "CONFLICT"; }
            }
            var observations = await Task.WhenAll(Submit("observation", 0), Submit("observation", 1));
            Assert.Single(observations, x => x == "CREATED"); Assert.Single(observations, x => x == "REPLAY");
            await using var verify = new BlissDbContext(options);
            var observation = await verify.CampaignObservations.SingleAsync();
            var proposals = await Task.WhenAll(Submit("proposal", 0, observation.Id), Submit("proposal", 1, observation.Id));
            Assert.Single(proposals, x => x == "CREATED"); Assert.Single(proposals, x => x == "REPLAY");
            var proposal = await verify.CampaignLearningProposals.SingleAsync();
            var reviews = await Task.WhenAll(Submit("review", 0, proposal.Id), Submit("review", 1, proposal.Id));
            Assert.Single(reviews, x => x == "CREATED"); Assert.Single(reviews, x => x == (sameReviewKey ? "REPLAY" : "CONFLICT"));
            Assert.Single(await verify.CampaignLearningReviews.ToListAsync());
            foreach (var action in new[] { "CAMPAIGN_OBSERVATION_RECORDED", "CAMPAIGN_LEARNING_PROPOSED", "CAMPAIGN_LEARNING_REVIEWED" })
                Assert.Single(await verify.WeddingPlannerAuditEvents.Where(x => x.Action == action).ToListAsync());
            Assert.Equal("NOT_SENT", (await verify.CreativeCampaignHandoffs.SingleAsync()).Delivery);
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
