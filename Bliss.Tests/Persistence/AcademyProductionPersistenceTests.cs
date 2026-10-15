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

public sealed class AcademyProductionPersistenceTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory factory;
    public AcademyProductionPersistenceTests(BlissApiFactory factory) => this.factory = factory;

    [Theory]
    [InlineData("brief")]
    [InlineData("candidate")]
    [InlineData("review")]
    public async Task Production_records_refuse_updates_and_deletes(string kind)
    {
        await using var db = TestDb.CreateContext();
        object row = kind switch
        {
            "brief" => new AcademyProductionBrief { Id = Guid.NewGuid(), ActorLabel = "Original" },
            "candidate" => new AcademyProductionCandidate { Id = Guid.NewGuid(), ActorLabel = "Original" },
            _ => new AcademyProductionReview { Id = Guid.NewGuid(), ActorLabel = "Original" }
        };
        db.Add(row); await db.SaveChangesAsync();
        db.Entry(row).Property("ActorLabel").CurrentValue = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.Entry(row).State = EntityState.Unchanged;
        db.Remove(row);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_candidates_and_reviews_preserve_one_revision_and_one_audit(bool sameKey)
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var schema = "production_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString).Options;
            var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
            AcademyProductionService Service(BlissDbContext db) => new(db, new BlueprintPhaseService(db, environment));
            WeddingPlannerWorkspaceResult workspace; AcademyProductionBrief brief;
            await using (var setup = new BlissDbContext(options))
            {
                await setup.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateTablesAsync();
                var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "TEST Production Concurrency", CreatedAt = DateTime.UtcNow };
                setup.Advertisers.Add(advertiser); await setup.SaveChangesAsync();
                workspace = await new WeddingPlannerService(setup).OpenPrimaryWorkspaceAsync(advertiser.Id, "TEST", "workspace", true, null, "OPERATOR", "tester", "test");
                var brand = new WeddingPlannerBrandDna { Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, Version = 1,
                    Status = "CONFIRMED", BrandName = "TEST Pharmacy", Niche = "pharmacy", Market = "Taipei", Language = "English", Goals = "Awareness", IdempotencyKey = "brand", CreatedAt = DateTime.UtcNow };
                setup.WeddingPlannerBrandDna.Add(brand); await setup.SaveChangesAsync();
                brief = (await Service(setup).CreateAsync(workspace, new CreateProductionBrief(brand.Id, "pharmacy", "Neighborhood care", "Visit", "brief"), "OPERATOR", "tester", "test", default)).Row;
            }
            var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
            {
                await using var db = new BlissDbContext(options);
                try
                {
                    var result = await Service(db).SubmitAsync(workspace, brief.Id, new SubmitProductionCandidate(0, "Candidate", "", AcademyProductionTests.Pixel, sameKey ? "same" : $"key-{index}"), "OPERATOR", "tester", "test", default);
                    return result.Replay ? "REPLAY" : "CREATED";
                }
                catch (WeddingPlannerConflictException) { return "CONFLICT"; }
            }));
            Assert.Single(outcomes, x => x == "CREATED"); Assert.Single(outcomes, x => x == (sameKey ? "REPLAY" : "CONFLICT"));
            await using var verify = new BlissDbContext(options);
            var candidate = Assert.Single(await verify.AcademyProductionCandidates.ToListAsync());
            Assert.Single(await verify.WeddingPlannerAuditEvents.Where(x => x.Action == "PRODUCTION_CANDIDATE_SAVED").ToListAsync());
            var reviews = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
            {
                await using var db = new BlissDbContext(options);
                try
                {
                    var result = await Service(db).ReviewAsync(workspace, brief.Id, candidate.Id,
                        new RecordProductionReview(0, "PASS", "PASS", "PASS", "PASS", "PASS", false, "Human-reviewed", "", "", sameKey ? "same-review" : $"review-{index}"), "OPERATOR", "tester", "test", default);
                    return result.Replay ? "REPLAY" : "CREATED";
                }
                catch (WeddingPlannerConflictException) { return "CONFLICT"; }
            }));
            Assert.Single(reviews, x => x == "CREATED"); Assert.Single(reviews, x => x == (sameKey ? "REPLAY" : "CONFLICT"));
            Assert.Single(await verify.AcademyProductionReviews.ToListAsync());
            Assert.Single(await verify.WeddingPlannerAuditEvents.Where(x => x.Action == "PRODUCTION_REVIEW_RECORDED").ToListAsync());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
