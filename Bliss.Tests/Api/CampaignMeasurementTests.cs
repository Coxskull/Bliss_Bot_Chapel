using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Operations;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bliss.Tests.Api;

public sealed class CampaignMeasurementTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory factory;
    public CampaignMeasurementTests(WeddingPlannerOidcFactory factory) => this.factory = factory;
    private sealed record ObservationResponse(CampaignObservation Observation, bool IsReplay);
    private sealed record ProposalResponse(CampaignLearningProposal Proposal, bool IsReplay);
    private sealed record ReviewResponse(CampaignLearningReview Review, bool IsReplay);
    private sealed class Scenario(WebApplicationFactory<Program> app, CreativeHandoffFixture fixture, CreativeCampaignHandoff handoff) : IAsyncDisposable
    {
        public WebApplicationFactory<Program> App = app;
        public CreativeHandoffFixture Fixture = fixture;
        public CreativeCampaignHandoff Handoff = handoff;
        public string Path => $"/api/wedding-planner/workspaces/{Fixture.Workspace.WorkspaceId}/measurement";
        public ValueTask DisposeAsync() { App.Dispose(); return ValueTask.CompletedTask; }
    }
    private async Task<Scenario> Setup()
    {
        var name = Guid.NewGuid().ToString();
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<BlissDbContext>(); services.RemoveAll<DbContextOptions<BlissDbContext>>();
            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(name));
        }));
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var fixture = await CreativeHandoffTestData.Seed(db, environment);
        var plan = await CreativeHandoffTestData.Approve(db, environment, fixture);
        var handoff = (await new CreativeHandoffService(db, new AcademyProductionService(db, new BlueprintPhaseService(db, environment)), new CampaignPlacementService(db))
            .Plan(fixture.Workspace, plan, "fixture-planner", "Fixture planner", true, default)).Row;
        return new(app, fixture, handoff);
    }
    private async Task<HttpClient> Client(Scenario s, string roles, Guid? advertiser = null, string? identity = null)
    {
        var client = s.App.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Name", identity ?? $"Measurement {Guid.NewGuid():N}"); client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        if (advertiser.HasValue) client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiser.Value.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken); return client;
    }
    private static RecordCampaignObservation Observation(Scenario s, string kind = "LOCAL_VALIDATION")
    {
        var now = DateTime.UtcNow.AddSeconds(-1);
        return new(s.Handoff.Id, kind, "TEST workflow fixture", "Fictional evidence, not real campaign performance", now,
            kind == "EXTERNAL_REPORT" ? now.AddHours(-2) : null, kind == "EXTERNAL_REPORT" ? now.AddHours(-1) : null,
            kind == "EXTERNAL_REPORT" ? 100 : null, kind == "EXTERNAL_REPORT" ? 8 : null, null, kind == "EXTERNAL_REPORT", "test-observation");
    }
    private static ProposeCampaignLearning Proposal(Guid observation) => new(observation, "TEST improve readability", "Compare fictional layouts in a controlled review",
        "Independent human readability assessment", false, "test-learning-proposal");
    private static ReviewCampaignLearning Review => new(0, "ACCEPT_FOR_RESEARCH", "Research only, reported metrics remain unverified", false, "test-learning-review");

    [Fact]
    public async Task Observations_keep_frozen_asset_brand_quote_lineage_and_retries_do_not_duplicate_history()
    {
        await using var s = await Setup(); var staff = await Client(s, "bliss.operator"); var request = Observation(s);
        var response = await staff.PostAsJsonAsync(s.Path + "/observations", request); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var row = (await response.Content.ReadFromJsonAsync<ObservationResponse>())!.Observation;
        Assert.Equal(s.Handoff.SnapshotJson, row.LineageSnapshotJson); Assert.Equal(ControlledAiService.Hash(s.Handoff.SnapshotJson), row.LineageSha256);
        Assert.Null(row.Impressions); Assert.Null(row.Clicks); Assert.Null(row.Conversions);
        using (var scope = s.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new WeddingPlannerBrandDnaService(db).AppendAsync(s.Fixture.Workspace,
                new BrandDnaFields("Updated TEST brand", "pharmacy", "Taipei", "English", "Gold", "Montserrat", "Awareness", "Test"),
                2, "measurement-brand-update", false, "ADVERTISER", "Fixture owner", "test", default);
        }
        var replay = await staff.PostAsJsonAsync(s.Path + "/observations", request); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<ObservationResponse>())!.IsReplay);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(s.Path + "/observations", request with { Evidence = "Changed evidence" })).StatusCode);
        using var verify = s.App.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(row.LineageSnapshotJson, (await context.CampaignObservations.SingleAsync()).LineageSnapshotJson);
        Assert.Single(await context.WeddingPlannerAuditEvents.Where(x => x.Action == "CAMPAIGN_OBSERVATION_RECORDED").ToListAsync());
        Assert.Equal("NOT_SENT", (await context.CreativeCampaignHandoffs.SingleAsync()).Delivery);
    }
    [Fact]
    public async Task Independent_research_review_keeps_metrics_unverified_and_matching_pricing_and_delivery_unchanged()
    {
        await using var s = await Setup(); var author = await Client(s, "bliss.operator"); var reviewer = await Client(s, "bliss.admin");
        string before;
        using (var scope = s.App.Services.CreateScope()) before = await ProductionFingerprint(scope.ServiceProvider.GetRequiredService<BlissDbContext>());
        var observation = (await (await author.PostAsJsonAsync(s.Path + "/observations", Observation(s, "EXTERNAL_REPORT"))).Content.ReadFromJsonAsync<ObservationResponse>())!.Observation;
        Assert.Equal("UNVERIFIED_REPORT", observation.EvidenceStatus); Assert.Null(observation.Conversions);
        var proposalRequest = Proposal(observation.Id);
        var response = await author.PostAsJsonAsync(s.Path + "/proposals", proposalRequest); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var proposal = (await response.Content.ReadFromJsonAsync<ProposalResponse>())!.Proposal;
        Assert.Equal(HttpStatusCode.OK, (await author.PostAsJsonAsync(s.Path + "/proposals", proposalRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await author.PostAsJsonAsync(s.Path + "/proposals", proposalRequest with { Experiment = "Changed experiment" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await author.PostAsJsonAsync(s.Path + "/proposals", proposalRequest with { ApplyToProduction = true })).StatusCode);
        var path = $"{s.Path}/proposals/{proposal.Id}/reviews";
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(path, Review with { ApplyToProduction = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await reviewer.PostAsJsonAsync(path, Review)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reviewer.PostAsJsonAsync(path, Review)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(path, Review with { Rationale = "Changed review" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(path, Review with { IdempotencyKey = "stale-research-review" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await reviewer.PostAsJsonAsync(path, Review with { ExpectedVersion = 1, Decision = "REJECT", IdempotencyKey = "later-research-review" })).StatusCode);
        using var verify = s.App.Services.CreateScope(); var db = verify.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(before, await ProductionFingerprint(db));
        Assert.Equal("PROPOSED", (await db.CampaignLearningProposals.SingleAsync()).Status);
        Assert.Equal("UNVERIFIED_REPORT", (await db.CampaignObservations.SingleAsync()).EvidenceStatus);
        Assert.Equal(2, await db.CampaignLearningReviews.CountAsync());
    }
    private static async Task<string> ProductionFingerprint(BlissDbContext db) => JsonSerializer.Serialize(new {
        rules = await db.RuleVersions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(),
        matches = await db.BlissMatches.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Status, x.OverallScore }).ToListAsync(),
        rates = await db.RateRecommendations.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.RangeLow, x.RangeTarget, x.RangeHigh }).ToListAsync(),
        quotes = await db.Quotes.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Status, x.CurrentVersionNumber }).ToListAsync(),
        campaigns = await db.Campaigns.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Status }).ToListAsync(),
        handoffs = await db.CreativeCampaignHandoffs.AsNoTracking().OrderBy(x => x.Id).ToListAsync()
    });

    [Theory]
    [InlineData("missing-consent")]
    [InlineData("missing-window")]
    [InlineData("reversed-window")]
    [InlineData("negative-count")]
    [InlineData("excess-clicks")]
    [InlineData("missing-counts")]
    [InlineData("future-time")]
    [InlineData("local-metrics")]
    [InlineData("blank-source")]
    public async Task Invalid_reports_write_neither_observation_nor_append_audit(string invalid)
    {
        await using var s = await Setup(); var staff = await Client(s, "bliss.operator"); var request = Observation(s, "EXTERNAL_REPORT");
        request = invalid switch {
            "missing-consent" => request with { AcknowledgeUnverifiedReport = false },
            "missing-window" => request with { WindowStart = null },
            "reversed-window" => request with { WindowStart = request.WindowEnd },
            "negative-count" => request with { Clicks = -1 },
            "excess-clicks" => request with { Clicks = 101 },
            "missing-counts" => request with { Impressions = null, Clicks = null },
            "future-time" => request with { ObservedAt = DateTime.UtcNow.AddHours(1) },
            "local-metrics" => request with { Kind = "LOCAL_VALIDATION" },
            _ => request with { SourceLabel = " " }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync(s.Path + "/observations", request)).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Empty(await db.CampaignObservations.ToListAsync()); Assert.Empty(await db.WeddingPlannerAuditEvents.Where(x => x.Action == "CAMPAIGN_OBSERVATION_RECORDED").ToListAsync());
    }
    [Fact]
    public async Task Tenant_roles_anonymous_and_csrf_boundaries_are_enforced()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var staff = await Client(s, "bliss.operator");
        var other = await Client(s, "bliss.advertiser", Phase1DataSeeder.AdvertiserId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.App.CreateClient().GetAsync(s.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(s.Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(s.Path + "/observations", Observation(s, "ADVERTISER_FEEDBACK"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + "/observations", Observation(s))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + "/observations", Observation(s, "EXTERNAL_REPORT"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync(s.Path + "/observations", Observation(s, "ADVERTISER_FEEDBACK"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + "/proposals", Proposal(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + $"/proposals/{Guid.NewGuid()}/reviews", Review)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsJsonAsync(s.Path + "/observations", Observation(s) with { HandoffId = Guid.NewGuid(), IdempotencyKey = "missing-handoff" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsJsonAsync(s.Path + "/proposals", Proposal(Guid.NewGuid()))).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(s.Path + "/observations", Observation(s, "ADVERTISER_FEEDBACK") with { IdempotencyKey = "csrf-feedback" })).StatusCode);
    }
    [Fact]
    public async Task Principal_with_author_and_reviewer_roles_cannot_review_own_proposal()
    {
        await using var s = await Setup(); var combined = await Client(s, "bliss.admin", identity: "Same research author");
        var observation = (await (await combined.PostAsJsonAsync(s.Path + "/observations", Observation(s))).Content.ReadFromJsonAsync<ObservationResponse>())!.Observation;
        var proposal = (await (await combined.PostAsJsonAsync(s.Path + "/proposals", Proposal(observation.Id))).Content.ReadFromJsonAsync<ProposalResponse>())!.Proposal;
        Assert.Equal(HttpStatusCode.Forbidden, (await combined.PostAsJsonAsync($"{s.Path}/proposals/{proposal.Id}/reviews", Review)).StatusCode);
    }
    [Fact]
    public async Task Readiness_is_staff_only_and_does_not_equate_development_or_configuration_with_hosted_acceptance()
    {
        await using var s = await Setup(); var staff = await Client(s, "bliss.operator"); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        const string path = "/api/operations/campaign-readiness";
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.App.CreateClient().GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync(path)).StatusCode);
        var response = await staff.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.False(body.RootElement.GetProperty("hostedAcceptanceClaimed").GetBoolean());
        Assert.False(body.RootElement.GetProperty("productionGatesApplied").GetBoolean());
        Assert.Contains(body.RootElement.GetProperty("checks").EnumerateArray(), x => x.GetProperty("area").GetString() == "HOSTED_RECOVERY" && x.GetProperty("status").GetString() == "PENDING_HOSTED_VALIDATION");
        Assert.DoesNotContain("password=", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
