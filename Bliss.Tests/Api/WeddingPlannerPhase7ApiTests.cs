using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bliss.Api.Contracts;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase7ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase7ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Qa_happy_path_two_runs_three_contributions_accept_and_pointer()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"p7-{Guid.NewGuid():N}"[..16];
        var workspaceId = await OpenWorkspaceWithApprovedCreativeAsync(client, key);

        var jobKey = $"qj-{key}";
        jobKey = jobKey[..Math.Min(jobKey.Length, WeddingPlannerQaIdempotency.MaxJobIdempotencyKeyLength)];
        var create = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/qa-review-jobs",
            new CreateWeddingPlannerQaReviewJobRequest(
                "Control review selected variant",
                [WeddingPlannerQaFocusAreas.Copy, WeddingPlannerQaFocusAreas.Visual, WeddingPlannerQaFocusAreas.Provenance],
                null,
                "DashboardFixture",
                jobKey));
        var job = await create.Content.ReadFromJsonAsync<WeddingPlannerQaReviewJobDto>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("SUCCEEDED", job!.Status);
        Assert.NotNull(job.OutputQaReviewReportVersionId);
        Assert.NotNull(job.RulesFindingsJson);

        var replay = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/qa-review-jobs",
            new CreateWeddingPlannerQaReviewJobRequest(
                "ignored", ["COPY"], null, "DashboardFixture", jobKey));
        var replayed = await replay.Content.ReadFromJsonAsync<WeddingPlannerQaReviewJobDto>();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replayed!.IsReplay);
        Assert.Equal(job.QaReviewJobId, replayed.QaReviewJobId);

        var reportRuns = await client.GetFromJsonAsync<List<WeddingPlannerAgentRunDto>>(
            $"/api/wedding-planner/qa-review-reports/{job.OutputQaReviewReportVersionId}/agent-runs");
        Assert.Equal(2, reportRuns!.Count);
        Assert.All(reportRuns, r => Assert.Equal("SUCCEEDED", r.Status));
        Assert.Equal(1, reportRuns.Count(r => r.OutputQaReviewReportVersionId is not null));
        Assert.DoesNotContain(reportRuns, r =>
            r.WorkerProfileVersion is "CREATIVE_DIRECTION_V1" or "VARIANT_PRODUCTION_V1");

        var contributions = await client.GetFromJsonAsync<List<WeddingPlannerQaRoleContributionDto>>(
            $"/api/wedding-planner/qa-review-reports/{job.OutputQaReviewReportVersionId}/contributions");
        Assert.Equal(3, contributions!.Count);
        Assert.Equal(
            WeddingPlannerQaLogicalRoles.AllInOrder.ToArray(),
            contributions.Select(c => c.LogicalRole).ToArray());
        Assert.Null(contributions.Single(c => c.LogicalRole == WeddingPlannerQaLogicalRoles.HumanEscalationSteward).ProducingAgentRunId);
        Assert.Equal(
            WeddingPlannerQaContributionSources.RulesHuman,
            contributions.Single(c => c.LogicalRole == WeddingPlannerQaLogicalRoles.HumanEscalationSteward).ContributionSource);

        var report = await client.GetFromJsonAsync<WeddingPlannerQaReviewReportVersionDto>(
            $"/api/wedding-planner/qa-review-reports/{job.OutputQaReviewReportVersionId}");
        Assert.Contains(WeddingPlannerQaReviewReportDisclaimer.Text, report!.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview, report.DocumentJson, StringComparison.Ordinal);

        var accept = await client.PostAsJsonAsync(
            $"/api/wedding-planner/qa-review-reports/{job.OutputQaReviewReportVersionId}/decisions",
            new WeddingPlannerQaReviewDecisionRequest(
                "ACCEPT",
                "Selected variant passes control review after visual check.",
                "variant_1",
                true,
                true,
                true,
                true,
                null,
                "DashboardFixture",
                $"ap-{key}"));
        var decision = await accept.Content.ReadFromJsonAsync<WeddingPlannerQaReviewDecisionDto>();
        Assert.Equal(HttpStatusCode.Created, accept.StatusCode);
        Assert.True(decision!.Version.IsCurrentAccepted);
        Assert.Equal("ACCEPTED", decision.Version.Status);
    }

    [Fact]
    public async Task Requires_approved_creative_and_rejects_forbidden_fields()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString("N")[..12];

        Guid workspaceId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var advertiser = new Bliss.Domain.Entities.Advertiser
            {
                Id = Guid.NewGuid(),
                Name = $"NoCreative {key}",
                CreatedAt = DateTime.UtcNow
            };
            db.Advertisers.Add(advertiser);
            await db.SaveChangesAsync();
            var workspace = await (await client.PostAsJsonAsync(
                "/api/wedding-planner/workspaces",
                new OpenWeddingPlannerWorkspaceRequest(advertiser.Id, "DashboardFixture", $"ws-empty-{key}")))
                .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
            workspaceId = workspace!.WorkspaceId;
        }

        var missing = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/qa-review-jobs",
            new CreateWeddingPlannerQaReviewJobRequest(
                "O", ["COPY"], null, "DashboardFixture", $"job-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var ready = await OpenWorkspaceWithApprovedCreativeAsync(client, $"rdy-{key}");
        var forbiddenBody = $$"""
            {"reviewObjective":"O","focusAreas":["COPY"],"campaignId":"camp-1","sourceSystem":"DashboardFixture","idempotencyKey":"forbid-{{key}}"}
            """;
        var forbidden = await client.PostAsync(
            $"/api/wedding-planner/workspaces/{ready}/qa-review-jobs",
            new StringContent(forbiddenBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }

    private static async Task<Guid> OpenWorkspaceWithApprovedCreativeAsync(HttpClient client, string key)
    {
        var workspace = await (await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(Phase1DataSeeder.AdvertiserId, "DashboardFixture", $"ws-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var session = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("DashboardFixture", $"session-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session!.SessionId}/turns",
            new WeddingPlannerTurnRequest("voice: calm. audience: couples. market: Manila.", "DashboardFixture", $"t1-{key}"));
        var dna = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("DashboardFixture", $"interp-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{dna!.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "Approved", "DashboardFixture", $"dna-ap-{key}"));

        var color = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#336699", null, null, null, null, null, "DashboardFixture", $"color-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerColorProfileVersionDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/color-profiles/{color!.ColorProfileVersionId}/decisions",
            new WeddingPlannerColorProfileDecisionRequest("APPROVE", "ok", "DashboardFixture", $"color-ap-{key}"));

        var researchKey = $"r-{key}";
        researchKey = researchKey[..Math.Min(researchKey.Length, WeddingPlannerCuratorIdempotency.MaxJobIdempotencyKeyLength)];
        var research = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "Manila demand", "Signals", ["Who books?"], "Manila", "en", null,
                "DashboardFixture", researchKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerResearchJobDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/research-reports/{research!.OutputResearchReportVersionId}/decisions",
            new WeddingPlannerResearchReportDecisionRequest("APPROVE", "ok", "DashboardFixture", $"res-ap-{key}"));

        var workshopKey = $"wj-{key}";
        workshopKey = workshopKey[..Math.Min(workshopKey.Length, WeddingPlannerConceptWorkshopIdempotency.MaxJobIdempotencyKeyLength)];
        var workshop = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "Objective for calm social",
                "Grow venue inquiries",
                "Engaged couples",
                WeddingPlannerChannelFormats.StaticSocialSquare,
                ["Static square creative"],
                "Start planning",
                Array.Empty<string>(),
                null,
                null,
                "DashboardFixture",
                workshopKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkshopJobDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/concept-packages/{workshop!.OutputConceptPackageVersionId}/decisions",
            new WeddingPlannerConceptPackageDecisionRequest(
                "APPROVE", "Concept 1 fits", "concept_1", "DashboardFixture", $"concept-ap-{key}"));

        var creativeKey = $"cj-{key}";
        creativeKey = creativeKey[..Math.Min(creativeKey.Length, WeddingPlannerCreativeDepartmentIdempotency.MaxJobIdempotencyKeyLength)];
        var creative = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL",
                "Draft calm square",
                [WeddingPlannerChannelFormats.StaticSocialSquare],
                1,
                null,
                null,
                null,
                null,
                "DashboardFixture",
                creativeKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/creative-packages/{creative!.OutputCreativePackageVersionId}/decisions",
            new WeddingPlannerCreativePackageDecisionRequest(
                "APPROVE", "Variant 1", "variant_1", "DashboardFixture", $"creative-ap-{key}"));

        return workspace.WorkspaceId;
    }
}

public sealed class WeddingPlannerPhase7IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase7IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Oidc_advertiser_decision_403_reviewer_decide_but_creative_write_403_reviewer_waive_403()
    {
        await SeedAsync();
        var dental = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.DentalManilaId, "Dental Manila");
        var restaurant = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "Restaurant Santo Domingo");
        var reviewer = await CreateRoleClientAsync("bliss.reviewer", "Reviewer");
        var operatorClient = await CreateRoleClientAsync("bliss.operator", "Operator");

        var dentalWs = await OpenReadyCreativeWorkspaceAsync(dental, "d7");
        var jobKey = $"qj-{Guid.NewGuid():N}";
        jobKey = jobKey[..Math.Min(jobKey.Length, WeddingPlannerQaIdempotency.MaxJobIdempotencyKeyLength)];
        var job = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/qa-review-jobs",
            new CreateWeddingPlannerQaReviewJobRequest(
                "O", ["COPY", "VISUAL"], null, "SECURITY_TEST", jobKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerQaReviewJobDto>();

        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/qa-review-jobs/{job!.QaReviewJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/qa-review-reports/{job.OutputQaReviewReportVersionId}")).StatusCode);

        // Reviewer can visually review the pinned selected PNG via same-origin asset content.
        var meta = await reviewer.GetAsync($"/api/wedding-planner/creative-assets/{job.SelectedCreativeAssetId}");
        Assert.Equal(HttpStatusCode.OK, meta.StatusCode);
        var content = await reviewer.GetAsync($"/api/wedding-planner/creative-assets/{job.SelectedCreativeAssetId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/png", content.Content.Headers.ContentType!.MediaType);

        Assert.Equal(HttpStatusCode.Forbidden, (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/qa-review-reports/{job.OutputQaReviewReportVersionId}/decisions",
            new WeddingPlannerQaReviewDecisionRequest(
                "ACCEPT", "no", "variant_1", true, true, true, true, null, "SECURITY_TEST", "adv-dec"))).StatusCode);

        var reviewerAccept = await reviewer.PostAsJsonAsync(
            $"/api/wedding-planner/qa-review-reports/{job.OutputQaReviewReportVersionId}/decisions",
            new WeddingPlannerQaReviewDecisionRequest(
                "RETURN_FOR_REVISION", "needs work", "variant_1", null, null, null, null, null, "SECURITY_TEST", "rev-ret"));
        Assert.Equal(HttpStatusCode.Created, reviewerAccept.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "SECURITY_TEST", $"rj-{Guid.NewGuid():N}"[..20]))).StatusCode);

        // Reviewer still cannot approve a creative package (generic Phase 6 write remains closed).
        var packages = await dental.GetFromJsonAsync<WeddingPlannerCreativePackageListDto>(
            $"/api/wedding-planner/workspaces/{dentalWs}/creative-packages");
        var proposedOrApproved = packages!.Versions.First();
        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync(
            $"/api/wedding-planner/creative-packages/{proposedOrApproved.CreativePackageVersionId}/decisions",
            new WeddingPlannerCreativePackageDecisionRequest(
                "APPROVE", "no", "variant_1", "SECURITY_TEST", $"rev-pkg-{Guid.NewGuid():N}"[..20]))).StatusCode);

        var job2Key = $"qj2-{Guid.NewGuid():N}";
        job2Key = job2Key[..Math.Min(job2Key.Length, WeddingPlannerQaIdempotency.MaxJobIdempotencyKeyLength)];
        var job2 = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/qa-review-jobs",
            new CreateWeddingPlannerQaReviewJobRequest(
                "O2", ["COPY"], null, "SECURITY_TEST", job2Key)))
            .Content.ReadFromJsonAsync<WeddingPlannerQaReviewJobDto>();
        var escalate = await operatorClient.PostAsJsonAsync(
            $"/api/wedding-planner/qa-review-reports/{job2!.OutputQaReviewReportVersionId}/decisions",
            new WeddingPlannerQaReviewDecisionRequest(
                "ESCALATE", "boundary", "variant_1", null, null, null, null,
                WeddingPlannerQaEscalationCategories.Provenance, "SECURITY_TEST", "op-esc"));
        var escDto = await escalate.Content.ReadFromJsonAsync<WeddingPlannerQaReviewDecisionDto>();
        Assert.Equal(HttpStatusCode.Created, escalate.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await reviewer.PostAsJsonAsync(
            $"/api/wedding-planner/qa-escalation-cases/{escDto!.EscalationCaseId}/resolutions",
            new WeddingPlannerQaEscalationResolutionRequest(
                "WAIVE_AND_ACCEPT",
                "no",
                "exception",
                true,
                [],
                "SECURITY_TEST",
                "rev-waive"))).StatusCode);

        var waive = await operatorClient.PostAsJsonAsync(
            $"/api/wedding-planner/qa-escalation-cases/{escDto.EscalationCaseId}/resolutions",
            new WeddingPlannerQaEscalationResolutionRequest(
                "WAIVE_AND_ACCEPT",
                "Operator exception after documented review.",
                "Blockers acknowledged; temporary waiver for control path only.",
                true,
                WeddingPlannerQaReviewValidation.ExtractBlockerCodes(job2.RulesFindingsJson!).ToArray(),
                "SECURITY_TEST",
                "op-waive"));
        Assert.Equal(HttpStatusCode.Created, waive.StatusCode);

        var anonymous = _factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            $"/api/wedding-planner/qa-review-jobs/{job.QaReviewJobId}")).StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }

    private static async Task<Guid> OpenReadyCreativeWorkspaceAsync(HttpClient client, string prefix)
    {
        var key = $"{prefix}-{Guid.NewGuid():N}"[..14];
        var advertiserId = Guid.Parse(client.DefaultRequestHeaders.GetValues("X-Test-Advertiser-Id").Single());
        var workspace = await (await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(advertiserId, "SECURITY_TEST", $"ws-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();

        var session = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("SECURITY_TEST", $"s-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session!.SessionId}/turns",
            new WeddingPlannerTurnRequest("voice: calm.", "SECURITY_TEST", $"t-{key}"));
        var dna = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("SECURITY_TEST", $"dna-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{dna!.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "ok", "SECURITY_TEST", $"dna-ap-{key}"));
        var color = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#336699", null, null, null, null, null, "SECURITY_TEST", $"color-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerColorProfileVersionDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/color-profiles/{color!.ColorProfileVersionId}/decisions",
            new WeddingPlannerColorProfileDecisionRequest("APPROVE", "ok", "SECURITY_TEST", $"color-ap-{key}"));
        var researchKey = $"res-{key}";
        researchKey = researchKey[..Math.Min(researchKey.Length, WeddingPlannerCuratorIdempotency.MaxJobIdempotencyKeyLength)];
        var research = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "T", "O", ["Q"], "G", "en", null, "SECURITY_TEST", researchKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerResearchJobDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/research-reports/{research!.OutputResearchReportVersionId}/decisions",
            new WeddingPlannerResearchReportDecisionRequest("APPROVE", "ok", "SECURITY_TEST", $"res-ap-{key}"));
        var workshopKey = $"wj-{key}";
        workshopKey = workshopKey[..Math.Min(workshopKey.Length, WeddingPlannerConceptWorkshopIdempotency.MaxJobIdempotencyKeyLength)];
        var workshop = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "O", "G", "A", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["d"], "cta", null, null, null, "SECURITY_TEST", workshopKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkshopJobDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/concept-packages/{workshop!.OutputConceptPackageVersionId}/decisions",
            new WeddingPlannerConceptPackageDecisionRequest(
                "APPROVE", "ok", "concept_1", "SECURITY_TEST", $"concept-ap-{key}"));
        var creativeKey = $"cj-{key}";
        creativeKey = creativeKey[..Math.Min(creativeKey.Length, WeddingPlannerCreativeDepartmentIdempotency.MaxJobIdempotencyKeyLength)];
        var creative = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace.WorkspaceId}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "SECURITY_TEST", creativeKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();
        await client.PostAsJsonAsync(
            $"/api/wedding-planner/creative-packages/{creative!.OutputCreativePackageVersionId}/decisions",
            new WeddingPlannerCreativePackageDecisionRequest(
                "APPROVE", "ok", "variant_1", "SECURITY_TEST", $"creative-ap-{key}"));
        return workspace.WorkspaceId;
    }

    private async Task<HttpClient> CreateAdvertiserClientAsync(Guid advertiserId, string name)
    {
        var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.advertiser");
        client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiserId.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session?.CsrfToken);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
        return client;
    }

    private async Task<HttpClient> CreateRoleClientAsync(string role, string name)
    {
        var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session?.CsrfToken);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
        return client;
    }
}

public sealed class WeddingPlannerPhase7BoundaryTests
{
    [Fact]
    public void Phase7_sources_do_not_couple_to_matching_or_third_steward_run_or_campaign_ready()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerQaReviewValidation.cs"),
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerQaRulesEngine.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerQaReviewOrchestrationService.cs")
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("DeterministicRuleEvaluator", source);
            Assert.DoesNotContain("MatchRuleEvaluationService", source);
            Assert.DoesNotContain("MatchFormationService", source);
            Assert.DoesNotContain("Alpha Auto", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
        }

        var validation = File.ReadAllText(Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerQaReviewValidation.cs"));
        Assert.Contains("campaignReady", validation, StringComparison.Ordinal);
        Assert.Contains("ForbiddenBriefKeySet", validation, StringComparison.Ordinal);

        var orchestration = File.ReadAllText(Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerQaReviewOrchestrationService.cs"));
        Assert.Contains("exactly two AI profiles", orchestration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never a third Steward agent run", orchestration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AI never receives image bytes", orchestration, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HUMAN_ESCALATION_STEWARD_V1", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerateImage", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("CampaignReady", orchestration, StringComparison.Ordinal);

        var access = File.ReadAllText(Path.Combine(root, "Bliss.Api", "Security", "WeddingPlannerAccess.cs"));
        Assert.Contains("Do not widen for QA decisions", access, StringComparison.Ordinal);
        Assert.Contains("CanDecideQaReview", access, StringComparison.Ordinal);
        Assert.Contains("CanWaiveQaEscalation", access, StringComparison.Ordinal);

        var statuses = File.ReadAllText(Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerStatuses.cs"));
        Assert.Contains("control review only", statuses, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CHAPERONE_REVIEW_V1", statuses, StringComparison.Ordinal);
        Assert.Contains("QA_INSPECTION_V1", statuses, StringComparison.Ordinal);
        Assert.DoesNotContain("HUMAN_ESCALATION_STEWARD_V1", statuses, StringComparison.Ordinal);
    }
}
