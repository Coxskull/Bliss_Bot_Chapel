using System.Data;
using System.Security.Authentication;
using Npgsql;

namespace Bliss.Api.Runtime;

public sealed record HostedDatabaseDiagnosticResult(
    bool Connected, bool TlsVerified, bool MigrationHistoryPresent,
    long PublicTableCount, string ErrorCode)
{
    public bool Success => Connected && TlsVerified;
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
            return new(connected, tls, result.GetBoolean(1), result.GetInt64(2), tls ? "" : "tls_not_active");
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
