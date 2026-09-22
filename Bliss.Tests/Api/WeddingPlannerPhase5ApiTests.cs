using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bliss.Api.Contracts;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase5ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase5ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Workshop_job_happy_path_lists_exactly_three_runs_and_four_contributions()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"p5-{Guid.NewGuid():N}"[..20];
        var workspaceId = await OpenWorkspaceWithPrerequisitesAsync(client, key);

        var create = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/workshop-jobs",
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
                $"job-{key}"));
        var job = await create.Content.ReadFromJsonAsync<WeddingPlannerWorkshopJobDto>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("SUCCEEDED", job!.Status);
        Assert.NotNull(job.OutputConceptPackageVersionId);

        var replay = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "ignored", "ignored", "ignored", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["x"], "cta", null, null, null, "DashboardFixture", $"job-{key}"));
        var replayed = await replay.Content.ReadFromJsonAsync<WeddingPlannerWorkshopJobDto>();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replayed!.IsReplay);
        Assert.Equal(job.WorkshopJobId, replayed.WorkshopJobId);

        var packageRuns = await client.GetFromJsonAsync<List<WeddingPlannerAgentRunDto>>(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}/agent-runs");
        Assert.Equal(3, packageRuns!.Count);
        Assert.All(packageRuns, r => Assert.Equal("SUCCEEDED", r.Status));
        Assert.Equal(1, packageRuns.Count(r => r.OutputConceptPackageVersionId is not null));
        Assert.Contains(packageRuns, r => r.WorkerProfileVersion == "CONCEPT_STRATEGY_V1");
        Assert.Contains(packageRuns, r => r.WorkerProfileVersion == "CONCEPT_CREATIVE_V1");
        Assert.Contains(packageRuns, r => r.WorkerProfileVersion == "PROTOTYPE_PRODUCTION_V1");

        var contributions = await client.GetFromJsonAsync<List<WeddingPlannerConceptRoleContributionDto>>(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}/contributions");
        Assert.Equal(4, contributions!.Count);
        Assert.Equal(
            WeddingPlannerConceptWorkshopLogicalRoles.AllInOrder.ToArray(),
            contributions.Select(c => c.LogicalRole).ToArray());

        var package = await client.GetFromJsonAsync<WeddingPlannerConceptPackageVersionDto>(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}");
        Assert.Contains(WeddingPlannerConceptPackageDisclaimer.Text, package!.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype, package.DocumentJson, StringComparison.Ordinal);

        var approve = await client.PostAsJsonAsync(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}/decisions",
            new WeddingPlannerConceptPackageDecisionRequest(
                "APPROVE", "Direction 1 fits", "concept_1", "DashboardFixture", $"ap-{key}"));
        var decision = await approve.Content.ReadFromJsonAsync<WeddingPlannerConceptPackageDecisionDto>();
        Assert.Equal(HttpStatusCode.Created, approve.StatusCode);
        Assert.Equal("concept_1", decision!.SelectedConceptId);
        Assert.True(decision.Version.IsCurrentApproved);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(3, await db.WeddingPlannerAgentRuns.CountAsync(x =>
            x.WorkspaceId == workspaceId
            && (x.WorkerProfileVersion == "CONCEPT_STRATEGY_V1"
                || x.WorkerProfileVersion == "CONCEPT_CREATIVE_V1"
                || x.WorkerProfileVersion == "PROTOTYPE_PRODUCTION_V1")));
        Assert.Equal(4, await db.WeddingPlannerConceptRoleContributions.CountAsync(x =>
            x.ConceptPackageVersionId == job.OutputConceptPackageVersionId));
    }

    [Fact]
    public async Task Workshop_job_requires_all_three_prerequisites_and_rejects_forbidden_fields()
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
                Name = $"NoPrereq {key}",
                CreatedAt = DateTime.UtcNow
            };
            db.Advertisers.Add(advertiser);
            await db.SaveChangesAsync();

            var workspace = await (await client.PostAsJsonAsync(
                "/api/wedding-planner/workspaces",
                new OpenWeddingPlannerWorkspaceRequest(advertiser.Id, "DashboardFixture", $"ws-{key}")))
                .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
            workspaceId = workspace!.WorkspaceId;
        }

        var missing = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "O", "G", "A", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["d"], "cta", null, null, null, "DashboardFixture", $"job-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        // Forbidden fields are rejected before prerequisite checks.
        var forbiddenBody = $$"""
            {"objective":"O","campaignGoal":"G","audienceFocus":"A","channelFormat":"STATIC_SOCIAL_SQUARE","deliverables":["d"],"cta":"cta","constraints":[],"campaignId":"camp-1","sourceSystem":"DashboardFixture","idempotencyKey":"forbid-{{key}}"}
            """;
        var forbidden = await client.PostAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/workshop-jobs",
            new StringContent(forbiddenBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);
    }

    [Fact]
    public async Task Reject_forbids_selection_and_approve_requires_valid_concept()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"dec-{Guid.NewGuid():N}"[..16];
        var workspaceId = await OpenWorkspaceWithPrerequisitesAsync(client, key);
        var job = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "O", "G", "A", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["d"], "cta", null, null, null, "DashboardFixture", $"job-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkshopJobDto>();

        var rejectWithSelection = await client.PostAsJsonAsync(
            $"/api/wedding-planner/concept-packages/{job!.OutputConceptPackageVersionId}/decisions",
            new WeddingPlannerConceptPackageDecisionRequest(
                "REJECT", "nope", "concept_1", "DashboardFixture", $"rj-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, rejectWithSelection.StatusCode);

        var approveBad = await client.PostAsJsonAsync(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}/decisions",
            new WeddingPlannerConceptPackageDecisionRequest(
                "APPROVE", "ok", "concept_9", "DashboardFixture", $"ap-bad-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, approveBad.StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }

    private static async Task<Guid> OpenWorkspaceWithPrerequisitesAsync(HttpClient client, string key)
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

        return workspace.WorkspaceId;
    }
}

public sealed class WeddingPlannerPhase5IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase5IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Oidc_isolation_anonymous_and_viewer_enforcement()
    {
        await SeedAsync();
        var dental = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.DentalManilaId, "Dental Manila");
        var restaurant = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "Restaurant Santo Domingo");

        var dentalWs = await OpenReadyWorkspaceAsync(dental, "d");
        var jobKey = $"wj-{Guid.NewGuid():N}";
        jobKey = jobKey[..Math.Min(jobKey.Length, WeddingPlannerConceptWorkshopIdempotency.MaxJobIdempotencyKeyLength)];
        var job = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "O", "G", "A", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["d"], "cta", null, null, null, "SECURITY_TEST", jobKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkshopJobDto>();

        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/workshop-jobs/{job!.WorkshopJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}/contributions")).StatusCode);

        var anonymous = _factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            $"/api/wedding-planner/workshop-jobs/{job.WorkshopJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "O", "G", "A", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["d"], "cta", null, null, null, "SECURITY_TEST", "anon"))).StatusCode);

        var viewer = await CreateViewerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/workshop-jobs",
            new CreateWeddingPlannerWorkshopJobRequest(
                "O", "G", "A", WeddingPlannerChannelFormats.StaticSocialSquare,
                ["d"], "cta", null, null, null, "SECURITY_TEST", $"vj-{Guid.NewGuid():N}"[..20]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/wedding-planner/concept-packages/{job.OutputConceptPackageVersionId}/decisions",
            new WeddingPlannerConceptPackageDecisionRequest(
                "APPROVE", "no", "concept_1", "SECURITY_TEST", "viewer-dec"))).StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }

    private static async Task<Guid> OpenReadyWorkspaceAsync(HttpClient client, string prefix)
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

    private async Task<HttpClient> CreateViewerClientAsync()
    {
        var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", "Viewer");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.viewer");
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        Assert.NotNull(session?.CsrfToken);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
        return client;
    }
}

public sealed class WeddingPlannerPhase5BoundaryTests
{
    [Fact]
    public void Phase5_workshop_does_not_couple_to_matching_image_generation_or_url_fetch()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerConceptWorkshopValidation.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerConceptWorkshopOrchestrationService.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "LocalDeterministicWeddingPlannerAiProvider.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "OpenAiCompatibleWeddingPlannerAiProvider.cs")
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("DeterministicRuleEvaluator", source);
            Assert.DoesNotContain("MatchRuleEvaluationService", source);
            Assert.DoesNotContain("MatchFormationService", source);
            Assert.DoesNotContain("Alpha Auto", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("GenerateImage", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DallE", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("images/generations", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HttpMethod.Get", source);
            Assert.DoesNotContain("FetchUrl", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("IAssetUploadPipeline", source, StringComparison.Ordinal);
        }

        var validation = File.ReadAllText(Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerConceptWorkshopValidation.cs"));
        Assert.Contains("Never generates images", validation, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Net.Http", validation, StringComparison.Ordinal);

        var orchestration = File.ReadAllText(Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerConceptWorkshopOrchestrationService.cs"));
        Assert.Contains("Never generates images", orchestration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Brand DNA and Color Profile are creative constraints only", orchestration, StringComparison.Ordinal);
    }
}
