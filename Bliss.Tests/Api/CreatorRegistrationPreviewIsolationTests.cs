using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;

// This only checks fail-closed static publication control. It does NOT
// validate production privacy acceptance, real DB consent or hosted routes.
public sealed class CreatorRegistrationPreviewIsolationTests
{
    [Theory]
    [InlineData("/creator-registration-preview.html", "GET")]
    [InlineData("/CREATOR-REGISTRATION-PREVIEW.HTML", "HEAD")]
    [InlineData("/creator-registration-preview.js", "GET")]
    [InlineData("/CREATOR-REGISTRATION-PREVIEW.JS", "POST")]
    public async Task Engineering_preview_is_never_served_over_http(string path, string method)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        var nextCalls = 0;
        await CreatorRegistrationPreviewIsolation.InvokeAsync(context, () =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("noindex, nofollow", context.Response.Headers["X-Robots-Tag"].ToString());
        Assert.Equal(0, nextCalls);
    }

    [Fact]
    public async Task Actual_host_pipeline_denies_preview_assets_before_static_files()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Runtime:RestrictedPreview", "true");
        });
        using var client = factory.CreateClient();
        foreach (var path in new[] {
            "/creator-registration-preview.html", "/creator-registration-preview.js"
        })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("no-store",
                response.Headers.CacheControl?.ToString() ?? string.Empty);
        }
        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/global-media.html")]
    [InlineData("/api/creator-registration")]
    [InlineData("/health/live")]
    public async Task Other_routes_continue_into_existing_security_middleware(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var nextCalls = 0;
        await CreatorRegistrationPreviewIsolation.InvokeAsync(context, () =>
        {
            nextCalls++;
            return Task.CompletedTask;
        });
        Assert.Equal(1, nextCalls);
        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }
}
