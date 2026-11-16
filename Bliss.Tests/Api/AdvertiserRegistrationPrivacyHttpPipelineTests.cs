using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bliss.Tests.Api;

/// <summary>
/// Real ASP.NET TestServer HTTP pipeline, fictional identities and isolated
/// in-memory persistence. Never contacts a real IdP, Railway, Supabase or CRM.
/// PostgreSQL migration/integrity checks run in separate synthetic PG CI jobs.
/// </summary>
public sealed class AdvertiserRegistrationPrivacyHttpPipelineTests
{
    private const string Base = "/api/advertisers/privacy-requests";

    private static HttpClient IdentityClient(AdvertiserPrivacyHttpFactory factory,
        string subject, string mode = "pending", Guid? advertiserId = null)
    {
        var client = factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Fixture-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Fixture-Mode", mode);
        if (advertiserId.HasValue)
            client.DefaultRequestHeaders.Add("X-Fixture-Advertiser-Id", advertiserId.Value.ToString("D"));
        return client;
    }

    private static async Task<string> Csrf(HttpClient client)
    {
        using var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isAuthenticated").GetBoolean());
        return Assert.IsType<string>(json.RootElement.GetProperty("csrfToken").GetString());
    }

    private static async Task<Guid> Register(HttpClient client, string label)
    {
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        using var response = await client.PostAsJsonAsync("/api/advertisers/register", new
        {
            requestKey = Guid.NewGuid(),
            businessName = label,
            countryCode = "PH",
            website = "https://example.invalid",
            processingConsent = true,
            termsAccepted = true,
            marketingOptIn = false,
            privacyNoticeVersion = AdvertiserRegistrationController.PrivacyNoticeVersion,
            termsVersion = AdvertiserRegistrationController.TermsVersion
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PENDING_REVIEW", json.RootElement.GetProperty("status").GetString());
        return json.RootElement.GetProperty("advertiserId").GetGuid();
    }

    private static async Task<Guid> CreateCase(HttpClient client, Guid key, string kind = "ACCESS")
    {
        using var response = await client.PostAsJsonAsync(Base, new
        {
            requestKey = key,
            requestType = kind
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("RECEIVED", json.RootElement.GetProperty("status").GetString());
        return json.RootElement.GetProperty("caseId").GetGuid();
    }

    [Fact]
    public async Task Http_registration_rejects_excess_writes_with_429_before_database_access()
    {
        // Actual ASP.NET endpoint limiter; fake issuer + a separate in-memory
        // factory. Only intentionally invalid requests: zero database writes.
        await using var factory = new AdvertiserPrivacyHttpFactory(writePermitLimit: 1);
        using var client = IdentityClient(factory, "fixture-throttle");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(client));
        var invalid = new
        {
            requestKey = Guid.Empty,
            businessName = "No business created",
            countryCode = "PH",
            website = (string?)null,
            processingConsent = false,
            termsAccepted = false,
            marketingOptIn = false,
            privacyNoticeVersion = AdvertiserRegistrationController.PrivacyNoticeVersion,
            termsVersion = AdvertiserRegistrationController.TermsVersion
        };
        using var first = await client.PostAsJsonAsync("/api/advertisers/register", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        using var throttled = await client.PostAsJsonAsync("/api/advertisers/register", invalid);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Contains("600", throttled.Headers.GetValues("Retry-After"));
    }

    [Fact]
    public async Task Http_registration_case_receipts_tenant_isolation_and_staff_ack_use_real_csrf_pipeline()
    {
        // The successful operator ACK uses EF ExecuteUpdate, which only a real
        // relational provider can execute. CI supplies a localhost PostgreSQL
        // service; the fixture creates and destroys its own database.
        await using var isolated = Task3bHttpPostgresDatabase.TryCreate();
        await using var factory = new AdvertiserPrivacyHttpFactory(
            postgresConnectionString: isolated?.ConnectionString);
        using var a = IdentityClient(factory, "fixture-a");
        using var b = IdentityClient(factory, "fixture-b");
        var advertiserA = await Register(a, "Fictional A Studio");
        var advertiserB = await Register(b, "Fictional B Studio");
        Assert.NotEqual(advertiserA, advertiserB);

        var key = Guid.NewGuid();
        var caseId = await CreateCase(a, key, "MARKETING_WITHDRAWAL");
        using var replay = await a.PostAsJsonAsync(Base, new { requestKey = key, requestType = "MARKETING_WITHDRAWAL" });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using (var json = JsonDocument.Parse(await replay.Content.ReadAsStringAsync()))
            Assert.Equal(caseId, json.RootElement.GetProperty("caseId").GetGuid());
        using var wrongType = await a.PostAsJsonAsync(Base, new { requestKey = key, requestType = "ERASURE" });
        Assert.Equal(HttpStatusCode.Conflict, wrongType.StatusCode);
        using var crossTenant = await b.PostAsJsonAsync(Base, new { requestKey = key, requestType = "MARKETING_WITHDRAWAL" });
        Assert.Equal(HttpStatusCode.Conflict, crossTenant.StatusCode);
        using var invalid = await a.PostAsJsonAsync(Base, new { requestKey = Guid.NewGuid(), requestType = "EVERY_TENANT_EXPORT" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var own = await a.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using (var json = JsonDocument.Parse(await own.Content.ReadAsStringAsync()))
        {
            Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(caseId, json.RootElement[0].GetProperty("caseId").GetGuid());
        }
        using var otherOwn = await b.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, otherOwn.StatusCode);
        using (var json = JsonDocument.Parse(await otherOwn.Content.ReadAsStringAsync()))
            Assert.Empty(json.RootElement.EnumerateArray());

        using var forbiddenQueue = await a.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenQueue.StatusCode);
        using var forbiddenAck = await a.PostAsJsonAsync(Base + "/" + caseId + "/acknowledge", new { });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenAck.StatusCode);
        using var forbiddenMarketplace = await a.GetAsync("/api/creators");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenMarketplace.StatusCode);

        using var operatorClient = IdentityClient(factory, "fixture-operator", "operator");
        operatorClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(operatorClient));
        using var queue = await operatorClient.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.OK, queue.StatusCode);
        using (var json = JsonDocument.Parse(await queue.Content.ReadAsStringAsync()))
        {
            Assert.Single(json.RootElement.EnumerateArray());
            Assert.Equal(caseId, json.RootElement[0].GetProperty("caseId").GetGuid());
        }
        if (isolated is null) return; // Local source-only builds lack Postgres; CI exercises ACK.

        using var acknowledged = await operatorClient.PostAsJsonAsync(Base + "/" + caseId + "/acknowledge", new { });
        Assert.Equal(HttpStatusCode.OK, acknowledged.StatusCode);
        using (var json = JsonDocument.Parse(await acknowledged.Content.ReadAsStringAsync()))
            Assert.Equal("VERIFYING", json.RootElement.GetProperty("status").GetString());
        using var repeatAck = await operatorClient.PostAsJsonAsync(Base + "/" + caseId + "/acknowledge", new { });
        Assert.Equal(HttpStatusCode.Conflict, repeatAck.StatusCode);

        using var after = await a.GetAsync(Base);
        using (var json = JsonDocument.Parse(await after.Content.ReadAsStringAsync()))
            Assert.Equal("VERIFYING", json.RootElement[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Http_pending_and_approved_sessions_require_valid_csrf_and_exact_case_scope()
    {
        await using var factory = new AdvertiserPrivacyHttpFactory();
        using var anonymous = factory.CreateSecureClient();
        using var denyAnonymous = await anonymous.GetAsync(Base);
        Assert.Equal(HttpStatusCode.Unauthorized, denyAnonymous.StatusCode);
        using var anonymousPost = await anonymous.PostAsJsonAsync(Base, new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPost.StatusCode);

        using var pending = IdentityClient(factory, "fixture-a");
        using var noToken = await pending.PostAsJsonAsync("/api/advertisers/register",
            new { requestKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Contains("CSRF", await noToken.Content.ReadAsStringAsync());
        var token = await Csrf(pending);
        pending.DefaultRequestHeaders.Add("RequestVerificationToken", token);
        using var wrongHeader = await pending.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongHeader.StatusCode);
        pending.DefaultRequestHeaders.Remove("RequestVerificationToken");
        pending.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

        var advertiserId = await RegisterWithoutResettingCsrf(pending, "Fictional Pending Studio");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var membership = await db.AdvertiserAccountMemberships.SingleAsync(x => x.Subject == "fixture-a");
            Assert.Equal(advertiserId, membership.AdvertiserId);
            membership.Status = "ACTIVE";
            await db.SaveChangesAsync();
        }
        using var approved = IdentityClient(factory, "fixture-a", "approved", advertiserId);
        approved.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await Csrf(approved));
        using var getOwn = await approved.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, getOwn.StatusCode);
        using var created = await approved.PostAsJsonAsync(Base, new
        {
            requestKey = Guid.NewGuid(), requestType = "ACCESS"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var forbiddenQueue = await approved.GetAsync(Base + "/operator-queue");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenQueue.StatusCode);
        using var forbiddenUnknown = await approved.GetAsync(Base + "/other-tenant");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenUnknown.StatusCode);
        using var forbiddenMarketplace = await approved.GetAsync("/api/creators");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenMarketplace.StatusCode);
    }

    private static async Task<Guid> RegisterWithoutResettingCsrf(HttpClient client, string label)
    {
        using var response = await client.PostAsJsonAsync("/api/advertisers/register", new
        {
            requestKey = Guid.NewGuid(), businessName = label, countryCode = "PH",
            website = "https://example.invalid", processingConsent = true,
            termsAccepted = true, marketingOptIn = false,
            privacyNoticeVersion = AdvertiserRegistrationController.PrivacyNoticeVersion,
            termsVersion = AdvertiserRegistrationController.TermsVersion
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("advertiserId").GetGuid();
    }

    [Fact]
    public async Task Http_privacy_feature_off_denies_intake_despite_registration_being_enabled()
    {
        await using var factory = new AdvertiserPrivacyHttpFactory(privacyEnabled: false);
        using var applicant = IdentityClient(factory, "fixture-a");
        var advertiserId = await Register(applicant, "Fictional Rights Disabled Studio");
        Assert.NotEqual(Guid.Empty, advertiserId);
        using var denyRead = await applicant.GetAsync(Base);
        Assert.Equal(HttpStatusCode.Forbidden, denyRead.StatusCode);
        using var denyWrite = await applicant.PostAsJsonAsync(Base,
            new { requestKey = Guid.NewGuid(), requestType = "ACCESS" });
        Assert.Equal(HttpStatusCode.Forbidden, denyWrite.StatusCode);
    }
}

/// <summary>
/// One test-owned database on local CI PostgreSQL only. No hosted or remote
/// target is eligible; all storage is synthetic and removed at test exit.
/// </summary>
internal sealed class Task3bHttpPostgresDatabase : IAsyncDisposable
{
    private readonly string adminConnection;
    private readonly string databaseName;
    public string ConnectionString { get; }

    private Task3bHttpPostgresDatabase(string adminConnection, string databaseName, string connectionString)
    {
        this.adminConnection = adminConnection;
        this.databaseName = databaseName;
        ConnectionString = connectionString;
    }

    public static Task3bHttpPostgresDatabase? TryCreate()
    {
        var source = Environment.GetEnvironmentVariable("BLISS_TEST_POSTGRES_CONNECTION");
        // The dedicated PG18 registration/rights acceptance job must never
        // silently fall back to EF InMemory. Local no-Postgres source tests
        // retain their existing lightweight behavior.
        var requirePg18 = Environment.GetEnvironmentVariable("TASK3B_REQUIRE_PG18") == "1";
        if (string.IsNullOrWhiteSpace(source))
        {
            if (requirePg18)
                throw new InvalidOperationException("TASK3B_PG18_RELATIONAL_TARGET_REQUIRED");
            return null;
        }
        var builder = new NpgsqlConnectionStringBuilder(source);
        if (!string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            && builder.Host != "127.0.0.1" && builder.Host != "::1")
            throw new InvalidOperationException("Task #3B HTTP integration requires localhost PostgreSQL.");

        var name = "task3b_http_" + Guid.NewGuid().ToString("N");
        using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            admin.Open();
            if (requirePg18)
            {
                using var major = new NpgsqlCommand("SHOW server_version_num", admin);
                var version = int.Parse((string)(major.ExecuteScalar()
                    ?? throw new InvalidOperationException("TASK3B_PG18_VERSION_UNAVAILABLE")),
                    System.Globalization.CultureInfo.InvariantCulture);
                if (version < 180000 || version >= 190000)
                    throw new InvalidOperationException("TASK3B_PG18_VERSION_MISMATCH");
            }
            using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            create.ExecuteNonQuery();
        }
        var scoped = new NpgsqlConnectionStringBuilder(source) { Database = name };
        var options = new DbContextOptionsBuilder<BlissDbContext>().UseNpgsql(scoped.ConnectionString).Options;
        using var db = new BlissDbContext(options);
        db.Database.Migrate();
        return new Task3bHttpPostgresDatabase(builder.ConnectionString, name, scoped.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }
}

internal sealed class AdvertiserPrivacyHttpFactory(bool privacyEnabled = true,
    string? postgresConnectionString = null, int? writePermitLimit = null) : WebApplicationFactory<Program>
{
    private const string Issuer = "https://identity.example.test";
    private readonly string dbName = "task3b_http_" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:SupabaseEnabled", "true");
        builder.UseSetting("Authentication:Authority", Issuer);
        builder.UseSetting("Authentication:ClientId", "synthetic-http-fixture");
        builder.UseSetting("Authentication:AdvertiserRegistrationRequested", "true");
        builder.UseSetting("Authentication:AdvertiserSelfServiceRequested", "true");
        builder.UseSetting("Authentication:AdvertiserPrivacyRequestsRequested", privacyEnabled ? "true" : "false");
        builder.UseSetting("Authentication:ApprovedAdvertiserSubjects:0", "fixture-a");
        builder.UseSetting("Authentication:ApprovedPrivacyOperatorSubjects:0", "fixture-operator");
        if (writePermitLimit.HasValue)
        {
            builder.UseSetting("Runtime:WriteRateLimitPermitLimit", writePermitLimit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("Runtime:RateLimitWindowSeconds", "600");
        }

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                || d.ServiceType == typeof(BlissDbContext)).ToArray();
            foreach (var item in toRemove) services.Remove(item);
            if (!string.IsNullOrWhiteSpace(postgresConnectionString))
                services.AddDbContext<BlissDbContext>(options => options.UseNpgsql(postgresConnectionString));
            else
                // The registration controller begins an EF transaction. InMemory
                // cannot enforce transactions; this is only for no-Postgres tests.
                services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(dbName)
                    .ConfigureWarnings(warnings => warnings.Ignore(
                        Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AdvertiserPrivacyTestAuthHandler.FixtureScheme;
                options.DefaultChallengeScheme = AdvertiserPrivacyTestAuthHandler.FixtureScheme;
                options.DefaultForbidScheme = AdvertiserPrivacyTestAuthHandler.FixtureScheme;
            }).AddScheme<AuthenticationSchemeOptions, AdvertiserPrivacyTestAuthHandler>(
                AdvertiserPrivacyTestAuthHandler.FixtureScheme, _ => { });
        });
    }

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    });
}

/// <summary>Test-only fake issuer. No production authentication route or user ever created.</summary>
internal sealed class AdvertiserPrivacyTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string FixtureScheme = "Task3bSyntheticOnly";
    private const string Issuer = "https://identity.example.test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Fixture-Subject", out var subjects)
            || subjects.Count != 1 || string.IsNullOrWhiteSpace(subjects[0]))
            return Task.FromResult(AuthenticateResult.NoResult());

        var subject = subjects[0]!;
        var mode = Request.Headers.TryGetValue("X-Fixture-Mode", out var modes)
            ? modes.ToString() : "pending";
        var claims = new List<Claim> { new("sub", subject) };
        switch (mode)
        {
            case "pending":
                claims.Add(new Claim("email_verified", "true"));
                claims.Add(new Claim(BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
                    "true", ClaimValueTypes.Boolean, Issuer, Issuer));
                break;
            case "approved":
                if (!Request.Headers.TryGetValue("X-Fixture-Advertiser-Id", out var id)
                    || !Guid.TryParse(id.ToString(), out var parsed) || parsed == Guid.Empty)
                    return Task.FromResult(AuthenticateResult.Fail("No fictional advertiser scope."));
                claims.Add(new Claim("roles", "bliss.advertiser", ClaimValueTypes.String, Issuer, Issuer));
                claims.Add(new Claim("advertiser_id", parsed.ToString("D"), ClaimValueTypes.String, Issuer, Issuer));
                claims.Add(new Claim(BlissAuthenticationOptions.AdvertiserSelfServiceMarkerClaimType,
                    "true", ClaimValueTypes.Boolean, Issuer, Issuer));
                break;
            case "operator":
                claims.Add(new Claim("roles", "bliss.operator", ClaimValueTypes.String, Issuer, Issuer));
                break;
            default:
                return Task.FromResult(AuthenticateResult.Fail("Unknown fictional test identity."));
        }

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, FixtureScheme)),
            FixtureScheme)));
    }
}
