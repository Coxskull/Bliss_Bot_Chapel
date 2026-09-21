using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bliss.Tests.Api;

public sealed class Phase8DevelopmentAuthenticationTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase8DevelopmentAuthenticationTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Development_session_explicitly_reports_open_mode()
    {
        var session = await _factory.CreateClient().GetFromJsonAsync<SessionResponse>("/api/auth/session");

        Assert.NotNull(session);
        Assert.False(session.AuthenticationEnabled);
        Assert.True(session.AccessAllowed);
        Assert.True(session.CanWrite);
        Assert.True(session.CanReview);
        Assert.Null(session.CsrfToken);
    }

    [Fact]
    public void Authenticated_identity_replaces_submitted_operator_label()
    {
        var options = new BlissAuthenticationOptions { Enabled = true, NameClaimType = "name" };
        var identity = new ClaimsIdentity(
            [new Claim("name", "OIDC Operator"), new Claim("sub", "operator-123")],
            "test");

        var label = new OperatorIdentity(options)
            .ResolveLabel(new ClaimsPrincipal(identity), "UNTRUSTED_BROWSER_LABEL");

        Assert.Equal("OIDC Operator", label);
    }
}

public sealed class Phase8OidcSecurityTests : IClassFixture<OidcSecurityApiFactory>
{
    private readonly OidcSecurityApiFactory _factory;

    public Phase8OidcSecurityTests(OidcSecurityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_api_access_is_rejected_when_oidc_is_enabled()
    {
        var client = _factory.CreateSecureClient();
        var response = await client.GetAsync("/api/creators");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session);
        Assert.True(session.AuthenticationEnabled);
        Assert.False(session.AccessAllowed);
    }

    [Fact]
    public async Task Roles_and_csrf_both_gate_operator_writes()
    {
        var viewer = _factory.CreateSecureClient();
        viewer.DefaultRequestHeaders.Add("X-Test-Name", "Read Only");
        viewer.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.viewer");

        var viewerResponse = await viewer.PostAsJsonAsync("/api/bliss/matches", InvalidMatchRequest());
        Assert.Equal(HttpStatusCode.Forbidden, viewerResponse.StatusCode);

        var operatorClient = _factory.CreateSecureClient();
        operatorClient.DefaultRequestHeaders.Add("X-Test-Name", "OIDC Operator");
        operatorClient.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.operator");

        var missingCsrf = await operatorClient.PostAsJsonAsync("/api/bliss/matches", InvalidMatchRequest());
        Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        Assert.Contains("CSRF", await missingCsrf.Content.ReadAsStringAsync());

        var session = await operatorClient.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session?.CsrfToken);
        operatorClient.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);

        var authorized = await operatorClient.PostAsJsonAsync("/api/bliss/matches", InvalidMatchRequest());
        Assert.Equal(HttpStatusCode.BadRequest, authorized.StatusCode);
        Assert.DoesNotContain("CSRF", await authorized.Content.ReadAsStringAsync());
    }

    private static object InvalidMatchRequest() => new
    {
        sourceSystem = "SECURITY_TEST",
        idempotencyKey = $"security-{Guid.NewGuid()}",
        creatorId = Guid.NewGuid(),
        advertiserOpportunityId = Guid.NewGuid(),
        ruleVersionId = Guid.NewGuid(),
        evaluateOnCreate = false
    };
}

public sealed class OidcSecurityApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:Authority", "https://identity.example.test");
        builder.UseSetting("Authentication:ClientId", "bliss-tests");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                    || descriptor.ServiceType == typeof(BlissDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BlissDbContext>(
                options => options.UseInMemoryDatabase($"oidc-{Guid.NewGuid()}"));
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }

    public HttpClient CreateSecureClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
}

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Phase8Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Name", out var names))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("name", names.ToString()),
            new("sub", $"test-{names}")
        };
        if (Request.Headers.TryGetValue("X-Test-Roles", out var roles))
        {
            claims.AddRange(roles.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed record SessionResponse(
    bool AuthenticationEnabled,
    bool AccessAllowed,
    bool IsAuthenticated,
    string? DisplayName,
    string[] Roles,
    bool CanWrite,
    bool CanReview,
    string? CsrfToken);
