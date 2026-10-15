using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class ControlledAiPostgresTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_reservations_and_execution_claims_do_not_exceed_quota_or_duplicate_calls(bool sameKey)
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var schema = "ai_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection); await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema }.ConnectionString).Options;
            var runtime = ControlledAiProviderTests.Runtime(); runtime.ControlledAi.MaxCallsPerDay = 1; runtime.ControlledAi.TotalBudgetUsd = .1m;
            var provider = new ControlledAiLifecycleTests.Provider();
            WeddingPlannerWorkspaceResult workspace; Guid session;
            await using (var db = new BlissDbContext(options))
            {
                await db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateTablesAsync();
                var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "TEST AI concurrency", CreatedAt = DateTime.UtcNow }; db.Add(advertiser); await db.SaveChangesAsync();
                var planner = new WeddingPlannerService(db);
                workspace = await planner.OpenPrimaryWorkspaceAsync(advertiser.Id, "TEST", "workspace", true, null, "OPERATOR", "tester", "test");
                session = (await planner.CreateSessionAsync(workspace.WorkspaceId, "TEST", "session", true, null, "OPERATOR", "tester", "test")).SessionId;
                db.Add(new WeddingPlannerBrandDna { Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, Version = 1, Status = "CONFIRMED", BrandName = "TEST Brand", IdempotencyKey = "brand", CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
            }
            async Task<string> Submit(int index)
            {
                await using var db = new BlissDbContext(options);
                try
                {
                    var result = await new ControlledAiService(db, runtime, provider).Submit(workspace,
                        new SubmitAiRun("TEXT", session, null, null, "Offer an idea", sameKey ? "same" : $"key-{index}", true), "OPERATOR", "tester", "test", default);
                    return result.Replay ? "REPLAY" : "CREATED";
                }
                catch (WeddingPlannerConflictException) { return "CONFLICT"; }
                catch (AiQuotaException) { return "QUOTA"; }
            }
            var outcomes = await Task.WhenAll(Submit(0), Submit(1)); Assert.Single(outcomes, x => x == "CREATED");
            await using var verify = new BlissDbContext(options);
            var queued = Assert.Single(await verify.ControlledAiRuns.ToListAsync());
            Assert.Equal(1, (await verify.ControlledAiDailyUsage.SingleAsync()).Calls);
            Assert.Equal(.1m, (await verify.ControlledAiBudgetLedgers.SingleAsync()).ReservedUsd);
            Assert.Single(await verify.ControlledAiEvents.Where(x => x.Action == "QUEUED").ToListAsync());
            if (sameKey) Assert.Equal("REPLAY", await Submit(0));
            async Task Process()
            {
                await using var db = new BlissDbContext(options);
                await new ControlledAiService(db, runtime, provider).RunOne(default);
            }
            await Task.WhenAll(Process(), Process());
            await verify.Entry(queued).ReloadAsync();
            Assert.Equal("SUCCEEDED_PENDING_REVIEW", queued.Status); Assert.Equal(1, provider.Calls);
            Assert.Single(await verify.ControlledAiEvents.Where(x => x.Action == "RUNNING").ToListAsync());
            Assert.Single(await verify.ControlledAiEvents.Where(x => x.Action == "SUCCEEDED_PENDING_REVIEW").ToListAsync());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin); await drop.ExecuteNonQueryAsync(); }
    }
}
