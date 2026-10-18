using Bliss.Api.Runtime;
using Bliss.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Bliss.Tests.Persistence;

public sealed class InitialMigrationRehearsalTests
{
    private const string DisposableConnection =
        "Host=127.0.0.1;Port=5432;Database=bliss_initial_migration_ci;Username=postgres;Password=postgres";

    [InitialMigrationPostgresFact]
    public async Task Reviewed_idempotent_SQL_initializes_a_separate_empty_database_and_replays()
    {
        var configured = Environment.GetEnvironmentVariable("BLISS_INITIAL_MIGRATION_CI");
        Assert.Equal(DisposableConnection, configured);
        await using (var admin = new NpgsqlConnection(configured))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = "CREATE DATABASE bliss_initial_script_ci";
            await create.ExecuteNonQueryAsync();
        }
        var scriptConnection = new NpgsqlConnectionStringBuilder(configured) { Database = "bliss_initial_script_ci" };
        var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scriptConnection.ConnectionString).Options;
        await using var db = new BlissDbContext(options);
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        await using var connection = new NpgsqlConnection(scriptConnection.ConnectionString);
        await connection.OpenAsync();
        // Execute the exact exported SQL path, rather than assuming it behaves like MigrateAsync.
        for (var iteration = 0; iteration < 2; iteration++)
        {
            await using var apply = connection.CreateCommand();
            apply.CommandText = script;
            apply.CommandTimeout = 120;
            await apply.ExecuteNonQueryAsync();
            Assert.Equal(db.Database.GetMigrations().ToArray(), (await db.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Empty(await db.Advertisers.AsNoTracking().ToListAsync());
        }
        var output = Path.Combine(Directory.GetCurrentDirectory(), "MigrationEvidence");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "sql-replay-result.txt"),
            "PASS: exported idempotent SQL applied twice to a separate disposable database; migration history matches; application query succeeds. Hosted database unchanged.\n");
    }

    [InitialMigrationPostgresFact]
    public async Task Empty_database_migrates_matches_model_and_supports_rolled_back_application_write()
    {
        var configured = Environment.GetEnvironmentVariable("BLISS_INITIAL_MIGRATION_CI");
        // Never accept a hosted connection, even if accidentally supplied by CI.
        Assert.Equal(DisposableConnection, configured);
        var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(configured).Options;
        await using var db = new BlissDbContext(options);
        await db.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using (var empty = connection.CreateCommand())
        {
            empty.CommandText = "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema') AND table_type='BASE TABLE'";
            Assert.Equal(0L, (long)(await empty.ExecuteScalarAsync())!);
        }

        await using var services = new ServiceCollection()
            .AddDbContext<BlissDbContext>(builder => builder.UseNpgsql(configured))
            .BuildServiceProvider();
        var readiness = new DatabaseReadinessHealthCheck(services.GetRequiredService<IServiceScopeFactory>());
        var before = await readiness.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, before.Status);
        Assert.Equal("Application database migrations are pending.", before.Description);

        var output = Path.Combine(Directory.GetCurrentDirectory(), "MigrationEvidence");
        Directory.CreateDirectory(output);
        var migrator = db.GetService<IMigrator>();
        await File.WriteAllTextAsync(Path.Combine(output, "initial-idempotent.sql"),
            migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent));
        var expected = db.Database.GetMigrations().ToArray();
        Assert.NotEmpty(expected);
        await db.Database.MigrateAsync();
        Assert.Equal(expected, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(HealthStatus.Healthy, (await readiness.CheckHealthAsync(new HealthCheckContext())).Status);

        // A successful migration history is insufficient if the model has drifted.
        foreach (var table in db.Model.GetRelationalModel().Tables)
        {
            await using var columns = connection.CreateCommand();
            columns.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema=@schema AND table_name=@table";
            columns.Parameters.AddWithValue("schema", table.Schema ?? "public");
            columns.Parameters.AddWithValue("table", table.Name);
            var actual = new HashSet<string>(StringComparer.Ordinal);
            await using (var reader = await columns.ExecuteReaderAsync())
                while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
            foreach (var column in table.Columns)
                Assert.True(actual.Contains(column.Name), $"Missing migrated column {table.Schema ?? "public"}.{table.Name}.{column.Name}");
        }

        var id = Guid.NewGuid();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Advertisers.Add(new Advertiser { Id = id, Name = "SYNTHETIC MIGRATION REHEARSAL", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            Assert.True(await db.Advertisers.AsNoTracking().AnyAsync(x => x.Id == id));
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.False(await db.Advertisers.AnyAsync(x => x.Id == id));
        // Re-running startup migration must be a no-op, not a duplicate-schema failure.
        await db.Database.MigrateAsync();
        Assert.Equal(expected, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        await File.WriteAllTextAsync(Path.Combine(output, "result.txt"),
            $"PASS: {expected.Length} migrations; model columns present; synthetic write/read rolled back; second migration no-op; readiness rejects empty schema and accepts migrated schema. Disposable CI only; hosted acceptance not claimed.\n");
    }
}

public sealed class InitialMigrationPostgresFactAttribute : FactAttribute
{
    public InitialMigrationPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BLISS_INITIAL_MIGRATION_CI")))
            Skip = "Requires the explicitly configured disposable initial-migration fixture.";
    }
}
