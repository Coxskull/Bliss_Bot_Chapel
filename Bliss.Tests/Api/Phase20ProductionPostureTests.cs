using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bliss.Tests.Api;

public sealed class Phase20ProductionPostureEvaluatorTests
{
    [Fact]
    public void Development_does_not_require_hosted_production_settings()
    {
        var report = ProductionPostureEvaluator.Evaluate(ValidProductionInput() with { IsDevelopment = true });

        Assert.Empty(report.Failures);
        Assert.False(report.Document.ProductionGatesApplied);
        Assert.True(report.Document.SecretMaterialExternal);
    }

    [Fact]
    public void Production_accepts_a_hosted_ssl_identity_and_encrypted_key_declaration()
    {
        var report = ProductionPostureEvaluator.Evaluate(ValidProductionInput());

        ProductionPostureEvaluator.Ensure(report);
        Assert.True(report.Document.ProductionGatesApplied);
        Assert.True(report.Document.KeysEncryptedAtRest);
        Assert.True(report.Document.HostedDatabaseConfigured);
        Assert.True(report.Document.DatabaseTransportEncrypted);
        Assert.True(report.Document.DatabaseServerCertificateVerified);
        Assert.True(report.Document.IdentityProviderHttps);
        Assert.True(report.Document.RoleClaimsDistinct);
        Assert.Equal("roles", report.Document.RoleClaimType);
        Assert.Contains("bliss.admin", report.Document.ConfiguredRoles);
        Assert.True(report.Document.KnownProxiesConfigured);
        Assert.True(report.Document.BackupDeclared);
        Assert.Equal(14, report.Document.BackupRetentionDays);
    }

    [Theory]
    [InlineData("SSL Mode=Require")]
    [InlineData("SSL Mode=VerifyCA")]
    [InlineData("SSL Mode=VerifyFull;Trust Server Certificate=true")]
    public void Production_rejects_database_tls_that_does_not_verify_the_server_name(string ssl)
    {
        var report = ProductionPostureEvaluator.Evaluate(ValidProductionInput() with
        {
            ConnectionString =
                "Host=db.phase20.example.test;Port=5432;Database=bliss;Username=bliss_app;Password=phase20-db-password-do-not-leak;"
                + ssl
        });

        var exception = Assert.Throws<InvalidOperationException>(() => ProductionPostureEvaluator.Ensure(report));
        Assert.Contains("VerifyFull", exception.Message);
        Assert.False(report.Document.DatabaseServerCertificateVerified);
        Assert.DoesNotContain("phase20-db-password-do-not-leak", exception.Message);
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=bliss;Username=bliss_app;Password=phase20-db-password-do-not-leak;SSL Mode=Require", "Loopback")]
    [InlineData("Host=127.0.0.1;Port=5432;Database=bliss;Username=bliss_app;Password=phase20-db-password-do-not-leak;SSL Mode=Require", "Loopback")]
    [InlineData("Host=db.phase20.example.test;Port=5432;Database=bliss;Username=bliss_app;Password=phase20-db-password-do-not-leak;SSL Mode=Disable", "SSL Mode")]
    [InlineData("Host=db.phase20.example.test;Port=5432;Database=bliss;Username=bliss_app;Password=short;SSL Mode=Require", "secret store")]
    public void Production_rejects_unsafe_database_settings(string connectionString, string expected)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { ConnectionString = connectionString })));

        Assert.Contains("Production posture is incomplete", exception.Message);
        Assert.Contains(expected, exception.Message);
        Assert.DoesNotContain("phase20-db-password-do-not-leak", exception.Message);
        Assert.DoesNotContain("short", exception.Message);
    }

    [Fact]
    public void Production_rejects_a_missing_database_password_without_echoing_it()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { ConnectionString = "" })));

        Assert.Contains("ConnectionStrings:DefaultConnection is required", exception.Message);
        Assert.DoesNotContain("phase20", exception.Message);
    }

    [Theory]
    [InlineData("http://identity.example.test")]
    [InlineData("https://localhost/realms/bliss")]
    [InlineData("https://127.0.0.1/realms/bliss")]
    public void Production_rejects_a_non_hosted_identity_provider(string authority)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { Authority = authority })));

        Assert.Contains("Authentication:Authority", exception.Message);
        Assert.DoesNotContain("phase20-client-secret", exception.Message);
    }

    [Fact]
    public void Production_rejects_placeholder_and_committed_secrets()
    {
        var placeholder = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { ClientSecret = "changeme" })));
        Assert.Contains("ClientSecret", placeholder.Message);
        Assert.DoesNotContain("changeme", placeholder.Message);

        var committed = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { CommittedClientSecret = "from-file-secret-value" })));
        Assert.Contains("must not be stored in appsettings", committed.Message);
        Assert.DoesNotContain("from-file-secret-value", committed.Message);
    }

    [Fact]
    public void Production_rejects_duplicate_roles_missing_backup_and_loopback_proxies()
    {
        var roles = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { OperatorRole = "bliss.viewer" })));
        Assert.Contains("distinct", roles.Message);

        var backup = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { BackupProvider = "password=hunter2" })));
        Assert.Contains("Runtime:Backup", backup.Message);
        Assert.DoesNotContain("hunter2", backup.Message);

        var proxy = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with { KnownProxies = ["127.0.0.1"] })));
        Assert.Contains("KnownProxies", proxy.Message);
    }

    [Fact]
    public void Production_rejects_a_key_ring_that_is_not_encrypted()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionPostureEvaluator.Ensure(ProductionPostureEvaluator.Evaluate(
                ValidProductionInput() with
                {
                    DataProtectionCertificatePath = "",
                    CertificateFileExists = false
                })));

        Assert.Contains("encrypted at rest", exception.Message);
        Assert.DoesNotContain("phase20-cert-password", exception.Message);
    }

    [Fact]
    public void Committed_appsettings_do_not_contain_secrets()
    {
        var root = RepoRoot();
        foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var path = Path.Combine(root, "Bliss.Api", name);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var connection = document.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
            Assert.True(string.IsNullOrEmpty(connection));
            if (document.RootElement.TryGetProperty("Authentication", out var authentication)
                && authentication.TryGetProperty("ClientSecret", out var secret))
            {
                Assert.True(string.IsNullOrEmpty(secret.GetString()));
            }

            if (document.RootElement.TryGetProperty("Runtime", out var runtime)
                && runtime.TryGetProperty("DataProtectionCertificatePassword", out var password))
            {
                Assert.True(string.IsNullOrEmpty(password.GetString()));
            }
        }
    }

    [Fact]
    public void Certificate_loader_rejects_a_wrong_password_without_echoing_it()
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-cert-" + Guid.NewGuid().ToString("N")));
        var path = Path.Combine(directory.FullName, "dp.pfx");
        try
        {
            TestCertificate.Write(path, "phase20-cert-password-do-not-leak");
            var exception = Assert.Throws<InvalidOperationException>(() =>
                DataProtectionCertificateLoader.Load(path, "wrong-password-value"));
            Assert.Contains("could not be loaded", exception.Message);
            Assert.DoesNotContain("wrong-password-value", exception.Message);
            Assert.DoesNotContain("phase20-cert-password", exception.Message);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task Request_log_records_the_path_and_omits_the_query()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/health/live";
        context.Request.QueryString = new QueryString("?access_token=super-secret-value");
        context.Response.StatusCode = StatusCodes.Status200OK;
        var logger = new CollectingLogger();

        await RequestCompletionLog.InvokeAsync(context, logger, _ => Task.CompletedTask);

        var message = Assert.Single(logger.Messages);
        Assert.Contains("GET", message);
        Assert.Contains("/health/live", message);
        Assert.Contains("200", message);
        Assert.DoesNotContain("super-secret-value", message);
        Assert.DoesNotContain("access_token", message);
    }

    private static ProductionPostureInput ValidProductionInput() => new()
    {
        IsDevelopment = false,
        AuthenticationEnabled = true,
        ConnectionString = "Host=db.phase20.example.test;Port=5432;Database=bliss;Username=bliss_app;Password=phase20-db-password-do-not-leak;SSL Mode=VerifyFull",
        Authority = "https://identity.example.test/realms/bliss",
        ClientId = "bliss-chapel",
        ClientSecret = "phase20-client-secret-do-not-leak",
        RoleClaimType = "roles",
        ViewerRole = "bliss.viewer",
        OperatorRole = "bliss.operator",
        ReviewerRole = "bliss.reviewer",
        AdminRole = "bliss.admin",
        AdvertiserRole = "bliss.advertiser",
        DataProtectionKeysPath = "/var/lib/bliss/data-protection",
        DataProtectionCertificatePath = "/var/lib/bliss/data-protection.pfx",
        DataProtectionCertificatePassword = "phase20-cert-password-do-not-leak",
        CertificateFileExists = true,
        KnownProxies = ["10.0.0.10"],
        BackupProvider = "platform-managed",
        BackupSchedule = "daily",
        BackupRetentionDays = 14
    };

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "BlissBotChapel.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}

public sealed class Phase20RoleClaimTests
{
    private static readonly BlissAuthenticationOptions Options = new()
    {
        Enabled = true,
        RoleClaimType = "roles"
    };

    [Fact]
    public void Userinfo_role_array_grants_admin_write_and_review()
    {
        using var document = JsonDocument.Parse("""{"name":"Acceptance Operator","email":"a@example.test","roles":["bliss.admin"]}""");
        var identity = new ClaimsIdentity("oidc", "name", "roles");
        OidcRoleClaimMapper.Apply(identity, document.RootElement, "roles");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(principal.IsInRole("bliss.admin"));
        Assert.True(BlissAuthorization.CanWrite(principal, Options));
        Assert.True(BlissAuthorization.CanReview(principal, Options));
    }

    [Fact]
    public void Userinfo_reviewer_string_cannot_write()
    {
        using var document = JsonDocument.Parse("""{"roles":"bliss.reviewer"}""");
        var identity = new ClaimsIdentity("oidc", "name", "roles");
        OidcRoleClaimMapper.Apply(identity, document.RootElement, "roles");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(BlissAuthorization.CanReview(principal, Options));
        Assert.False(BlissAuthorization.CanWrite(principal, Options));
    }

    [Fact]
    public void Json_array_role_claim_is_normalized_before_authorization()
    {
        var identity = new ClaimsIdentity(
            [new Claim("roles", """["bliss.operator"]""")],
            "oidc",
            "name",
            "roles");
        OidcRoleClaimMapper.Normalize(identity, "roles");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(principal.IsInRole("bliss.operator"));
        Assert.True(BlissAuthorization.CanWrite(principal, Options));
        Assert.False(BlissAuthorization.CanReview(principal, Options));
    }

    [Fact]
    public void Cookie_alignment_honors_framework_role_claims()
    {
        var incoming = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "bliss.admin"), new Claim("name", "Acceptance Operator")],
            "oidc",
            ClaimTypes.Name,
            ClaimTypes.Role);
        var aligned = OidcRoleClaimMapper.Align(incoming, "name", "roles");
        var principal = new ClaimsPrincipal(aligned);

        Assert.Equal("roles", aligned.RoleClaimType);
        Assert.True(principal.IsInRole("bliss.admin"));
        Assert.True(BlissAuthorization.CanWrite(principal, Options));
    }

    [Fact]
    public async Task OpenIdConnect_userinfo_event_applies_roles()
    {
        var options = new OpenIdConnectOptions();
        OidcRoleClaimMapper.Configure(options, Options);
        var identity = new ClaimsIdentity("oidc", "name", "roles");
        var principal = new ClaimsPrincipal(identity);
        using var document = JsonDocument.Parse("""{"roles":["bliss.viewer","bliss.reviewer"]}""");
        var context = new UserInformationReceivedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme(OpenIdConnectDefaults.AuthenticationScheme, "OIDC", typeof(OpenIdConnectHandler)),
            options,
            principal,
            new AuthenticationProperties())
        {
            User = document
        };

        await options.Events.OnUserInformationReceived(context);

        Assert.True(principal.IsInRole("bliss.reviewer"));
        Assert.False(BlissAuthorization.CanWrite(principal, Options));
        Assert.True(BlissAuthorization.CanReview(principal, Options));
    }
}

public sealed class Phase20DevelopmentPostureTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase20DevelopmentPostureTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Development_posture_is_anonymous_and_does_not_claim_production()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/posture");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"productionGatesApplied\":false", body);
        Assert.Contains("\"secretMaterialExternal\":true", body);
        Assert.DoesNotContain("Password", body);
    }
}

public sealed class Phase20ProductionHostTests : IClassFixture<ProductionPostureApiFactory>
{
    private readonly ProductionPostureApiFactory _factory;

    public Phase20ProductionHostTests(ProductionPostureApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Production_probes_distinguish_process_database_and_posture()
    {
        var client = _factory.CreateProductionClient();

        var live = await client.GetAsync("/health/live?access_token=super-secret-value");
        var ready = await client.GetAsync("/health/ready");
        var posture = await client.GetAsync("/health/posture");
        var status = await client.GetAsync("/api/runtime/status");
        var creators = await client.GetAsync("/api/creators");
        var liveBody = await live.Content.ReadAsStringAsync();
        var readyBody = await ready.Content.ReadAsStringAsync();
        var postureBody = await posture.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", liveBody);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Contains("\"database\"", readyBody);
        Assert.Contains("Unhealthy", readyBody);
        Assert.Equal(HttpStatusCode.OK, posture.StatusCode);
        Assert.Contains("\"productionGatesApplied\":true", postureBody);
        Assert.Contains("\"keysEncryptedAtRest\":true", postureBody);
        Assert.Contains("\"hostedDatabaseConfigured\":true", postureBody);
        Assert.Contains("\"databaseTransportEncrypted\":true", postureBody);
        Assert.Contains("\"databaseServerCertificateVerified\":true", postureBody);
        Assert.Contains("\"identityProviderHttps\":true", postureBody);
        Assert.Contains("bliss.reviewer", postureBody);
        Assert.Contains("\"backupDeclared\":true", postureBody);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, creators.StatusCode);
        Assert.DoesNotContain(ProductionPostureApiFactory.DatabasePassword, liveBody + readyBody + postureBody);
        Assert.DoesNotContain(ProductionPostureApiFactory.ClientSecret, liveBody + readyBody + postureBody);
        Assert.DoesNotContain(ProductionPostureApiFactory.CertificatePassword, liveBody + readyBody + postureBody);
        Assert.DoesNotContain("super-secret-value", liveBody + readyBody + postureBody);
    }

    [Fact]
    public async Task Persisted_keys_are_encrypted_and_round_trip_on_a_second_process()
    {
        var protector = _factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("phase20");
        var protectedPayload = protector.Protect("phase20-marker");
        var keyFile = Directory.GetFiles(_factory.KeyDirectory, "key-*.xml").Single();
        var xml = await File.ReadAllTextAsync(keyFile);

        Assert.Contains("EncryptedData", xml);
        Assert.DoesNotContain("<value>", xml);
        Assert.DoesNotContain(ProductionPostureApiFactory.CertificatePassword, xml);
        Assert.DoesNotContain(ProductionPostureApiFactory.DatabasePassword, xml);

        using var second = ProductionPostureApiFactory.Sharing(_factory);
        var restored = second.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("phase20")
            .Unprotect(protectedPayload);
        Assert.Equal("phase20-marker", restored);
    }

    [Fact]
    public async Task Liveness_stays_available_under_a_small_concurrent_probe()
    {
        var client = _factory.CreateProductionClient();
        var responses = await Task.WhenAll(Enumerable.Range(0, 48).Select(_ => client.GetAsync("/health/live")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
    }
}

public sealed class Phase20StartupRejectionTests
{
    [Fact]
    public void Production_startup_rejects_an_unencrypted_key_ring()
    {
        using var factory = new IncompleteProductionFactory();
        var exception = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Production posture is incomplete", exception.ToString());
        Assert.Contains("encrypted at rest", exception.ToString());
        Assert.DoesNotContain(ProductionPostureApiFactory.ClientSecret, exception.ToString());
    }
}

public sealed class Phase20BackupDrillTests
{
    [Fact]
    public void Backup_drill_refuses_without_an_explicit_local_opt_in()
    {
        var script = Path.Combine(Phase20ProductionPostureEvaluatorTests.RepoRoot(), "scripts", "postgres-backup-restore-drill.sh");
        var start = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(script);
        start.Environment["BLISS_BACKUP_DRILL"] = "0";
        start.Environment["BLISS_BACKUP_DRILL_TARGET"] = "local";
        using var process = Process.Start(start);
        Assert.NotNull(process);
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);

        Assert.Equal(2, process.ExitCode);
        Assert.Contains("refused", stdout + stderr, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class ProductionPostureApiFactory : WebApplicationFactory<Program>
{
    private readonly string _root;
    private readonly bool _ownsRoot;

    public const string DatabasePassword = "phase20-db-password-do-not-leak";
    public const string ClientSecret = "phase20-client-secret-do-not-leak";
    public const string CertificatePassword = "phase20-cert-password-do-not-leak";

    public string KeyDirectory { get; }
    public string CertificatePath { get; }

    public ProductionPostureApiFactory()
        : this(Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bliss-phase20-" + Guid.NewGuid().ToString("N"))).FullName, true)
    {
    }

    private ProductionPostureApiFactory(string root, bool ownsRoot)
    {
        _root = root;
        _ownsRoot = ownsRoot;
        KeyDirectory = Path.Combine(root, "keys");
        Directory.CreateDirectory(KeyDirectory);
        CertificatePath = Path.Combine(root, "dp.pfx");
        if (!File.Exists(CertificatePath))
        {
            TestCertificate.Write(CertificatePath, CertificatePassword);
        }
    }

    public static ProductionPostureApiFactory Sharing(ProductionPostureApiFactory existing) =>
        new(existing._root, false);

    public HttpClient CreateProductionClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=203.0.113.10;Port=5432;Database=bliss;Username=bliss_app;Password="
            + DatabasePassword
            + ";SSL Mode=VerifyFull;Trust Server Certificate=false;Timeout=1");
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:Authority", "https://identity.example.test/realms/bliss");
        builder.UseSetting("Authentication:ClientId", "bliss-chapel");
        builder.UseSetting("Authentication:ClientSecret", ClientSecret);
        builder.UseSetting("Runtime:DataProtectionKeysPath", KeyDirectory);
        builder.UseSetting("Runtime:DataProtectionCertificatePath", CertificatePath);
        builder.UseSetting("Runtime:DataProtectionCertificatePassword", CertificatePassword);
        builder.UseSetting("Runtime:KnownProxies:0", "10.0.0.10");
        builder.UseSetting("Runtime:Backup:Provider", "platform-managed");
        builder.UseSetting("Runtime:Backup:Schedule", "daily");
        builder.UseSetting("Runtime:Backup:RetentionDays", "14");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_ownsRoot && Directory.Exists(_root))
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }
    }
}

internal sealed class IncompleteProductionFactory : WebApplicationFactory<Program>
{
    private readonly string _keys = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "bliss-phase20-reject-" + Guid.NewGuid().ToString("N"))).FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:Authority", "https://identity.example.test/realms/bliss");
        builder.UseSetting("Authentication:ClientId", "bliss-chapel");
        builder.UseSetting("Authentication:ClientSecret", ProductionPostureApiFactory.ClientSecret);
        builder.UseSetting("Runtime:DataProtectionKeysPath", _keys);
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=db.phase20.example.test;Port=5432;Database=bliss;Username=bliss_app;Password="
            + ProductionPostureApiFactory.DatabasePassword
            + ";SSL Mode=Require");
        builder.UseSetting("Runtime:KnownProxies:0", "10.0.0.10");
        builder.UseSetting("Runtime:Backup:Provider", "platform-managed");
        builder.UseSetting("Runtime:Backup:Schedule", "daily");
        builder.UseSetting("Runtime:Backup:RetentionDays", "14");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            Directory.Delete(_keys, true);
        }
        catch (IOException)
        {
        }
    }
}

internal static class TestCertificate
{
    public static void Write(string path, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Bliss Data Protection",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment,
            true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(2));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
    }
}

internal sealed class CollectingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Messages.Add(formatter(state, exception));

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}
