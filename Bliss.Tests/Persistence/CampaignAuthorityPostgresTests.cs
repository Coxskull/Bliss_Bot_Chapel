using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class CampaignAuthorityPostgresFactAttribute : FactAttribute
{
    public CampaignAuthorityPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")))
            Skip = "Explicit disposable campaign PostgreSQL fixture required.";
    }
}

public sealed class CampaignAuthorityPostgresTests
{
    [CampaignAuthorityPostgresFact]
    public async Task Upgrade_marks_only_known_fixtures_and_database_rejects_unknown_authority()
    {
        var connection = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION")!;
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", builder.Host);
        var database = "campaign_authority_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(connection);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin))
            await create.ExecuteNonQueryAsync();
        try
        {
            builder.Database = database;
            var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using var db = new BlissDbContext(options);
            await db.Database.MigrateAsync();
            await new Phase1DataSeeder(db).SeedAsync();
            await new Phase2DataSeeder(db).SeedAsync();
            var unknown = Guid.NewGuid();
            db.Creators.Add(new Creator { Id = unknown, Name = "Unverified intake", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            var migrator = db.GetService<IMigrator>();
            var migrations = db.Database.GetMigrations().ToList();
            Assert.EndsWith("CampaignAuthorityLineage", migrations[^1]);
            await migrator.MigrateAsync(migrations[^2]);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(RecordAuthorityStatuses.Unverified, (await db.Creators.SingleAsync(x => x.Id == unknown)).AuthorityStatus);
            Assert.Equal(3, await db.Creators.CountAsync(x => x.AuthorityStatus == RecordAuthorityStatuses.SyntheticFixture));
            Assert.Equal(5, await db.AdvertiserOpportunities.CountAsync(x => x.AuthorityStatus == RecordAuthorityStatuses.SyntheticFixture));
            Assert.Equal(2, await db.Campaigns.CountAsync(x => x.AuthorityStatus == RecordAuthorityStatuses.SyntheticFixture));
            foreach (var statement in new[]
            {
                "UPDATE \"Creators\" SET \"AuthorityStatus\" = 'APPROVED_BY_REQUEST'",
                "UPDATE \"AdvertiserOpportunities\" SET \"AuthorityStatus\" = 'APPROVED_BY_REQUEST'",
                "UPDATE \"Campaigns\" SET \"AuthorityStatus\" = 'APPROVED_BY_REQUEST'"
            })
            {
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(statement));
                Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
            }
            var result = await new CampaignPlacementService(db).BindAsync(new("POSTGRES_FIXTURE", "fixture-plan", "Fixture operator",
                Phase2DataSeeder.MatchBrazilApprovedId, Phase2DataSeeder.Campaign2Id, Phase2DataSeeder.BrazilContentId,
                Guid.Parse("99999999-9999-9999-9999-999999999997")));
            Assert.Equal("PLANNED", result.Outcome);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}
