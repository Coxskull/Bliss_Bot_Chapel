using System.Net;

namespace Bliss.Tests.Api;

public sealed class FrontendTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public FrontendTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Root_serves_the_operations_frontend()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Bliss Chapel", body);
        Assert.Contains("Operations audit", body);
        Assert.Contains("Creator directory", body);
        Assert.Contains("Form match", body);
        Assert.Contains("Review work queue", body);
        Assert.Contains("Placement work queue", body);
        Assert.Contains("Continue with SSO", body);
        Assert.Contains("app.js", body);
        Assert.DoesNotContain("TEST ENVIRONMENT", body);
        Assert.DoesNotContain("TEST_OPERATOR", body);
    }

    [Theory]
    [InlineData("/app.css", "text/css")]
    [InlineData("/app.js", "text/javascript")]
    public async Task Frontend_assets_are_served(string path, string mediaType)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Content.Headers.ContentLength > 1_000);
    }

    [Fact]
    public async Task Client_routes_fall_back_to_the_dashboard()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/matches");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Match certificates", body);
    }
}
