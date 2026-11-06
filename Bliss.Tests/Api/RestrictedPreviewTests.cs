using System.Net;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Bliss.Tests.Api;

public sealed class RestrictedPreviewTests
{
    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/API/bliss/matches")]
    [InlineData("/operations/generated/private.jpg")]
    [InlineData("/source-media/private.png")]
    [InlineData("/operations/financial-review/index.html")]
    [InlineData("/signin-oidc")]
    [InlineData("/operations")]
    [InlineData("/operations/index.html")]
    [InlineData("/ai-workspace.html")]
    [InlineData("/brand-intake.html")]
    [InlineData("/production.html")]
    [InlineData("/campaign-handoff.html")]
    [InlineData("/acquisition/index.html")]
    [InlineData("/oauth-consent.html")]
    [InlineData("/oauth-consent.bundle.js")]
    [InlineData("/assets/unreviewed.jpg")]
    [InlineData("/swagger/index.html")]
    public void Data_and_auth_paths_are_never_interface_files(string path) =>
        Assert.False(RestrictedPreviewGate.IsInterfacePath(path));

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/wedding-planner.css")]
    [InlineData("/wedding-planner.js")]
    [InlineData("/preview-lock.js")]
    [InlineData("/assets/bliss-chapel-hero.jpg")]
    [InlineData("/health/live")]
    [InlineData("/creator-signin-preview.html")]
    [InlineData("/advertiser-signin-preview.html")]
    public void Public_landing_paths_are_explicitly_allowed(string path) =>
        Assert.True(RestrictedPreviewGate.IsInterfacePath(path));

    [Fact]
    public async Task Preview_serves_interface_but_never_claims_operational_readiness_or_executes_api()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development"); // disposable fixture, never deployment configuration
            builder.UseSetting("Runtime:RestrictedPreview", "true");
        });
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        foreach (var path in new[] { "/health/ready", "/api/auth/session", "/api/auth/login", "/api/bliss/matches", "/signin-oidc", "/operations/generated/private.jpg" })
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsync("/api/bliss/matches", new StringContent("{}"))).StatusCode);
        var response = await client.GetAsync("/preview-status");
        Assert.Contains("\"operational\":false", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Production_restricted_preview_starts_without_oidc_but_keeps_all_non_interface_paths_unavailable()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), "bliss-preview-test-" + Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Authentication:Enabled", "false");
            builder.UseSetting("Runtime:RestrictedPreview", "true");
            builder.UseSetting("Runtime:DataProtectionKeysPath", keyPath);
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/wedding-planner.css")).StatusCode);
        foreach (var role in new[] { "creator", "advertiser" })
        {
            var screen = await client.GetAsync($"/{role}-signin-preview.html");
            Assert.Equal(HttpStatusCode.OK, screen.StatusCode);
            Assert.Equal("restricted-preview", screen.Headers.GetValues("X-Bliss-Mode").Single());
            var html = await screen.Content.ReadAsStringAsync();
            Assert.Contains("type=\"email\"", html);
            Assert.Contains("type=\"password\"", html);
            Assert.Contains("<fieldset disabled", html);
            Assert.Contains("<button type=\"button\" disabled>", html);
            Assert.DoesNotContain("method=\"post\"", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("fetch(", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("localStorage", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("name=\"password\"", html, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/auth/login")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/login.html")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/preview-lock.js")).StatusCode);
        foreach (var sensitivePath in new[]
        {
            "/operations", "/operations/index.html", "/ai-workspace.html",
            "/brand-intake.html", "/production.html", "/oauth-consent.html",
            "/acquisition/index.html", "/assets/unreviewed.jpg"
        })
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(sensitivePath)).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/signin-oidc")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsync("/api/bliss/matches", new StringContent("{}"))).StatusCode);
    }
}
