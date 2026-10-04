using System.Net;
using Bliss.Api.Security;
using Npgsql;

namespace Bliss.Api.Runtime;

public sealed record ProductionPostureDocument(
    bool ProductionGatesApplied,
    bool KeysEncryptedAtRest,
    bool HostedDatabaseConfigured,
    bool DatabaseTransportEncrypted,
    bool DatabaseServerCertificateVerified,
    bool IdentityProviderHttps,
    string RoleClaimType,
    bool RoleClaimsDistinct,
    IReadOnlyList<string> ConfiguredRoles,
    bool KnownProxiesConfigured,
    bool BackupDeclared,
    string BackupProvider,
    string BackupSchedule,
    int BackupRetentionDays,
    bool SecretMaterialExternal);

public sealed class ProductionPostureReport(
    ProductionPostureDocument document,
    IReadOnlyList<string> failures)
{
    public ProductionPostureDocument Document { get; } = document;
    public IReadOnlyList<string> Failures { get; } = failures;
}

public sealed record ProductionPostureInput
{
    public bool IsDevelopment { get; init; }
    public bool AuthenticationEnabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string Authority { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string RoleClaimType { get; init; } = string.Empty;
    public string ViewerRole { get; init; } = string.Empty;
    public string OperatorRole { get; init; } = string.Empty;
    public string ReviewerRole { get; init; } = string.Empty;
    public string AdminRole { get; init; } = string.Empty;
    public string AdvertiserRole { get; init; } = string.Empty;
    public string DataProtectionKeysPath { get; init; } = string.Empty;
    public string DataProtectionCertificatePath { get; init; } = string.Empty;
    public string DataProtectionCertificatePassword { get; init; } = string.Empty;
    public bool CertificateFileExists { get; init; }
    public string[] KnownProxies { get; init; } = [];
    public string BackupProvider { get; init; } = string.Empty;
    public string BackupSchedule { get; init; } = string.Empty;
    public int BackupRetentionDays { get; init; }
    public string CommittedConnectionString { get; init; } = string.Empty;
    public string CommittedClientSecret { get; init; } = string.Empty;
    public string CommittedCertificatePassword { get; init; } = string.Empty;
}

public static class ProductionPostureEvaluator
{
    private static readonly HashSet<string> PlaceholderSecrets = new(StringComparer.OrdinalIgnoreCase)
    {
        "changeme",
        "secret",
        "password",
        "placeholder",
        "test",
        "your-client-secret",
        "oidc_client_secret",
        "<oidc_client_secret>"
    };

    public static ProductionPostureInput CreateInput(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        BlissAuthenticationOptions authentication,
        BlissRuntimeOptions runtime)
    {
        var committed = ReadCommittedSettings(environment.ContentRootPath, environment.EnvironmentName);
        var certificatePath = runtime.DataProtectionCertificatePath?.Trim() ?? string.Empty;
        var backup = runtime.Backup ?? new BackupDeclarationOptions();
        return new ProductionPostureInput
        {
            IsDevelopment = environment.IsDevelopment(),
            AuthenticationEnabled = authentication.Enabled,
            ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
            Authority = authentication.Authority ?? string.Empty,
            ClientId = authentication.ClientId ?? string.Empty,
            ClientSecret = authentication.ClientSecret ?? string.Empty,
            RoleClaimType = authentication.RoleClaimType ?? string.Empty,
            ViewerRole = authentication.ViewerRole ?? string.Empty,
            OperatorRole = authentication.OperatorRole ?? string.Empty,
            ReviewerRole = authentication.ReviewerRole ?? string.Empty,
            AdminRole = authentication.AdminRole ?? string.Empty,
            AdvertiserRole = authentication.AdvertiserRole ?? string.Empty,
            DataProtectionKeysPath = runtime.DataProtectionKeysPath ?? string.Empty,
            DataProtectionCertificatePath = certificatePath,
            DataProtectionCertificatePassword = runtime.DataProtectionCertificatePassword ?? string.Empty,
            CertificateFileExists = IsCertificateFile(certificatePath),
            KnownProxies = runtime.KnownProxies ?? [],
            BackupProvider = backup.Provider ?? string.Empty,
            BackupSchedule = backup.Schedule ?? string.Empty,
            BackupRetentionDays = backup.RetentionDays,
            CommittedConnectionString = committed.ConnectionString,
            CommittedClientSecret = committed.ClientSecret,
            CommittedCertificatePassword = committed.CertificatePassword
        };
    }

    public static ProductionPostureReport Evaluate(ProductionPostureInput input)
    {
        var failures = new List<string>();
        var secretMaterialExternal = AddCommittedSecretFailures(input, failures);
        var database = InspectDatabase(input.ConnectionString);
        var identityProviderHttps = IsHostedHttps(input.Authority);
        var configuredRoles = ConfiguredRoles(input);
        var roleClaimsDistinct = AreRolesDistinct(input, configuredRoles);
        var keysEncryptedAtRest = InspectKeys(input, failures);
        var knownProxiesConfigured = HasDeploymentProxy(input.KnownProxies, failures, input.IsDevelopment);
        var backupDeclared = IsBackupDeclared(input);

        if (!input.IsDevelopment)
        {
            if (!input.AuthenticationEnabled)
            {
                failures.Add(
                    "OIDC authentication must be enabled outside Development.");
            }

            AddDatabaseFailures(input, database, failures);
            if (!identityProviderHttps)
            {
                failures.Add(
                    "Authentication:Authority must be an https URL for a non-loopback identity provider outside Development.");
            }

            if (string.IsNullOrWhiteSpace(input.ClientId))
            {
                failures.Add("Authentication:ClientId is required outside Development.");
            }

            if (!IsSecretOk(input.ClientSecret, 16))
            {
                failures.Add(
                    "Authentication:ClientSecret must be supplied through the secret store and must not be a placeholder.");
            }

            if (!roleClaimsDistinct)
            {
                failures.Add("Authentication role names must be distinct, non-empty values.");
            }

            if (string.IsNullOrWhiteSpace(input.DataProtectionKeysPath))
            {
                failures.Add(
                    "Runtime:DataProtectionKeysPath is required outside Development so OIDC sessions survive restarts and replicas.");
            }

            if (string.IsNullOrWhiteSpace(input.DataProtectionCertificatePath))
            {
                failures.Add(
                    "Runtime:DataProtectionCertificatePath is required outside Development so persisted keys are encrypted at rest.");
            }

            if (!IsSecretOk(input.DataProtectionCertificatePassword, 12))
            {
                failures.Add(
                    "Runtime:DataProtectionCertificatePassword must be supplied through the secret store.");
            }

            if (!knownProxiesConfigured)
            {
                failures.Add(
                    "Runtime:KnownProxies must include at least one non-loopback deployment proxy outside Development.");
            }

            if (!backupDeclared)
            {
                failures.Add(
                    "Runtime:Backup:Provider, Runtime:Backup:Schedule, and a positive Runtime:Backup:RetentionDays are required outside Development.");
            }
        }

        var document = new ProductionPostureDocument(
            !input.IsDevelopment && failures.Count == 0,
            keysEncryptedAtRest,
            database.Hosted,
            database.TransportEncrypted,
            database.ServerCertificateVerified,
            identityProviderHttps,
            input.RoleClaimType.Trim(),
            roleClaimsDistinct,
            configuredRoles,
            knownProxiesConfigured,
            backupDeclared,
            input.BackupProvider.Trim(),
            input.BackupSchedule.Trim(),
            input.BackupRetentionDays,
            secretMaterialExternal);
        return new ProductionPostureReport(document, failures);
    }

    public static void Ensure(ProductionPostureReport report)
    {
        if (report.Failures.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Production posture is incomplete. " + string.Join(" ", report.Failures));
    }

    public static (string ConnectionString, string ClientSecret, string CertificatePassword) ReadCommittedSettings(
        string contentRoot,
        string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(string.IsNullOrWhiteSpace(contentRoot) ? Directory.GetCurrentDirectory() : contentRoot)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .Build();
        return (
            configuration.GetConnectionString("DefaultConnection") ?? string.Empty,
            configuration["Authentication:ClientSecret"] ?? string.Empty,
            configuration["Runtime:DataProtectionCertificatePassword"] ?? string.Empty);
    }

    private static bool AddCommittedSecretFailures(ProductionPostureInput input, List<string> failures)
    {
        var clean = true;
        if (!string.IsNullOrWhiteSpace(input.CommittedConnectionString))
        {
            clean = false;
            failures.Add("ConnectionStrings:DefaultConnection must not be stored in appsettings.");
        }

        if (!string.IsNullOrWhiteSpace(input.CommittedClientSecret))
        {
            clean = false;
            failures.Add("Authentication:ClientSecret must not be stored in appsettings.");
        }

        if (!string.IsNullOrWhiteSpace(input.CommittedCertificatePassword))
        {
            clean = false;
            failures.Add("Runtime:DataProtectionCertificatePassword must not be stored in appsettings.");
        }

        return clean;
    }

    private static void AddDatabaseFailures(
        ProductionPostureInput input,
        DatabaseInspection database,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(input.ConnectionString))
        {
            failures.Add(
                "ConnectionStrings:DefaultConnection is required outside Development and must be supplied through the secret store.");
            return;
        }

        if (!database.Parsed)
        {
            failures.Add("ConnectionStrings:DefaultConnection could not be parsed.");
            return;
        }

        if (!database.Hosted)
        {
            failures.Add(
                "The database host must be a hosted address outside Development. Loopback hosts are rejected.");
        }

        if (!database.TransportEncrypted)
        {
            failures.Add(
                "The database connection must set SSL Mode to VerifyFull outside Development.");
        }
        else if (!database.ServerCertificateVerified)
        {
            failures.Add(
                "The database connection must set SSL Mode to VerifyFull outside Development. Trust Server Certificate must stay false.");
        }

        if (!database.UsernamePresent)
        {
            failures.Add("The database username is required outside Development.");
        }

        if (!database.PasswordOk)
        {
            failures.Add("The database password must be supplied through the secret store.");
        }

        if (!database.DatabasePresent)
        {
            failures.Add("The database name is required outside Development.");
        }
    }

    private static DatabaseInspection InspectDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return DatabaseInspection.Empty;
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var hosted = !IsLoopbackHost(builder.Host)
                && !string.IsNullOrWhiteSpace(builder.Username)
                && IsSecretOk(builder.Password, 12)
                && !string.IsNullOrWhiteSpace(builder.Database);
            var transportEncrypted = builder.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull;
            var verified = builder.SslMode == SslMode.VerifyFull && !builder.TrustServerCertificate;
            return new DatabaseInspection(true, hosted, transportEncrypted, verified,
                !string.IsNullOrWhiteSpace(builder.Username),
                IsSecretOk(builder.Password, 12),
                !string.IsNullOrWhiteSpace(builder.Database));
        }
        catch (ArgumentException)
        {
            return DatabaseInspection.Unparsed;
        }
    }

    private static bool InspectKeys(ProductionPostureInput input, List<string> failures)
    {
        var path = input.DataProtectionCertificatePath?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(path) && !input.CertificateFileExists)
        {
            failures.Add(
                "Runtime:DataProtectionCertificatePath does not point to a readable PKCS#12 file.");
        }

        if (input.IsDevelopment
            && !string.IsNullOrWhiteSpace(path)
            && !IsSecretOk(input.DataProtectionCertificatePassword, 12))
        {
            failures.Add(
                "Runtime:DataProtectionCertificatePassword must be supplied through the secret store.");
        }

        return !string.IsNullOrWhiteSpace(input.DataProtectionKeysPath)
            && !string.IsNullOrWhiteSpace(path)
            && input.CertificateFileExists
            && IsSecretOk(input.DataProtectionCertificatePassword, 12);
    }

    private static bool HasDeploymentProxy(string[] proxies, List<string> failures, bool isDevelopment)
    {
        var configured = false;
        foreach (var value in proxies ?? [])
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!IPAddress.TryParse(value.Trim(), out var address))
            {
                if (!isDevelopment)
                {
                    failures.Add("Runtime:KnownProxies contains an invalid IP address.");
                }

                continue;
            }

            if (!IPAddress.IsLoopback(address)
                && !address.Equals(IPAddress.Any)
                && !address.Equals(IPAddress.IPv6Any))
            {
                configured = true;
            }
        }

        return configured;
    }

    private static bool IsBackupDeclared(ProductionPostureInput input)
    {
        var provider = input.BackupProvider.Trim();
        var schedule = input.BackupSchedule.Trim();
        if (provider.Length is 0 or > 64 || schedule.Length is 0 or > 64)
        {
            return false;
        }

        if (provider.Contains("://", StringComparison.Ordinal)
            || provider.Contains(';', StringComparison.Ordinal)
            || provider.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return input.BackupRetentionDays is >= 1 and <= 3650;
    }

    private static List<string> ConfiguredRoles(ProductionPostureInput input)
    {
        return new[]
        {
            input.ViewerRole,
            input.OperatorRole,
            input.ReviewerRole,
            input.AdminRole,
            input.AdvertiserRole
        }.Select(role => role.Trim())
        .Where(role => role.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToList();
    }

    private static bool AreRolesDistinct(ProductionPostureInput input, IReadOnlyList<string> configuredRoles)
    {
        var raw = new[]
        {
            input.ViewerRole,
            input.OperatorRole,
            input.ReviewerRole,
            input.AdminRole,
            input.AdvertiserRole
        };
        if (raw.Any(role => string.IsNullOrWhiteSpace(role) || role != role.Trim()))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(input.RoleClaimType) || input.RoleClaimType != input.RoleClaimType.Trim())
        {
            return false;
        }

        return configuredRoles.Count == raw.Length
            && configuredRoles.Distinct(StringComparer.OrdinalIgnoreCase).Count() == raw.Length;
    }

    private static bool IsHostedHttps(string authority)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && !IsLoopbackHost(uri.Host);
    }

    private static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var normalized = host.Trim().Trim('[', ']');
        return normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("::1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("0.0.0.0", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCertificateFile(string path) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path) && !Directory.Exists(path);

    private static bool IsSecretOk(string? value, int minimumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length >= minimumLength && !PlaceholderSecrets.Contains(trimmed);
    }

    private readonly record struct DatabaseInspection(
        bool Parsed,
        bool Hosted,
        bool TransportEncrypted,
        bool ServerCertificateVerified,
        bool UsernamePresent,
        bool PasswordOk,
        bool DatabasePresent)
    {
        public static DatabaseInspection Empty { get; } = new(false, false, false, false, false, false, false);
        public static DatabaseInspection Unparsed { get; } = new(false, false, false, false, false, false, false);
    }
}
