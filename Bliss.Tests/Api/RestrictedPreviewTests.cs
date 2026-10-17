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
    public void Data_and_auth_paths_are_never_interface_files(string path) =>
        Assert.False(RestrictedPreviewGate.IsInterfacePath(path));

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
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/signin-oidc")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsync("/api/bliss/matches", new StringContent("{}"))).StatusCode);
    }
}
