using System.Data;
using System.Security.Authentication;
using Npgsql;

namespace Bliss.Api.Runtime;

public sealed record HostedDatabaseDiagnosticResult(
    bool Connected, bool TlsVerified, bool MigrationHistoryPresent,
    long PublicTableCount, string ErrorCode)
{
    // Connection/TLS and basic catalog presence are separate facts. A connected
    // but empty database is NOT a successful hosted database diagnostic.
    public bool ConnectionVerified => Connected && TlsVerified;
    public bool CatalogMetadataPresent => MigrationHistoryPresent && PublicTableCount > 0;
    public bool Success => ConnectionVerified && CatalogMetadataPresent && string.IsNullOrEmpty(ErrorCode);

    // This permits ONLY a deliberately locked, account-write-disabled interface
    // preview to redeploy while its separate hosted business DB is uninitialized.
    // The diagnostic Success above remains false, and full registration stays held.
    public bool AllowsLockedPreviewPredeploy(bool previewOnly) =>
        previewOnly && ConnectionVerified && !Success
        && ErrorCode is ("schema_not_initialized" or "migration_history_missing" or "public_tables_missing");

    // Basic catalog presence is a prerequisite, NOT proof the full application
    // schema, backup, authorization or hosted registration has been accepted.
    public static HostedDatabaseDiagnosticResult FromReadOnlyCatalog(
        bool tls, bool migrationHistory, long publicTables)
    {
        var code = !tls ? "tls_not_active"
            : !migrationHistory && publicTables == 0 ? "schema_not_initialized"
            : !migrationHistory ? "migration_history_missing"
            : publicTables == 0 ? "public_tables_missing"
            : "";
        return new(true, tls, migrationHistory, publicTables, code);
    }
}

// Console-only probe. Never starts HTTP, runs migrations, or reads customer rows.
public static class HostedDatabaseDiagnostic
{
    public static async Task<HostedDatabaseDiagnosticResult> RunAsync(string? connectionString)
    {
        var connected = false;
        try
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return Failure("connection_not_configured");
            var settings = new NpgsqlConnectionStringBuilder(connectionString);
            if (settings.SslMode != SslMode.VerifyFull
                || settings.TryGetValue("Trust Server Certificate", out var trust) && Convert.ToBoolean(trust))
                return Failure("verify_full_tls_required");
            settings.Timeout = 10;
            settings.CommandTimeout = 10;
            // No connection pooling: this standalone probe must leave no live session.
            settings.Pooling = false;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var connection = new NpgsqlConnection(settings.ConnectionString);
            await connection.OpenAsync(deadline.Token);
            connected = true;
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
            await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
                await readOnly.ExecuteNonQueryAsync(deadline.Token);
            const string query = """
                SELECT COALESCE((SELECT ssl FROM pg_stat_ssl WHERE pid=pg_backend_pid()), false),
                       to_regclass('public."__EFMigrationsHistory"') IS NOT NULL,
                       (SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname='public')
                """;
            await using var command = new NpgsqlCommand(query, connection, transaction);
            await using var result = await command.ExecuteReaderAsync(deadline.Token);
            if (!await result.ReadAsync(deadline.Token)) return Failure("no_diagnostic_result", connected);
            var tls = result.GetBoolean(0);
            return HostedDatabaseDiagnosticResult.FromReadOnlyCatalog(
                tls, result.GetBoolean(1), result.GetInt64(2));
            // Disposing the transaction rolls it back. Only catalog metadata was read.
        }
        catch (Exception error)
        {
            var code = error switch
            {
                ArgumentException => "invalid_connection_configuration",
                AuthenticationException => "tls_verification_failed",
                OperationCanceledException => "connection_timeout",
                PostgresException p when p.SqlState.Length == 5 && p.SqlState.All(char.IsAsciiLetterOrDigit) => "postgres_" + p.SqlState,
                NpgsqlException => "database_connection_failed",
                _ => "diagnostic_failed"
            };
            // Never print exception messages, endpoints, usernames, passwords or paths.
            return Failure(code, connected);
        }
    }

    private static HostedDatabaseDiagnosticResult Failure(string code, bool connected = false) => new(connected, false, false, 0, code);
}
