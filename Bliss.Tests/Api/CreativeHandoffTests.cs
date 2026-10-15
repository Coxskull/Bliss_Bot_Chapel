using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Operations;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bliss.Tests.Api;

public sealed class CreativeHandoffTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory factory;
    public CreativeHandoffTests(WeddingPlannerOidcFactory factory) => this.factory = factory;
    private sealed record DecisionResponse(CandidateApprovalDecision Decision);
    private sealed record PlanResponse(CreativeCampaignHandoff Handoff, bool IsReplay, bool CampaignActivated);
    private sealed class Scenario(WebApplicationFactory<Program> app, CreativeHandoffFixture fixture) : IAsyncDisposable
    {
        public WebApplicationFactory<Program> App = app;
        public CreativeHandoffFixture Fixture = fixture;
        public string Path => $"/api/wedding-planner/workspaces/{Fixture.Workspace.WorkspaceId}/handoff";
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
        using var scope = app.Services.CreateScope();
        var fixture = await CreativeHandoffTestData.Seed(scope.ServiceProvider.GetRequiredService<BlissDbContext>(), scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>());
        return new(app, fixture);
    }
    private async Task<HttpClient> Client(Scenario s, string roles, Guid? advertiser = null, string? identity = null)
    {
        var client = s.App.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Name", identity ?? $"Handoff {Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", roles);
        if (advertiser.HasValue) client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiser.Value.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken); return client;
    }
    private async Task<PlanCreativeHandoff> Approve(Scenario s, HttpClient owner, HttpClient reviewer)
    {
        async Task<Guid> Decide(HttpClient client, string stage, string key)
        {
            var response = await client.PostAsJsonAsync($"{s.Path}/candidates/{s.Fixture.Candidate}/approvals", CreativeHandoffTestData.Approval(s.Fixture, stage, key));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<DecisionResponse>())!.Decision.Id;
        }
        var advertiser = await Decide(owner, "ADVERTISER", "client-approval"); var qa = await Decide(reviewer, "ALPHA_QA", "qa-approval"); var fit = await Decide(reviewer, "INVENTORY_FIT", "fit-approval");
        return new(s.Fixture.Candidate, s.Fixture.Review, advertiser, qa, fit, Phase2DataSeeder.MatchBrazilApprovedId, s.Fixture.Campaign,
            CreativeHandoffFixture.Slot, s.Fixture.QuoteLine, "handoff-plan");
    }
    [Fact]
    public async Task Distinct_humans_plan_one_durable_handoff_using_only_accepted_economics()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        var result = await reviewer.PostAsJsonAsync(s.Path + "/plans", request); Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        var body = await result.Content.ReadFromJsonAsync<PlanResponse>(); Assert.False(body!.CampaignActivated);
        using var snapshot = JsonDocument.Parse(body.Handoff.SnapshotJson); Assert.Equal(s.Fixture.Amount, snapshot.RootElement.GetProperty("lineAmount").GetDecimal());
        Assert.Equal(s.Fixture.Candidate, snapshot.RootElement.GetProperty("id").GetGuid());
        var replay = await reviewer.PostAsJsonAsync(s.Path + "/plans", request); Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await replay.Content.ReadFromJsonAsync<PlanResponse>())!.IsReplay);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { CampaignId = Guid.NewGuid() })).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.CreativeCampaignHandoffs.ToListAsync()); Assert.Single(await db.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == body.Handoff.CampaignPlacementId);
        Assert.Equal("PLANNED", placement.Status); Assert.Null(placement.StartAt); Assert.Null(placement.EndAt);
        Assert.Equal("DRAFT", (await db.Campaigns.SingleAsync(x => x.Id == s.Fixture.Campaign)).Status);
        Assert.True((await db.AdInventorySlots.SingleAsync(x => x.Id == CreativeHandoffFixture.Slot)).IsAvailable);
    }

    [Fact]
    public async Task Standalone_reviewer_can_record_qa_and_inventory_fit_but_cannot_impersonate_advertiser_or_plan()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.reviewer"); // reviewer has no advertiser_id
        using var reader = await Client(s, "bliss.viewer", Phase2DataSeeder.Advertiser2Id);
        var decisionsPath = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";

        // Review scope does not widen normal workspace options or grant planner rights.
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync(s.Path + "/options")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.GetAsync(decisionsPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.PostAsJsonAsync(decisionsPath,
                CreativeHandoffTestData.Approval(s.Fixture, "ALPHA_QA", "viewer-qa"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reviewer.PostAsJsonAsync(decisionsPath,
                CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "reviewer-owner"))).StatusCode);

        var request = await Approve(s, owner, reviewer);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reviewer.PostAsJsonAsync(s.Path + "/plans", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reviewer.PostAsJsonAsync(s.Path + "/campaigns",
                new CreateHandoffCampaign(Guid.NewGuid(), Phase2DataSeeder.MatchBrazilApprovedId, "No reviewer campaign"))).StatusCode);

        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(3, await db.CandidateApprovalDecisions.CountAsync());
        Assert.Empty(await db.CreativeCampaignHandoffs.ToListAsync());
        Assert.Empty(await db.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
    }

    [Fact]
    public async Task Withholding_after_planning_preserves_one_not_sent_economics_snapshot()
    {
        await using var s = await Setup();
        using var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        using var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        var planned = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(HttpStatusCode.Created, planned.StatusCode);
        var original = await planned.Content.ReadFromJsonAsync<PlanResponse>();
        Assert.NotNull(original);
        Assert.False(original.CampaignActivated);
        var originalSnapshot = original.Handoff.SnapshotJson;
        using (var economics = JsonDocument.Parse(originalSnapshot))
            Assert.Equal(s.Fixture.Amount, economics.RootElement.GetProperty("lineAmount").GetDecimal());

        // The advertiser may revoke future use, but must not edit past audited
        // pricing or silently send/re-activate a previously planned placement.
        var withdrawal = await owner.PostAsJsonAsync(
            $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals",
            CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "withdraw-after-plan", 1, "WITHHOLD"));
        Assert.Equal(HttpStatusCode.Created, withdrawal.StatusCode);
        var attemptedSecondPlan = await reviewer.PostAsJsonAsync(
            s.Path + "/plans", request with { IdempotencyKey = "new-plan-after-withdrawal" });
        Assert.False(attemptedSecondPlan.IsSuccessStatusCode);

        var history = await reviewer.GetFromJsonAsync<List<CreativeCampaignHandoff>>(s.Path + "/plans");
        var retained = Assert.Single(history!);
        Assert.Equal(original.Handoff.Id, retained.Id);
        Assert.Equal(originalSnapshot, retained.SnapshotJson);
        Assert.Equal(original.Handoff.CampaignPlacementId, retained.CampaignPlacementId);

        using var scope = s.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.CreativeCampaignHandoffs.ToListAsync());
        Assert.Single(await db.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
        Assert.Single(await db.WeddingPlannerAuditEvents.Where(x => x.Action == "CREATIVE_HANDOFF_PLANNED").ToListAsync());
        Assert.Equal(2, await db.CandidateApprovalDecisions.CountAsync(x => x.Stage == "ADVERTISER"));
        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == retained.CampaignPlacementId);
        Assert.Equal("PLANNED", placement.Status);
        Assert.Null(placement.StartAt);
        Assert.Null(placement.EndAt);
        Assert.Equal("DRAFT", (await db.Campaigns.SingleAsync(x => x.Id == s.Fixture.Campaign)).Status);
        Assert.True((await db.AdInventorySlots.SingleAsync(x => x.Id == CreativeHandoffFixture.Slot)).IsAvailable);
    }

    [Fact]
    public async Task Advertiser_cannot_self_qa_or_plan_and_cross_tenant_anonymous_csrf_are_refused()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id);
        var other = await Client(s, "bliss.advertiser", Phase1DataSeeder.AdvertiserId); var reviewer = await Client(s, "bliss.admin");
        var path = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ALPHA_QA", "bad-qa"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "bad-owner"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "other-owner"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.App.CreateClient().GetAsync(path)).StatusCode);
        var request = await Approve(s, owner, reviewer);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + "/plans", request)).StatusCode);
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "csrf-owner", 1, "WITHHOLD"))).StatusCode);
        using var scope = s.App.Services.CreateScope(); Assert.Empty(await scope.ServiceProvider.GetRequiredService<BlissDbContext>().CreativeCampaignHandoffs.ToListAsync());
    }
    [Fact]
    public async Task One_principal_with_both_roles_cannot_supply_both_advertiser_and_qa_approval()
    {
        await using var s = await Setup(); var combined = await Client(s, "bliss.advertiser,bliss.admin", Phase2DataSeeder.Advertiser2Id, "Same human");
        var path = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";
        Assert.Equal(HttpStatusCode.Created, (await combined.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "dual-owner"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await combined.PostAsJsonAsync(path, CreativeHandoffTestData.Approval(s.Fixture, "ALPHA_QA", "dual-qa-approval"))).StatusCode);
    }
    [Theory]
    [InlineData("revoked")]
    [InlineData("inventory")]
    [InlineData("quote")]
    [InlineData("match")]
    [InlineData("new-candidate")]
    [InlineData("new-review")]
    [InlineData("new-brand")]
    [InlineData("quote-slot")]
    [InlineData("revised-quote")]
    public async Task Gate_changes_prevent_any_placement_or_handoff_write(string changed)
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        if (changed == "revoked") Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"{s.Path}/candidates/{s.Fixture.Candidate}/approvals", CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "revoke-owner", 1, "WITHHOLD"))).StatusCode);
        using (var scope = s.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            if (changed == "inventory") (await db.AdInventorySlots.SingleAsync(x => x.Id == CreativeHandoffFixture.Slot)).IsAvailable = false;
            if (changed == "quote") (await db.Quotes.SingleAsync(x => x.Id == s.Fixture.Quote)).Status = "DRAFT";
            if (changed == "revised-quote") (await db.Quotes.SingleAsync(x => x.Id == s.Fixture.Quote)).CurrentVersionNumber++;
            if (changed == "match") (await db.BlissMatches.SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId)).Status = EntityStatuses.ReviewRequired;
            var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var production = new AcademyProductionService(db, new BlueprintPhaseService(db, environment));
            if (changed == "new-candidate") await production.SubmitAsync(s.Fixture.Workspace, s.Fixture.Brief,
                new SubmitProductionCandidate(1, "Revised asset", "Test revision invalidates old approval", CreativeHandoffTestData.Png, "revised-candidate"), "ADVERTISER", "Fixture owner", "test", default);
            if (changed == "new-review") await production.ReviewAsync(s.Fixture.Workspace, s.Fixture.Brief, s.Fixture.Candidate,
                new RecordProductionReview(1, "PASS", "PASS", "PASS", "PASS", "PASS", false, "Fresh instructor assessment needs fresh approvals", "", "", "revised-review"), "OPERATOR", "Fixture QA", "test", default);
            if (changed == "new-brand") await new WeddingPlannerBrandDnaService(db).AppendAsync(s.Fixture.Workspace,
                new BrandDnaFields("New TEST brand", "pharmacy", "Taipei", "English", "Gold", "Montserrat", "Awareness", "Test"),
                2, "revised-brand", false, "ADVERTISER", "Fixture owner", "test", default);
            if (changed == "quote-slot")
            {
                var line = await db.QuoteLineItems.SingleAsync(x => x.Id == s.Fixture.QuoteLine);
                (await db.RateRecommendations.SingleAsync(x => x.Id == line.RateRecommendationId)).AdInventorySlotId = Guid.Parse("99999999-9999-9999-9999-999999999996");
            }
            await db.SaveChangesAsync();
        }
        var result = await reviewer.PostAsJsonAsync(s.Path + "/plans", request);
        Assert.Equal(changed is "new-candidate" or "new-brand" ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, result.StatusCode);
        using var verify = s.App.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Empty(await context.CreativeCampaignHandoffs.ToListAsync()); Assert.Empty(await context.CampaignPlacementRuns.Where(x => x.SourceSystem == "CREATIVE_HANDOFF_V1").ToListAsync());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unreviewed_and_pixel_candidates_cannot_receive_inventory_fit_approval(bool reviewedPixel)
    {
        await using var s = await Setup(); var reviewer = await Client(s, "bliss.admin"); Guid candidateId; Guid? reviewId = null;
        using (var scope = s.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var production = new AcademyProductionService(db, new BlueprintPhaseService(db, scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>()));
            candidateId = (await production.SubmitAsync(s.Fixture.Workspace, s.Fixture.Brief,
                new SubmitProductionCandidate(1, "Test new candidate", "Fixture", reviewedPixel ? AcademyProductionTests.Pixel : CreativeHandoffTestData.Png,
                    "unready-candidate"), "ADVERTISER", "Fixture owner", "test", default)).Row.Id;
            if (reviewedPixel) reviewId = (await production.ReviewAsync(s.Fixture.Workspace, s.Fixture.Brief, candidateId,
                new RecordProductionReview(0, "PASS", "PASS", "PASS", "PASS", "PASS", false, "Synthetic test only", "", "", "pixel-review"),
                "OPERATOR", "Fixture QA", "test", default)).Row.Id;
        }
        var request = CreativeHandoffTestData.Approval(s.Fixture, "INVENTORY_FIT", "unready-fit") with { ReviewId = reviewId };
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync($"{s.Path}/candidates/{candidateId}/approvals", request)).StatusCode);
        using var verify = s.App.Services.CreateScope(); Assert.Empty(await verify.ServiceProvider.GetRequiredService<BlissDbContext>().CandidateApprovalDecisions.ToListAsync());
    }
    [Fact]
    public async Task Approval_retries_conflict_on_changed_input_and_fit_requires_explicit_human_checks()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var path = $"{s.Path}/candidates/{s.Fixture.Candidate}/approvals";
        var approval = CreativeHandoffTestData.Approval(s.Fixture, "ADVERTISER", "retry-client-approval");
        Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync(path, approval)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync(path, approval)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, approval with { Rationale = "Changed intent" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, approval with { IdempotencyKey = "stale-approval" })).StatusCode);
        var fit = CreativeHandoffTestData.Approval(s.Fixture, "INVENTORY_FIT", "incomplete-fit");
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(path, fit with { CreatorSafeAreaVerified = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(path, fit with { AdInventorySlotId = Guid.Parse("99999999-9999-9999-9999-999999999998") })).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.CandidateApprovalDecisions.ToListAsync()); Assert.Empty(await db.CreativeCampaignHandoffs.ToListAsync());
    }
    [Fact]
    public async Task Staff_draft_campaign_creation_is_scoped_replayable_and_never_active()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var staff = await Client(s, "bliss.admin");
        var request = new CreateHandoffCampaign(Guid.NewGuid(), Phase2DataSeeder.MatchBrazilApprovedId, "TEST draft campaign");
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync(s.Path + "/campaigns", request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request with { BlissMatchId = Phase1DataSeeder.MatchAId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync(s.Path + "/campaigns", request with { Name = "Changed intent" })).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal("DRAFT", (await db.Campaigns.SingleAsync(x => x.Id == request.CampaignId)).Status);
        Assert.Single(await db.WeddingPlannerAuditEvents.Where(x => x.Action == "HANDOFF_CAMPAIGN_DRAFT_CREATED").ToListAsync());
    }
    [Fact]
    public async Task Missing_gates_incorrect_quote_graph_and_duplicate_quote_line_are_refused()
    {
        await using var s = await Setup(); var owner = await Client(s, "bliss.advertiser", Phase2DataSeeder.Advertiser2Id); var reviewer = await Client(s, "bliss.admin");
        var request = await Approve(s, owner, reviewer);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { AlphaQaApprovalId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { BlissMatchId = Phase1DataSeeder.MatchAId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { AdInventorySlotId = Guid.Parse("99999999-9999-9999-9999-999999999996") })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync(s.Path + "/plans", request with { IdempotencyKey = "different-plan-key" })).StatusCode);
    }
}
