using Npgsql;

namespace Bliss.Api.Runtime;

public static class MigrationCommand
{
    public const string Argument = "--migrate";
    // A separate, explicit path that never exposes any hosted HTTP routes.
    public const string InitialInstallArgument = "--initial-install-empty-hosted-db";
    public const string CreatorIncrementalArgument = "--task4-creator-incremental";
    public const string CreatorIncrementalCompletionLine =
        "TASK4_CREATOR_INCREMENTAL_SCHEMA=PASS_PREVIEW_AND_ADVERTISER_UNCHANGED";
    public const string CreatorIncrementalAlreadyPresentLine =
        "TASK4_CREATOR_INCREMENTAL_SCHEMA=ALREADY_PRESENT_VERIFIED";
    public const string CreatorMigrationId = "20261107100000_SecureCreatorRegistration";
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
    public static bool CreatorIncrementalRequested(string[] args) =>
        args.Length == 2
        && args.Contains(Argument, StringComparer.Ordinal)
        && args.Contains(CreatorIncrementalArgument, StringComparer.Ordinal);

    public static void ValidateCreatorIncrementalHostedInstall(
        bool restrictedPreview, bool isProduction, bool advertiserRegistrationEnabled,
        bool creatorRegistrationEnabled, string? connectionString,
        HostedDatabaseDiagnosticResult diagnostic, string? expectedHost,
        string? expectedDatabase, string? recoveryManifestSha256)
    {
        // This mode is NOT initial schema installation and must preserve the
        // existing advertiser signup lane while creator signup remains OFF.
        if (!restrictedPreview || !isProduction || !advertiserRegistrationEnabled
            || creatorRegistrationEnabled)
            throw new InvalidOperationException("Creator migration requires locked Production, live advertiser registration and creator signup OFF.");
        if (!diagnostic.Success || diagnostic.PublicTableCount < 90
            || !diagnostic.MigrationHistoryPresent)
            throw new InvalidOperationException("Creator migration refused: populated TLS-verified EF catalog required.");
        if (string.IsNullOrWhiteSpace(recoveryManifestSha256)
            || recoveryManifestSha256.Length != 64
            || recoveryManifestSha256.Any(c => (c < '0' || c > '9') && (c < 'a' || c > 'f')))
            throw new InvalidOperationException("Creator migration requires a 64-hex recent recovery evidence receipt.");
        if (string.IsNullOrWhiteSpace(expectedHost)
            || string.IsNullOrWhiteSpace(expectedDatabase)
            || string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Creator migration requires an explicit pinned database identity.");
        NpgsqlConnectionStringBuilder actual;
        try { actual = new NpgsqlConnectionStringBuilder(connectionString); }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("Creator migration refused: invalid database settings.");
        }
        if (actual.SslMode != SslMode.VerifyFull || actual.TrustServerCertificate
            || !string.Equals(actual.Host.Trim().TrimEnd('.'),
                expectedHost.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(actual.Database, expectedDatabase.Trim(), StringComparison.Ordinal)
            || !string.Equals(actual.Database, "bliss", StringComparison.Ordinal))
            throw new InvalidOperationException("Creator migration refused: target database or VerifyFull TLS mismatch.");
    }

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
