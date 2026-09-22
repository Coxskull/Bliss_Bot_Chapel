using System.Net;
using System.Net.Http.Json;
using System.Text;
using Bliss.Api.Contracts;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase6ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase6ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Creative_production_happy_path_six_runs_thirteen_contributions_and_asset_stream()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"p6-{Guid.NewGuid():N}"[..18];
        var workspaceId = await OpenWorkspaceWithApprovedConceptAsync(client, key);

        var create = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL",
                "Draft calm square and story creatives",
                [WeddingPlannerChannelFormats.StaticSocialSquare, WeddingPlannerChannelFormats.StaticSocialStory],
                2,
                null,
                null,
                null,
                null,
                "DashboardFixture",
                $"job-{key}"));
        var job = await create.Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("SUCCEEDED", job!.Status);
        Assert.NotNull(job.OutputCreativePackageVersionId);
        Assert.Equal(2, job.RequestedVariantCount);

        var replay = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "ignored", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "DashboardFixture", $"job-{key}"));
        var replayed = await replay.Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replayed!.IsReplay);
        Assert.Equal(job.CreativeProductionJobId, replayed.CreativeProductionJobId);

        var packageRuns = await client.GetFromJsonAsync<List<WeddingPlannerAgentRunDto>>(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}/agent-runs");
        Assert.Equal(6, packageRuns!.Count);
        Assert.All(packageRuns, r => Assert.Equal("SUCCEEDED", r.Status));
        Assert.Equal(1, packageRuns.Count(r => r.OutputCreativePackageVersionId is not null));
        Assert.DoesNotContain(packageRuns, r =>
            r.WorkerProfileVersion is "CONCEPT_STRATEGY_V1" or "CONCEPT_CREATIVE_V1" or "PROTOTYPE_PRODUCTION_V1");

        var contributions = await client.GetFromJsonAsync<List<WeddingPlannerCreativeRoleContributionDto>>(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}/contributions");
        Assert.Equal(13, contributions!.Count);
        Assert.Equal(
            WeddingPlannerCreativeDepartmentLogicalRoles.AllInOrder.ToArray(),
            contributions.Select(c => c.LogicalRole).ToArray());

        var assets = await client.GetFromJsonAsync<List<WeddingPlannerCreativeAssetDto>>(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}/assets");
        Assert.Equal(2, assets!.Count);

        var package = await client.GetFromJsonAsync<WeddingPlannerCreativePackageVersionDto>(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}");
        Assert.Contains(WeddingPlannerCreativePackageDisclaimer.Text, package!.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage, package.DocumentJson, StringComparison.Ordinal);

        var content = await client.GetAsync($"/api/wedding-planner/creative-assets/{assets[0].CreativeAssetId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/png", content.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", content.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("private", content.Headers.CacheControl!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", content.Headers.CacheControl!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal($"\"{assets[0].Sha256}\"", content.Headers.ETag!.Tag);
        Assert.Contains("inline", content.Content.Headers.ContentDisposition!.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(content.Headers.Location is not null);
        var bytes = await content.Content.ReadAsByteArrayAsync();
        Assert.Equal(assets[0].ByteSize, bytes.Length);
        WeddingPlannerPngValidator.ValidateExactCanvas(bytes, assets[0].Width, assets[0].Height);

        var approve = await client.PostAsJsonAsync(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}/decisions",
            new WeddingPlannerCreativePackageDecisionRequest(
                "APPROVE", "Variant 1 best fits", "variant_1", "DashboardFixture", $"ap-{key}"));
        var decision = await approve.Content.ReadFromJsonAsync<WeddingPlannerCreativePackageDecisionDto>();
        Assert.Equal(HttpStatusCode.Created, approve.StatusCode);
        Assert.Equal("variant_1", decision!.SelectedVariantId);
        Assert.True(decision.Version.IsCurrentApproved);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var jobEntity = await db.WeddingPlannerCreativeProductionJobs.SingleAsync(x => x.Id == job.CreativeProductionJobId);
        var runIds = new[]
        {
            jobEntity.CreativeDirectionAgentRunId,
            jobEntity.StrategyAdaptationAgentRunId,
            jobEntity.VisualSystemAgentRunId,
            jobEntity.ImageDirectionAgentRunId,
            jobEntity.CopySystemAgentRunId,
            jobEntity.VariantProductionAgentRunId
        };
        Assert.All(runIds, id => Assert.NotNull(id));
        Assert.Equal(6, await db.WeddingPlannerAgentRuns.CountAsync(x => runIds.Contains(x.Id)));
        Assert.Equal(13, await db.WeddingPlannerCreativeRoleContributions.CountAsync(x =>
            x.CreativePackageVersionId == job.OutputCreativePackageVersionId));
        Assert.Equal(2, await db.WeddingPlannerCreativeAssets.CountAsync(x =>
            x.CreativePackageVersionId == job.OutputCreativePackageVersionId));
    }

    [Fact]
    public async Task Requires_approved_concept_and_rejects_forbidden_fields_and_bad_decisions()
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
                Name = $"NoConcept {key}",
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
            $"/api/wedding-planner/workspaces/{workspaceId}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "DashboardFixture", $"job-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);

        var ready = await OpenWorkspaceWithApprovedConceptAsync(client, $"rdy-{key}");
        var forbiddenBody = $$"""
            {"jobKind":"INITIAL","objective":"O","formats":["STATIC_SOCIAL_SQUARE"],"requestedVariantCount":1,"campaignId":"camp-1","sourceSystem":"DashboardFixture","idempotencyKey":"forbid-{{key}}"}
            """;
        var forbidden = await client.PostAsync(
            $"/api/wedding-planner/workspaces/{ready}/creative-production-jobs",
            new StringContent(forbiddenBody, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, forbidden.StatusCode);

        var job = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{ready}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "DashboardFixture", $"ok-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();

        var rejectWithSelection = await client.PostAsJsonAsync(
            $"/api/wedding-planner/creative-packages/{job!.OutputCreativePackageVersionId}/decisions",
            new WeddingPlannerCreativePackageDecisionRequest(
                "REJECT", "nope", "variant_1", "DashboardFixture", $"rj-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, rejectWithSelection.StatusCode);

        var approveBad = await client.PostAsJsonAsync(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}/decisions",
            new WeddingPlannerCreativePackageDecisionRequest(
                "APPROVE", "ok", "variant_9", "DashboardFixture", $"ap-bad-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, approveBad.StatusCode);
    }

    [Fact]
    public async Task Revision_job_succeeds_with_parent_and_same_pins()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"rev-{Guid.NewGuid():N}"[..16];
        var workspaceId = await OpenWorkspaceWithApprovedConceptAsync(client, key);
        var initial = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "DashboardFixture", $"init-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();

        var revision = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "REVISION",
                "Revise CTA",
                [WeddingPlannerChannelFormats.StaticSocialSquare],
                1,
                initial!.OutputCreativePackageVersionId,
                "Sharper CTA",
                null,
                null,
                "DashboardFixture",
                $"rev-{key}"));
        var revJob = await revision.Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();
        Assert.Equal(HttpStatusCode.Created, revision.StatusCode);
        Assert.Equal("SUCCEEDED", revJob!.Status);
        Assert.Equal(initial.OutputCreativePackageVersionId, revJob.RevisionParentCreativePackageVersionId);
        Assert.NotEqual(initial.OutputCreativePackageVersionId, revJob.OutputCreativePackageVersionId);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }

    private static async Task<Guid> OpenWorkspaceWithApprovedConceptAsync(HttpClient client, string key)
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

        return workspace.WorkspaceId;
    }
}

public sealed class WeddingPlannerPhase6IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase6IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Oidc_isolation_anonymous_viewer_and_cross_tenant_asset_content()
    {
        await SeedAsync();
        var dental = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.DentalManilaId, "Dental Manila");
        var restaurant = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "Restaurant Santo Domingo");

        var dentalWs = await OpenReadyWorkspaceAsync(dental, "d6");
        var jobKey = $"cj-{Guid.NewGuid():N}";
        jobKey = jobKey[..Math.Min(jobKey.Length, WeddingPlannerCreativeDepartmentIdempotency.MaxJobIdempotencyKeyLength)];
        var job = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "SECURITY_TEST", jobKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerCreativeProductionJobDto>();

        var assets = await dental.GetFromJsonAsync<List<WeddingPlannerCreativeAssetDto>>(
            $"/api/wedding-planner/creative-packages/{job!.OutputCreativePackageVersionId}/assets");
        var assetId = assets![0].CreativeAssetId;

        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/creative-production-jobs/{job.CreativeProductionJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/creative-assets/{assetId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/creative-assets/{assetId}/content")).StatusCode);

        var anonymous = _factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            $"/api/wedding-planner/creative-production-jobs/{job.CreativeProductionJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            $"/api/wedding-planner/creative-assets/{assetId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "SECURITY_TEST", "anon"))).StatusCode);

        var viewer = await CreateViewerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs}/creative-production-jobs",
            new CreateWeddingPlannerCreativeProductionJobRequest(
                "INITIAL", "O", [WeddingPlannerChannelFormats.StaticSocialSquare], 1,
                null, null, null, null, "SECURITY_TEST", $"vj-{Guid.NewGuid():N}"[..20]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/wedding-planner/creative-packages/{job.OutputCreativePackageVersionId}/decisions",
            new WeddingPlannerCreativePackageDecisionRequest(
                "APPROVE", "no", "variant_1", "SECURITY_TEST", "viewer-dec"))).StatusCode);
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

public sealed class WeddingPlannerPhase6BoundaryTests
{
    [Fact]
    public void Phase6_creative_department_does_not_couple_to_matching_qa_or_phase5_reruns()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerCreativeDepartmentValidation.cs"),
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerPngValidator.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerCreativeDepartmentOrchestrationService.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "LocalDeterministicWeddingPlannerCreativeAssetProvider.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "RemoteHttpWeddingPlannerCreativeAssetProvider.cs")
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("DeterministicRuleEvaluator", source);
            Assert.DoesNotContain("MatchRuleEvaluationService", source);
            Assert.DoesNotContain("MatchFormationService", source);
            Assert.DoesNotContain("Alpha Auto", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Chaperone", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CONCEPT_STRATEGY_V1", source);
            Assert.DoesNotContain("PROTOTYPE_PRODUCTION_V1", source);
        }

        var validation = File.ReadAllText(Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerCreativeDepartmentValidation.cs"));
        Assert.Contains("Never generates image bytes", validation, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Net.Http", validation, StringComparison.Ordinal);

        var statuses = File.ReadAllText(Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerStatuses.cs"));
        Assert.Contains("draft creative approval only", statuses, StringComparison.OrdinalIgnoreCase);

        var orchestration = File.ReadAllText(Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerCreativeDepartmentOrchestrationService.cs"));
        Assert.Contains("Phase 5 concept profiles are never invoked", orchestration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IWeddingPlannerCreativeAssetProvider", orchestration, StringComparison.Ordinal);
        Assert.DoesNotContain("GenerateImage", orchestration, StringComparison.Ordinal);
        Assert.Contains("Brand DNA and Color Profile are creative constraints only", orchestration, StringComparison.Ordinal);

        var localAsset = File.ReadAllText(Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "LocalDeterministicWeddingPlannerCreativeAssetProvider.cs"));
        Assert.Contains("ZLibStream", localAsset, StringComparison.Ordinal);
        Assert.DoesNotContain("SkiaSharp", localAsset, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ImageSharp", localAsset, StringComparison.OrdinalIgnoreCase);
    }
}

