using System.Text.Json;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

// This is an in-memory contract test of the actual runtime middleware.
// It must never require hosting credentials, a database, login, or network.
public sealed class RestrictedPreviewGateContractTests
{
    private static async Task<(int Status, bool NextCalled, string Body,
        string Mode, string CacheControl)> InvokeAsync(string method, string path)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await RestrictedPreviewGate.InvokeAsync(context, () =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, nextCalled,
            await reader.ReadToEndAsync(),
            context.Response.Headers["X-Bliss-Mode"].ToString(),
            context.Response.Headers.CacheControl.ToString());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/health/live")]
    [InlineData("/wedding-planner.css")]
    [InlineData("/wedding-planner.js")]
    [InlineData("/preview-lock.js")]
    [InlineData("/assets/bliss-chapel-hero.jpg")]
    [InlineData("/favicon.ico")]
    [InlineData("/INDEX.HTML")]
    public async Task Public_get_and_head_paths_pass_through_without_changing_the_gate(
        string path)
    {
        foreach (var method in new[] { "GET", "HEAD" })
        {
            var response = await InvokeAsync(method, path);
            Assert.True(response.NextCalled);
            Assert.Equal(StatusCodes.Status204NoContent, response.Status);
            Assert.Equal("restricted-preview", response.Mode);
            Assert.Equal("no-store", response.CacheControl);
        }
    }

    [Theory]
    [InlineData("/production.html")]
    [InlineData("/brand-intake.html")]
    [InlineData("/operations")]
    [InlineData("/operations/generated/example.png")]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/supabase-config")]
    [InlineData("/api/operations/campaign-readiness")]
    [InlineData("/signin-oidc")]
    [InlineData("/health/ready")]
    [InlineData("/swagger")]
    [InlineData("/api/orders")]
    [InlineData("/index.html/anything")]
    [InlineData("/assets/private.jpg")]
    [InlineData("/%2e%2e/operations")]
    [InlineData("/PREVIEW-STATUS")]
    public async Task Private_or_unreviewed_get_paths_fail_closed_with_nonoperational_json(
        string path)
    {
        var response = await InvokeAsync("GET", path);
        Assert.False(response.NextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.Equal("restricted-preview", response.Mode);
        Assert.Equal("no-store", response.CacheControl);
        using var payload = JsonDocument.Parse(response.Body);
        Assert.Equal("restricted-preview",
            payload.RootElement.GetProperty("mode").GetString());
        Assert.False(payload.RootElement.GetProperty("operational").GetBoolean());
    }

    [Theory]
    [InlineData("POST", "/")]
    [InlineData("PUT", "/index.html")]
    [InlineData("PATCH", "/wedding-planner.css")]
    [InlineData("DELETE", "/health/live")]
    [InlineData("POST", "/api/orders")]
    [InlineData("POST", "/signin-oidc")]
    [InlineData("POST", "/preview-status")]
    [InlineData("HEAD", "/preview-status")]
    public async Task Non_read_only_methods_are_denied_even_on_public_paths(
        string method, string path)
    {
        var response = await InvokeAsync(method, path);
        Assert.False(response.NextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.Equal("restricted-preview", response.Mode);
        Assert.Equal("no-store", response.CacheControl);
    }

    [Fact]
    public async Task Preview_status_is_readable_but_never_grants_operational_authority()
    {
        var response = await InvokeAsync("GET", "/preview-status");
        Assert.False(response.NextCalled);
        Assert.Equal(StatusCodes.Status200OK, response.Status);
        Assert.Equal("restricted-preview", response.Mode);
        Assert.Equal("no-store", response.CacheControl);
        using var payload = JsonDocument.Parse(response.Body);
        Assert.True(payload.RootElement.GetProperty("restrictedPreview").GetBoolean());
        Assert.False(payload.RootElement.GetProperty("operational").GetBoolean());
    }

    [Fact]
    public void Unlisted_paths_never_become_public_because_they_have_an_extension()
    {
        Assert.False(RestrictedPreviewGate.IsInterfacePath("/private.html"));
        Assert.False(RestrictedPreviewGate.IsInterfacePath("/api/token.json"));
        Assert.False(RestrictedPreviewGate.IsInterfacePath("/operations/config.css"));
        Assert.False(RestrictedPreviewGate.IsInterfacePath("/assets/secret.png"));
        Assert.True(RestrictedPreviewGate.IsInterfacePath("/index.html"));
    }
}
