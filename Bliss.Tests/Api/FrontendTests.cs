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
        Assert.Contains("PHASE 2", body);
        Assert.Contains("LIVE CONCIERGE", body);
        Assert.Contains("Create Brand DNA proposal", body);
        Assert.Contains("brand-dna-decision-form", body);
        Assert.Contains("planner-composer", body);
        Assert.Contains("planner-message-stream", body);
        Assert.Contains("PHASE 3 · DETERMINISTIC COLOR INTELLIGENCE", body);
        Assert.Contains("color-intelligence-panel", body);
        Assert.Contains("color-compute-form", body);
        Assert.Contains("color-decision-form", body);
        Assert.Contains("color-swatch-grid", body);
        Assert.Contains("color-contrast-list", body);
        Assert.Contains("Deterministic palette computation only", body);
        Assert.Contains("no AI", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("arithmetic evidence", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not accessibility certification", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not psychology", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FOUNDATION READY", body);
        Assert.DoesNotContain("Help me choose colors", body);
        Assert.DoesNotContain("Show me some design concepts", body);
        Assert.DoesNotContain("Create a campaign for my restaurant", body);
        Assert.DoesNotContain("What advertising works in my industry?", body);
        Assert.Contains("wedding-planner.css", body);
        Assert.Contains("wedding-planner.js", body);
        Assert.DoesNotContain("Operations audit", body);
        Assert.DoesNotContain("Advertiser workspace, Concierge runs, and Brand DNA", body);
    }

    [Fact]
    public async Task Public_planner_script_wires_phase2_live_chat_and_brand_dna_without_fabricated_replies()
    {
        var client = _factory.CreateClient();
        var script = await (await client.GetAsync("/wedding-planner.js")).Content.ReadAsStringAsync();

        Assert.Contains("/api/auth/session", script);
        Assert.Contains("/api/wedding-planner/workspaces", script);
        Assert.Contains("/sessions", script);
        Assert.Contains("/turns", script);
        Assert.Contains("/brand-dna/interpret", script);
        Assert.Contains("/decisions", script);
        Assert.Contains("X-CSRF-TOKEN", script);
        Assert.Contains("authenticationEnabled", script);
        Assert.Contains("canWrite", script);
        Assert.Contains("advertiserId", script);
        Assert.Contains("plannerMessage", script);
        Assert.Contains("does not fabricate", script);
        Assert.DoesNotContain("fabricate a planner", script);
        Assert.DoesNotContain("fakePlanner", script);
        Assert.DoesNotContain("synthetic reply", script);
        Assert.DoesNotContain("I'm here to listen", script);
    }

    [Fact]
    public async Task Public_planner_script_wires_phase3_color_intelligence_from_server_documents_only()
    {
        var client = _factory.CreateClient();
        var script = await (await client.GetAsync("/wedding-planner.js")).Content.ReadAsStringAsync();

        Assert.Contains("PHASE 3", script);
        Assert.Contains("/color-profiles/compute", script);
        Assert.Contains("/color-profiles", script);
        Assert.Contains("/color-profiles/", script);
        Assert.Contains("/decisions", script);
        Assert.Contains("currentApprovedBrandDnaVersionId", script);
        Assert.Contains("documentJson", script);
        Assert.Contains("parseColorDocument", script);
        Assert.Contains("contrastEvidence", script);
        Assert.Contains("geometryDisclaimer", script);
        Assert.Contains("HUMAN", script);
        Assert.Contains("DERIVED", script);
        Assert.Contains("DEFAULT", script);
        Assert.Contains("secondaryDerived", script);
        Assert.Contains("accentDerived", script);
        Assert.Contains("backgroundDefaulted", script);
        Assert.Contains("surfaceDefaulted", script);
        Assert.Contains("aaNormal", script);
        Assert.Contains("aaLarge", script);
        Assert.Contains("inputSha256", script);
        Assert.Contains("approvedBrandDnaVersionId", script);
        Assert.Contains("isCurrentApproved", script);
        Assert.Contains("PROPOSED", script);
        Assert.Contains("APPROVED", script);
        Assert.Contains("CURRENT", script);
        Assert.Contains("X-CSRF-TOKEN", script);
        Assert.Contains("idempotencyKey", script);
        Assert.Contains("canComputeColorProfiles", script);
        Assert.Contains("No random or test advertiser is bound automatically", script);
        Assert.Contains("deterministic", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no AI", script, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("relativeLuminance", script);
        Assert.DoesNotContain("srgbToLinear", script);
        Assert.DoesNotContain("contrastRatio", script);
        Assert.DoesNotContain("mixTowardBlack", script);
        Assert.DoesNotContain("hue + 180", script);
        Assert.DoesNotContain("hue+180", script);
        Assert.DoesNotContain("fabricateColor", script);
        Assert.DoesNotContain("fakePalette", script);
        Assert.DoesNotContain("syntheticProfile", script);
        Assert.DoesNotContain("clientDerived", script);
        Assert.DoesNotContain("Math.pow", script);
        Assert.DoesNotContain("4.5", script);
        Assert.DoesNotContain("7.0", script);
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
        Assert.Contains("Advertiser workspace, Concierge, Brand DNA, and Color Intelligence", body);
        Assert.Contains("Concierge + Brand DNA Interpreter runs", body);
        Assert.Contains("Create Brand DNA proposal", body);
        Assert.Contains("Approve or reject Brand DNA", body);
        Assert.Contains("PHASE 3 · DETERMINISTIC COLOR INTELLIGENCE", body);
        Assert.Contains("Compute color profile", body);
        Assert.Contains("Approve or reject color profile", body);
        Assert.Contains("wedding-planner-color-compute-form", body);
        Assert.Contains("wedding-planner-color-decision-form", body);
        Assert.Contains("wedding-planner-color-profile-list", body);
        Assert.Contains("wedding-planner-color-inspect", body);
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
        Assert.Contains("/agent-runs", script);
        Assert.Contains("/brand-dna", script);
        Assert.Contains("/brand-dna/interpret", script);
        Assert.Contains("/decisions", script);
        Assert.Contains("/color-profiles/compute", script);
        Assert.Contains("/color-profiles", script);
        Assert.Contains("documentJson", script);
        Assert.Contains("contrastEvidence", script);
        Assert.Contains("geometryDisclaimer", script);
        Assert.Contains("inputSha256", script);
        Assert.Contains("approvedBrandDnaVersionId", script);
        Assert.Contains("currentApprovedColorProfileVersionId", script);
        Assert.Contains("submitWeddingPlannerColorCompute", script);
        Assert.Contains("submitWeddingPlannerColorDecision", script);
        Assert.Contains("renderWeddingPlannerColorInspect", script);
        Assert.Contains("Open primary workspace", body);
        Assert.Contains("Append a human message", body);
        Assert.DoesNotContain("Phase 1 does not invoke AI", body);
        Assert.DoesNotContain("TEST ENVIRONMENT", body);
        Assert.DoesNotContain("TEST_OPERATOR", body);
        Assert.DoesNotContain("relativeLuminance", script);
        Assert.DoesNotContain("srgbToLinear", script);
        Assert.DoesNotContain("fabricateColor", script);
        Assert.DoesNotContain("fakePalette", script);
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
