using Npgsql;

namespace Bliss.Api.Runtime;

public static class MigrationCommand
{
    public const string Argument = "--migrate";
    // A separate, explicit path that never exposes any hosted HTTP routes.
    public const string InitialInstallArgument = "--initial-install-empty-hosted-db";
    public const string CompletionLine =
        "Migrations applied. Hosted acceptance was not claimed. Delivery remains NOT_SENT.";
    public const string InitialInstallCompletionLine =
        "Initial schema installed. Restricted preview still required. Delivery remains NOT_SENT.";

    public static bool Requested(string[] args) =>
        args.Any(argument => string.Equals(argument, Argument, StringComparison.Ordinal));

    public static bool InitialInstallRequested(string[] args) =>
        args.Length == 2
        && args.Contains(Argument, StringComparer.Ordinal)
        && args.Contains(InitialInstallArgument, StringComparer.Ordinal);

    public static void ValidateInitialEmptyHostedInstall(
        bool restrictedPreview,
        bool isProduction,
        string? connectionString,
        HostedDatabaseDiagnosticResult diagnostic,
        string? expectedHost,
        string? expectedDatabase,
        string? backupEvidenceId)
    {
        if (!restrictedPreview || !isProduction)
            throw new InvalidOperationException("Initial install requires Production restricted-preview mode.");
        if (!diagnostic.Connected || !diagnostic.TlsVerified
            || diagnostic.MigrationHistoryPresent || diagnostic.PublicTableCount != 0)
            throw new InvalidOperationException("Initial install refused: TLS-verified empty schema not established.");
        if (string.IsNullOrWhiteSpace(backupEvidenceId)
            || backupEvidenceId.Trim().Length < 8
            || string.IsNullOrWhiteSpace(expectedHost)
            || string.IsNullOrWhiteSpace(expectedDatabase)
            || string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Initial install requires verified backup evidence and explicit database identity.");

        NpgsqlConnectionStringBuilder actual;
        try { actual = new NpgsqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Initial install refused: invalid database configuration.");
        }
        if (actual.SslMode != SslMode.VerifyFull
            || actual.TrustServerCertificate
            || !string.Equals(actual.Host.Trim().TrimEnd('.'),
                expectedHost.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(actual.Database, expectedDatabase.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Initial install refused: target identity or TLS mode mismatch.");
    }
}
