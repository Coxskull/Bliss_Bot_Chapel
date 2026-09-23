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
    public async Task Root_serves_the_public_bliss_chapel_experience()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("BLISS CHAPEL", body);
        Assert.Contains("For Advertisers", body);
        Assert.Contains("For Creators", body);
        Assert.Contains("Welcome to Bliss Chapel", body);
        Assert.Contains("The Wedding Planner", body);
        Assert.Contains("Nothing Goes Live Without Your Approval", body);
        Assert.Contains("FOUNDATION READY", body);
        Assert.Contains("AI conversation begins only after the next approved engineering contract", body);
        Assert.Contains("wedding-planner.css", body);
        Assert.Contains("wedding-planner.js", body);
        Assert.DoesNotContain("Operations audit", body);
        Assert.DoesNotContain("Advertiser workspace and session ledger", body);
    }

    [Fact]
    public async Task Operations_route_preserves_the_internal_console()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/operations");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Operations audit", body);
        Assert.Contains("Creator directory", body);
        Assert.Contains("Form match", body);
        Assert.Contains("Review work queue", body);
        Assert.Contains("Placement work queue", body);
        Assert.Contains("Continue with SSO", body);
        Assert.Contains("Workspace status", body);
        Assert.Contains("Last verification", body);
        Assert.Contains("Recent verifications", body);
        Assert.Contains("Wedding Planner", body);
        Assert.Contains("Advertiser workspace and session ledger", body);
        Assert.Contains("Economics reference data", body);
        Assert.Contains("Phase 5 keeps recommendation, quote, approval, and outcome separate", body);
        Assert.Contains("Generate explainable range", body);
        Assert.Contains("Create explicit draft quote", body);
        Assert.Contains("Record human decision or advertiser response", body);
        Assert.Contains("Rate recommendations", body);
        Assert.Contains("Commercial quotes", body);
        Assert.Contains("Audience snapshots", body);
        Assert.Contains("Performance snapshots", body);
        Assert.Contains("Market profiles", body);
        Assert.Contains("Industry profiles", body);
        Assert.Contains("Inventory benchmarks", body);
        Assert.Contains("FX observations", body);
        Assert.Contains("Export ledger", body);
        Assert.Contains("Verify pack", body);
        Assert.Contains("app.js", body);
        var script = await (await client.GetAsync("/operations/app.js")).Content.ReadAsStringAsync();
        Assert.Contains("Export case file", script);
        Assert.Contains("/api/audit/export/matches/", script);
        Assert.Contains("/api/audit/export/creators/", script);
        Assert.Contains("/api/audit/export/campaigns/", script);
        Assert.Contains("X-Content-SHA256", script);
        Assert.Contains("sha256:", script);
        Assert.Contains("/api/audit/verify", script);
        Assert.Contains("data-verify-pack", script);
        Assert.Contains("lastVerification", script);
        Assert.Contains("recentVerifications", script);
        Assert.Contains("bliss-verify-", script);
        Assert.Contains("event.detail", script);
        Assert.Contains("/api/wedding-planner/workspaces", script);
        Assert.Contains("/api/economics/markets", script);
        Assert.Contains("/api/economics/pricing-models", script);
        Assert.Contains("/api/economics/observations", script);
        Assert.Contains("/api/economics/audience-snapshots", script);
        Assert.Contains("/api/economics/performance-snapshots", script);
        Assert.Contains("/api/economics/market-profiles", script);
        Assert.Contains("/api/economics/industry-profiles", script);
        Assert.Contains("/api/economics/inventory-benchmarks", script);
        Assert.Contains("/api/economics/exchange-rates", script);
        Assert.Contains("/api/economics/pricing-rule-versions", script);
        Assert.Contains("/api/economics/recommendations", script);
        Assert.Contains("/api/economics/quotes", script);
        Assert.Contains("/approvals", script);
        Assert.Contains("/outcomes", script);
        Assert.Contains("Phase 1 does not invoke AI", body);
        Assert.DoesNotContain("TEST ENVIRONMENT", body);
        Assert.DoesNotContain("TEST_OPERATOR", body);
    }

    [Theory]
    [InlineData("/operations/app.css", "text/css")]
    [InlineData("/operations/app.js", "text/javascript")]
    [InlineData("/wedding-planner.css", "text/css")]
    [InlineData("/wedding-planner.js", "text/javascript")]
    public async Task Frontend_assets_are_served(string path, string mediaType)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mediaType, response.Content.Headers.ContentType?.MediaType);
        var minimumLength = path.EndsWith(".js", StringComparison.Ordinal) ? 100 : 1_000;
        Assert.True(response.Content.Headers.ContentLength > minimumLength);
    }

    [Fact]
    public async Task Client_routes_fall_back_to_the_dashboard()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/operations/matches");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Match certificates", body);
    }
}
