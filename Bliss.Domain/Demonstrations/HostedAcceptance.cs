namespace Bliss.Domain.Demonstrations;

public sealed record HostedReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string ReadingKey,
    string EnvironmentName,
    bool ProductionGatesApplied,
    bool HostedDatabaseConfigured,
    bool DatabaseServerCertificateVerified,
    bool IdentityProviderHttps,
    bool BackupDeclared,
    bool SecretMaterialExternal,
    bool RoleClaimsDistinct,
    bool HostedAcceptanceClaimed,
    bool IdentityContacted,
    bool BackupDrillRun,
    bool Duplicate);

/// <summary>
/// Records the process posture. Hosted acceptance is not claimed.
/// </summary>
public static class HostedAcceptance
{
    public const string Notice =
        "This reading records the process posture. Hosted acceptance is not claimed. A local database is not a hosted database. An identity provider was not contacted. A backup drill was not run. Green does not send. Delivery remains NOT_SENT.";

    public const string DuplicateNotice =
        "That hosted reading is already stored. Hosted acceptance was not claimed. Delivery remains NOT_SENT.";

    public static HostedReading Store(
        string? environmentName,
        bool productionGatesApplied,
        bool hostedDatabaseConfigured,
        bool databaseServerCertificateVerified,
        bool identityProviderHttps,
        bool backupDeclared,
        bool secretMaterialExternal,
        bool roleClaimsDistinct,
        bool claimHosted,
        string? readingKey,
        IReadOnlyList<string>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("The hosted reading history is required. None is invented.");
        }

        if (claimHosted)
        {
            throw new InvalidOperationException("Hosted acceptance is not on file. None was invented.");
        }

        var environment = RequireEnvironment(environmentName);
        RequireKey(readingKey);
        var key = readingKey!.Trim();
        var prior = existing.Any(item => string.Equals(item, key, StringComparison.Ordinal));
        return new HostedReading(
            prior ? DuplicateNotice : Notice,
            false,
            "NOT_SENT",
            key,
            environment,
            productionGatesApplied,
            hostedDatabaseConfigured,
            databaseServerCertificateVerified,
            identityProviderHttps,
            backupDeclared,
            secretMaterialExternal,
            roleClaimsDistinct,
            false,
            false,
            false,
            prior);
    }

    public static string RequireEnvironment(string? environmentName)
    {
        var environment = (environmentName ?? string.Empty).Trim();
        if (environment.Length == 0)
        {
            throw new InvalidOperationException("The process environment is required. None is invented.");
        }

        return environment;
    }

    public static void RequireKey(string? readingKey)
    {
        var key = (readingKey ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("A reading key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                throw new InvalidOperationException("A reading key is required. None is invented.");
            }
        }
    }
}
