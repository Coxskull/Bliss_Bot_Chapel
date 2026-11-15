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
    public async Task Http_registration_case_receipts_tenant_isolation_and_staff_ack_use_real_csrf_pipeline()
    {
        await using var factory = new AdvertiserPrivacyHttpFactory();
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

internal sealed class AdvertiserPrivacyHttpFactory(bool privacyEnabled = true)
    : WebApplicationFactory<Program>
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

        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                || d.ServiceType == typeof(BlissDbContext)).ToArray();
            foreach (var item in toRemove) services.Remove(item);
            services.AddDbContext<BlissDbContext>(
                options => options.UseInMemoryDatabase(dbName));
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AdvertiserPrivacyTestAuthHandler.Scheme;
                options.DefaultChallengeScheme = AdvertiserPrivacyTestAuthHandler.Scheme;
                options.DefaultForbidScheme = AdvertiserPrivacyTestAuthHandler.Scheme;
            }).AddScheme<AuthenticationSchemeOptions, AdvertiserPrivacyTestAuthHandler>(
                AdvertiserPrivacyTestAuthHandler.Scheme, _ => { });
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
    public const string Scheme = "Task3bSyntheticOnly";
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
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)),
            Scheme)));
    }
}
