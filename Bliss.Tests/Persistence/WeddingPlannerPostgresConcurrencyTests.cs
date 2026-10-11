using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Tests.Persistence;

/// <summary>
/// PostgreSQL-backed invariants for Wedding Planner concurrency.
/// CI sets BLISS_TEST_POSTGRES_CONNECTION; local runs without that opt-in return
/// without exercising PostgreSQL.
/// </summary>
public sealed class WeddingPlannerPostgresConcurrencyTests
{
    [Fact]
    public async Task Concurrent_primary_workspace_open_keeps_one_workspace_and_one_creation_audit()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            return;
        }

        var schema = $"wp_ci_{Guid.NewGuid():N}";
        var schemaBuilder = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schema
        };

        await using (var admin = new NpgsqlConnection(baseConnectionString))
        {
            await admin.OpenAsync();
            await using var createSchema = admin.CreateCommand();
            createSchema.CommandText = $"""CREATE SCHEMA "{schema}";""";
            await createSchema.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<BlissDbContext>()
                .UseNpgsql(schemaBuilder.ConnectionString)
                .Options;

            await using (var setup = new BlissDbContext(options))
            {
                await setup.Database.EnsureCreatedAsync();
                setup.Advertisers.Add(new Advertiser
                {
                    Id = _advertiserId,
                    Name = "Wedding Planner PostgreSQL concurrency test",
                    CreatedAt = DateTime.UtcNow
                });
                await setup.SaveChangesAsync();
            }

            var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
            {
                await using var db = new BlissDbContext(options);
                var service = new WeddingPlannerService(db);
                try
                {
                    var result = await service.OpenPrimaryWorkspaceAsync(
                        _advertiserId,
                        "POSTGRES_CONCURRENCY_TEST",
                        $"open-{Guid.NewGuid():N}-{index}",
                        true,
                        null,
                        "OPERATOR",
                        "CI integration test",
                        $"request-{index}");
                    return (Guid?)result.WorkspaceId;
                }
                catch (DbUpdateException)
                {
                    // A competing insert can lose the unique-index race. The
                    // durable invariant below ensures it cannot create a second row.
                    return null;
                }
            }));

            await using var verify = new BlissDbContext(options);
            Assert.Equal(1, await verify.WeddingPlannerWorkspaces.CountAsync(x => x.AdvertiserId == _advertiserId));
            Assert.Equal(1, await verify.WeddingPlannerAuditEvents.CountAsync(
                x => x.AdvertiserId == _advertiserId && x.Action == "WORKSPACE_OPENED"));
            Assert.Contains(outcomes, outcome => outcome.HasValue);
        }
        finally
        {
            await using var admin = new NpgsqlConnection(baseConnectionString);
            await admin.OpenAsync();
            await using var dropSchema = admin.CreateCommand();
            dropSchema.CommandText = $"""DROP SCHEMA IF EXISTS "{schema}" CASCADE;""";
            await dropSchema.ExecuteNonQueryAsync();
        }
    }

    private static readonly Guid _advertiserId = Guid.NewGuid();
}
