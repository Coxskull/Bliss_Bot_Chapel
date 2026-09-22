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
        Assert.Contains("PHASE 4 · THE CURATOR", body);
        Assert.Contains("curator-panel", body);
        Assert.Contains("curator-job-form", body);
        Assert.Contains("curator-decision-form", body);
        Assert.Contains("Evidence-backed research", body);
        Assert.Contains("Eight logical roles map to 3 workers/runs, not 8 subscriptions", body);
        Assert.Contains("never presented as live research", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Research approval only", body);
        Assert.Contains("PHASE 5 · CONCEPT / PROTOTYPE WORKSHOP", body);
        Assert.Contains("workshop-panel", body);
        Assert.Contains("workshop-job-form", body);
        Assert.Contains("workshop-decision-form", body);
        Assert.Contains("Concept direction workshop", body);
        Assert.Contains("Four logical roles map to 3 workers/runs, not 4 subscriptions", body);
        Assert.Contains("no generated image/assets", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not campaign-ready", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SYNTHETIC DEVELOPMENT PROTOTYPE", body);
        Assert.Contains("concept-direction approval only", body, StringComparison.OrdinalIgnoreCase);
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

        var styles = await (await client.GetAsync("/wedding-planner.css")).Content.ReadAsStringAsync();
        Assert.Contains("[hidden] { display: none !important; }", styles);
    }

    [Fact]
    public async Task Public_planner_script_wires_phase4_curator_from_server_documents_only()
    {
        var client = _factory.CreateClient();
        var script = await (await client.GetAsync("/wedding-planner.js")).Content.ReadAsStringAsync();

        Assert.Contains("PHASE 4 · THE CURATOR", script);
        Assert.Contains("/research-jobs", script);
        Assert.Contains("/research-reports", script);
        Assert.Contains("/research-reports/", script);
        Assert.Contains("/agent-runs", script);
        Assert.Contains("/decisions", script);
        Assert.Contains("canSubmitResearchJobs", script);
        Assert.Contains("currentApprovedBrandDnaVersionId", script);
        Assert.Contains("parseResearchDocument", script);
        Assert.Contains("documentJson", script);
        Assert.Contains("Eight logical roles map to 3 workers/runs, not 8 subscriptions", script);
        Assert.Contains("MARKET_LANDSCAPE_RESEARCHER", script);
        Assert.Contains("RESEARCH_SYNTHESIZER", script);
        Assert.Contains("CURATOR_RESEARCH_V1", script);
        Assert.Contains("CURATOR_EVIDENCE_V1", script);
        Assert.Contains("CURATOR_SYNTHESIS_RISK_V1", script);
        Assert.Contains("RUNNING", script);
        Assert.Contains("SUCCEEDED", script);
        Assert.Contains("FAILED", script);
        Assert.Contains("PROPOSED", script);
        Assert.Contains("APPROVED", script);
        Assert.Contains("CURRENT", script);
        Assert.Contains("SYNTHETIC", script);
        Assert.Contains(".invalid", script);
        Assert.Contains("not a retry", script);
        Assert.Contains("research approval only", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-CSRF-TOKEN", script);
        Assert.Contains("idempotencyKey", script);
        Assert.Contains("citationSourceIds", script);
        Assert.Contains("workerProfileVersion", script);
        Assert.Contains("promptPackVersion", script);
        Assert.Contains("estimatedCostUsd", script);
        Assert.Contains("target=\"_blank\"", script);
        Assert.Contains("noopener noreferrer nofollow", script);
        Assert.Contains("never presented as live research", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Approval of this report is research approval only. It is not creative, campaign, claim, legal, matching, accessibility, or compliance approval.",
            script);

        Assert.DoesNotContain("fetch(citation", script);
        Assert.DoesNotContain("fetch(source.url", script);
        Assert.DoesNotContain("fetch(url)", script);
        Assert.DoesNotContain("inventFinding", script);
        Assert.DoesNotContain("fabricateFinding", script);
        Assert.DoesNotContain("fakeCitation", script);
        Assert.DoesNotContain("clientGeneratedFinding", script);
        Assert.DoesNotContain("generateCitation", script);
        Assert.DoesNotContain("crawlUrl", script);
        Assert.DoesNotContain("eight fake agent runs", script);

        var styles = await (await client.GetAsync("/wedding-planner.css")).Content.ReadAsStringAsync();
        Assert.Contains("[hidden] { display: none !important; }", styles);
        Assert.Contains("curator-panel", styles);
        Assert.Contains("synthetic-badge", styles);
    }

    [Fact]
    public async Task Public_planner_script_wires_phase5_concept_workshop_from_server_documents_only()
    {
        var client = _factory.CreateClient();
        var script = await (await client.GetAsync("/wedding-planner.js")).Content.ReadAsStringAsync();

        Assert.Contains("PHASE 5 · CONCEPT / PROTOTYPE WORKSHOP", script);
        Assert.Contains("/workshop-jobs", script);
        Assert.Contains("/concept-packages", script);
        Assert.Contains("/concept-packages/", script);
        Assert.Contains("/contributions", script);
        Assert.Contains("/agent-runs", script);
        Assert.Contains("/decisions", script);
        Assert.Contains("canSubmitWorkshopJobs", script);
        Assert.Contains("currentApprovedBrandDnaVersionId", script);
        Assert.Contains("currentApprovedColorProfileVersionId", script);
        Assert.Contains("currentApprovedResearchReportVersionId", script);
        Assert.Contains("parseConceptPackageDocument", script);
        Assert.Contains("documentJson", script);
        Assert.Contains("Four logical roles map to 3 workers/runs, not 4 subscriptions", script);
        Assert.Contains("BRAND_STRATEGIST", script);
        Assert.Contains("ART_DIRECTOR", script);
        Assert.Contains("COPYWRITER", script);
        Assert.Contains("PRODUCTION_ARTIST", script);
        Assert.Contains("CONCEPT_STRATEGY_V1", script);
        Assert.Contains("CONCEPT_CREATIVE_V1", script);
        Assert.Contains("PROTOTYPE_PRODUCTION_V1", script);
        Assert.Contains("STATIC_SOCIAL_SQUARE", script);
        Assert.Contains("STATIC_SOCIAL_STORY", script);
        Assert.Contains("STATIC_DISPLAY_BANNER", script);
        Assert.Contains("EMAIL_HERO", script);
        Assert.Contains("concept_1", script);
        Assert.Contains("concept_2", script);
        Assert.Contains("concept_3", script);
        Assert.Contains("selectedConceptId", script);
        Assert.Contains("CREATIVE_NON_FACTUAL", script);
        Assert.Contains("factualClaims", script);
        Assert.Contains("sourceIds", script);
        Assert.Contains("paletteRoleRefs", script);
        Assert.Contains("LOFI_STACK_V1", script);
        Assert.Contains("LOFI_SPLIT_V1", script);
        Assert.Contains("LOFI_BANNER_V1", script);
        Assert.Contains("textRef", script);
        Assert.Contains("copy.headline", script);
        Assert.Contains("copy.body", script);
        Assert.Contains("copy.cta", script);
        Assert.Contains("renderSafePrototypePreview", script);
        Assert.Contains("assetPlaceholder", script);
        Assert.Contains("SYNTHETIC DEVELOPMENT PROTOTYPE", script);
        Assert.Contains("concept-direction approval only", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no generated image/assets", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not campaign-ready", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-CSRF-TOKEN", script);
        Assert.Contains("idempotencyKey", script);
        Assert.Contains("workerProfileVersion", script);
        Assert.Contains("promptPackVersion", script);
        Assert.Contains("estimatedCostUsd", script);
        Assert.Contains("approvedColorProfileVersionId", script);
        Assert.Contains("/color-profiles/", script);
        Assert.Contains(
            "Approval of this package is concept-direction approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, asset, QA, or production-artwork approval.",
            script);
        Assert.Contains("Missing workshop prerequisites", script);
        Assert.Contains("REJECT forbids", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("APPROVE requires", script, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("<img", script);
        Assert.DoesNotContain("fetch(media", script);
        Assert.DoesNotContain("fetch(image", script);
        Assert.DoesNotContain("fetch(asset", script);
        Assert.DoesNotContain("inventConcept", script);
        Assert.DoesNotContain("fabricateConcept", script);
        Assert.DoesNotContain("fakeConcept", script);
        Assert.DoesNotContain("clientGeneratedConcept", script);
        Assert.DoesNotContain("generateConcept", script);
        Assert.DoesNotContain("eval(", script);
        Assert.DoesNotContain("innerHTML = concept.html", script);
        Assert.DoesNotContain("four fake agent runs", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("campaignReady", script);
        Assert.DoesNotContain("matchId", script);
        Assert.DoesNotContain("placementId", script);
        Assert.DoesNotContain("inventoryId", script);

        var styles = await (await client.GetAsync("/wedding-planner.css")).Content.ReadAsStringAsync();
        Assert.Contains("[hidden] { display: none !important; }", styles);
        Assert.Contains("workshop-panel", styles);
        Assert.Contains("workshop-prototype-frame", styles);
        Assert.Contains("workshop-placeholder", styles);
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
        Assert.Contains("Advertiser workspace, Concierge, Brand DNA, Color Intelligence, Curator research, and Concept Workshop", body);
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
        Assert.Contains("PHASE 4 · THE CURATOR", body);
        Assert.Contains("wedding-planner-research-job-form", body);
        Assert.Contains("wedding-planner-research-decision-form", body);
        Assert.Contains("wedding-planner-research-job-list", body);
        Assert.Contains("wedding-planner-research-report-list", body);
        Assert.Contains("wedding-planner-research-inspect", body);
        Assert.Contains("Eight logical roles map to 3 workers/runs, not 8 subscriptions", body);
        Assert.Contains("Approve or reject research report", body);
        Assert.Contains("RUNNING · SUCCEEDED · FAILED", body);
        Assert.Contains("PHASE 5 · CONCEPT / PROTOTYPE WORKSHOP", body);
        Assert.Contains("wedding-planner-workshop-job-form", body);
        Assert.Contains("wedding-planner-workshop-decision-form", body);
        Assert.Contains("wedding-planner-workshop-job-list", body);
        Assert.Contains("wedding-planner-workshop-package-list", body);
        Assert.Contains("wedding-planner-workshop-inspect", body);
        Assert.Contains("Four logical roles map to 3 workers/runs, not 4 subscriptions", body);
        Assert.Contains("Approve or reject concept package", body);
        Assert.Contains("SYNTHETIC DEVELOPMENT PROTOTYPE", body);
        Assert.Contains("concept-direction approval only", body, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("/research-jobs", script);
        Assert.Contains("/research-reports", script);
        Assert.Contains("submitWeddingPlannerResearchJob", script);
        Assert.Contains("submitWeddingPlannerResearchDecision", script);
        Assert.Contains("renderWeddingPlannerResearchInspect", script);
        Assert.Contains("Wedding Planner Phase 5", script);
        Assert.Contains("parseWeddingPlannerResearchDocument", script);
        Assert.Contains("Eight logical roles map to 3 workers/runs, not 8 subscriptions", script);
        Assert.Contains("SYNTHETIC", script);
        Assert.Contains(".invalid", script);
        Assert.Contains("not a retry", script);
        Assert.Contains("research approval only", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("noopener noreferrer nofollow", script);
        Assert.Contains("currentApprovedResearchReportVersionId", script);
        Assert.Contains("workerProfileVersion", script);
        Assert.Contains("workspaces/${workspaceId}/agent-runs", script);
        Assert.Contains("Wedding Planner Phase 5", script);
        Assert.Contains("/workshop-jobs", script);
        Assert.Contains("/concept-packages", script);
        Assert.Contains("submitWeddingPlannerWorkshopJob", script);
        Assert.Contains("submitWeddingPlannerWorkshopDecision", script);
        Assert.Contains("renderWeddingPlannerWorkshopInspect", script);
        Assert.Contains("parseWeddingPlannerConceptPackageDocument", script);
        Assert.Contains("renderOpsSafePrototypePreview", script);
        Assert.Contains("Four logical roles map to 3 workers/runs, not 4 subscriptions", script);
        Assert.Contains("BRAND_STRATEGIST", script);
        Assert.Contains("CONCEPT_STRATEGY_V1", script);
        Assert.Contains("selectedConceptId", script);
        Assert.Contains("SYNTHETIC DEVELOPMENT PROTOTYPE", script);
        Assert.Contains("concept-direction approval only", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("currentApprovedConceptPackageVersionId", script);
        Assert.Contains("inputSha256", script);
        Assert.Contains("LOFI_STACK_V1", script);
        Assert.Contains("copy.headline", script);
        Assert.DoesNotContain("fetch(citation", script);
        Assert.DoesNotContain("inventFinding", script);
        Assert.DoesNotContain("fabricateFinding", script);
        Assert.DoesNotContain("fakeCitation", script);
        Assert.DoesNotContain("generateCitation", script);
        Assert.DoesNotContain("crawlUrl", script);
        Assert.DoesNotContain("<img", script);
        Assert.DoesNotContain("fetch(media", script);
        Assert.DoesNotContain("inventConcept", script);
        Assert.DoesNotContain("fabricateConcept", script);
        Assert.DoesNotContain("fakeConcept", script);
        Assert.DoesNotContain("generateConcept", script);
        Assert.DoesNotContain("eval(", script);
        Assert.Contains("Open primary workspace", body);
        Assert.Contains("Append a human message", body);
        Assert.DoesNotContain("Phase 1 does not invoke AI", body);
        Assert.DoesNotContain("TEST ENVIRONMENT", body);
        Assert.DoesNotContain("TEST_OPERATOR", body);
        Assert.DoesNotContain("relativeLuminance", script);
        Assert.DoesNotContain("srgbToLinear", script);
        Assert.DoesNotContain("fabricateColor", script);
        Assert.DoesNotContain("fakePalette", script);

        var styles = await (await client.GetAsync("/operations/app.css")).Content.ReadAsStringAsync();
        Assert.Contains("[hidden]", styles);
        Assert.Contains("grid-template-columns: 34px minmax(0, 1fr) auto", styles);
        Assert.Contains("overflow-wrap: anywhere", styles);
        Assert.Contains("workshop-prototype-frame", styles);
        Assert.Contains("workshop-placeholder", styles);
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
