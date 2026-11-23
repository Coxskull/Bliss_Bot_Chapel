using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Runtime;

/// <summary>
/// Single additive Task #4 migration for the EXISTING Railway PostgreSQL 18.
/// A production process must complete the separate recovery-evidence gate before
/// calling this method. No HTTP listener, new DB, signup or preview unlock.
/// </summary>
public static class CreatorIncrementalMigration
{
    public static async Task<bool> ApplyOnceAsync(
        BlissDbContext db, string connectionString, HostedDatabaseDiagnosticResult before)
    {
        if (!before.Success || !before.MigrationHistoryPresent || before.PublicTableCount < 90)
            throw new InvalidOperationException("Populated preexisting EF schema must be verified.");

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        if (pending.Length == 0)
        {
            if (!await VerifyCreatorSchemaAsync(connectionString))
                throw new InvalidOperationException("No pending migrations, but creator schema is not verified.");
            return false;
        }

        // EF Migrate() would apply EVERY pending migration. Refuse anything
        // except the one reviewed, non-destructive creator table migration.
        if (pending.Length != 1 || pending[0] != MigrationCommand.CreatorMigrationId)
            throw new InvalidOperationException("Unexpected pending migrations; no database changes.");
        if (await CreatorTablesExistAsync(connectionString))
            throw new InvalidOperationException("Creator objects already exist without the expected migration record.");

        await db.Database.MigrateAsync();

        var after = await HostedDatabaseDiagnostic.RunAsync(connectionString);
        if (!after.Success || after.PublicTableCount != before.PublicTableCount + 2
            || !await VerifyCreatorSchemaAsync(connectionString))
            throw new InvalidOperationException("Creator migration postcondition was not verified.");
        return true;
    }

    private static async Task<bool> CreatorTablesExistAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT to_regclass('public."CreatorAccountMemberships"') IS NOT NULL
                OR to_regclass('public."CreatorRegistrationReceipts"') IS NOT NULL
            """, connection);
        command.CommandTimeout = 10;
        return (bool)(await command.ExecuteScalarAsync() ?? true);
    }

    public static async Task<bool> VerifyCreatorSchemaAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        // Read only catalog objects. No applicant/customer records are read.
        await using var command = new NpgsqlCommand("""
            SELECT
                to_regclass('public."CreatorAccountMemberships"') IS NOT NULL
                AND to_regclass('public."CreatorRegistrationReceipts"') IS NOT NULL
                AND EXISTS (
                    SELECT 1 FROM public."__EFMigrationsHistory"
                    WHERE "MigrationId" = '20261107100000_SecureCreatorRegistration'
                )
                AND EXISTS (
                    SELECT 1 FROM pg_catalog.pg_trigger
                    WHERE tgrelid = to_regclass('public."CreatorRegistrationReceipts"')
                      AND tgname = 'creator_registration_receipts_immutable'
                      AND NOT tgisinternal
                )
                AND EXISTS (
                    SELECT 1 FROM pg_catalog.pg_indexes
                    WHERE schemaname='public' AND tablename='CreatorAccountMemberships'
                      AND indexname='IX_CreatorAccountMemberships_Subject'
                )
                AND EXISTS (
                    SELECT 1 FROM pg_catalog.pg_indexes
                    WHERE schemaname='public' AND tablename='CreatorRegistrationReceipts'
                      AND indexname='IX_CreatorRegistrationReceipts_RequestKey'
                )
                AND EXISTS (
                    SELECT 1 FROM pg_catalog.pg_class
                    WHERE oid=to_regclass('public."CreatorAccountMemberships"')
                      AND relrowsecurity
                )
                AND EXISTS (
                    SELECT 1 FROM pg_catalog.pg_class
                    WHERE oid=to_regclass('public."CreatorRegistrationReceipts"')
                      AND relrowsecurity
                )
            """, connection);
        command.CommandTimeout = 10;
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }
}
