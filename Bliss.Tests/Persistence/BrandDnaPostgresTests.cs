using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class BrandDnaPostgresTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_drafts_keep_one_revision_and_one_audit(bool sameKey)
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection)) return;
        var schema = "brand_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA \"{schema}\"", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var scoped = new NpgsqlConnectionStringBuilder(connection) { SearchPath = schema };
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scoped.ConnectionString).Options;
            WeddingPlannerWorkspaceResult workspace;
            await using (var setup = new BlissDbContext(options))
            {
                // The database can contain tables in other test schemas. Create this known-empty schema explicitly.
                await setup.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>().CreateTablesAsync();
                var advertiser = new Advertiser { Id = Guid.NewGuid(), Name = "TEST concurrent intake", CreatedAt = DateTime.UtcNow };
                setup.Advertisers.Add(advertiser);
                await setup.SaveChangesAsync();
                workspace = await new WeddingPlannerService(setup).OpenPrimaryWorkspaceAsync(
                    advertiser.Id, "TEST", "workspace", true, null, "OPERATOR", "tester", "test");
            }
            var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
            {
                await using var db = new BlissDbContext(options);
                try
                {
                    var result = await new WeddingPlannerBrandDnaService(db).AppendAsync(workspace,
                        new BrandDnaFields("Concurrent", "Bakery", "Taipei", "English", "", "", "Awareness", ""),
                        0, sameKey ? "same" : $"key-{index}", false, "OPERATOR", "tester", "test", default);
                    return result.IsReplay ? "REPLAY" : "CREATED";
                }
                catch (WeddingPlannerConflictException) { return "CONFLICT"; }
            }));
            Assert.Single(outcomes, x => x == "CREATED");
            Assert.Single(outcomes, x => x == (sameKey ? "REPLAY" : "CONFLICT"));
            await using var verify = new BlissDbContext(options);
            Assert.Single(await verify.WeddingPlannerBrandDna.ToListAsync());
            Assert.Single(await verify.WeddingPlannerAuditEvents.Where(x => x.Action == "BRAND_DNA_DRAFT_SAVED").ToListAsync());
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{schema}\" CASCADE", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
