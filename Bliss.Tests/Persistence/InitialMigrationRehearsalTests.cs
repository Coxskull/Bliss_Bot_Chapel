using Bliss.Domain.Entities;
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
            $"PASS: {expected.Length} migrations; model columns present; synthetic write/read rolled back; second migration no-op. Disposable CI only; hosted acceptance not claimed.\n");
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
