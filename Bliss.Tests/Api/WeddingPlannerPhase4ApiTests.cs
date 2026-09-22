using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase4ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase4ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Research_job_happy_path_lists_exactly_three_runs_and_eight_contributions()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"p4-{Guid.NewGuid():N}"[..20];
        var workspaceId = await OpenWorkspaceWithApprovedBrandDnaAsync(client, key);

        var create = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "Manila wedding demand",
                "Understand SYNTHETIC local signals",
                ["Who books?", "Which formats?"],
                "Manila",
                "en",
                Array.Empty<string>(),
                "DashboardFixture",
                $"job-{key}"));
        var job = await create.Content.ReadFromJsonAsync<WeddingPlannerResearchJobDto>();
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal("SUCCEEDED", job!.Status);
        Assert.Contains(".invalid", job.SourceCatalogJson!, StringComparison.Ordinal);
        Assert.Contains("SYNTHETIC", job.SourceCatalogJson!, StringComparison.Ordinal);
        Assert.NotNull(job.OutputResearchReportVersionId);

        var replay = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "ignored", "ignored", ["x"], "Manila", "en", null, "DashboardFixture", $"job-{key}"));
        var replayed = await replay.Content.ReadFromJsonAsync<WeddingPlannerResearchJobDto>();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replayed!.IsReplay);
        Assert.Equal(job.ResearchJobId, replayed.ResearchJobId);

        var reportRuns = await client.GetFromJsonAsync<List<WeddingPlannerAgentRunDto>>(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}/agent-runs");
        Assert.Equal(3, reportRuns!.Count);
        Assert.All(reportRuns, r => Assert.Equal("SUCCEEDED", r.Status));
        Assert.Equal(1, reportRuns.Count(r => r.OutputResearchReportVersionId is not null));
        Assert.Contains(reportRuns, r => r.WorkerProfileVersion == "CURATOR_RESEARCH_V1");
        Assert.Contains(reportRuns, r => r.WorkerProfileVersion == "CURATOR_EVIDENCE_V1");
        Assert.Contains(reportRuns, r => r.WorkerProfileVersion == "CURATOR_SYNTHESIS_RISK_V1");

        var contributions = await client.GetFromJsonAsync<List<WeddingPlannerResearchRoleContributionDto>>(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}/contributions");
        Assert.Equal(8, contributions!.Count);
        Assert.Equal(
            WeddingPlannerCuratorLogicalRoles.AllInOrder.ToArray(),
            contributions.Select(c => c.LogicalRole).ToArray());

        var report = await client.GetFromJsonAsync<WeddingPlannerResearchReportVersionDto>(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}");
        Assert.Contains(WeddingPlannerResearchDisclaimer.Text, report!.DocumentJson, StringComparison.Ordinal);

        var workspaceRuns = await client.GetFromJsonAsync<List<WeddingPlannerAgentRunDto>>(
            $"/api/wedding-planner/workspaces/{workspaceId}/agent-runs");
        Assert.True(workspaceRuns!.Count >= 3);

        var approve = await client.PostAsJsonAsync(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}/decisions",
            new WeddingPlannerResearchReportDecisionRequest("APPROVE", "Looks useful", "DashboardFixture", $"ap-{key}"));
        Assert.Equal(HttpStatusCode.Created, approve.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(3, await db.WeddingPlannerAgentRuns.CountAsync(x =>
            x.WorkspaceId == workspaceId && x.WorkerProfileVersion != null));
        Assert.Equal(8, await db.WeddingPlannerResearchRoleContributions.CountAsync(x =>
            x.ResearchReportVersionId == job.OutputResearchReportVersionId));
    }

    [Fact]
    public async Task Research_job_requires_approved_brand_dna()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = Guid.NewGuid().ToString("N")[..12];
        var workspace = await (await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(Phase1DataSeeder.AdvertiserId, "DashboardFixture", $"ws-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace!.WorkspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "T", "O", ["Q"], "G", "en", null, "DashboardFixture", $"job-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Provider_failure_returns_502_with_zero_runs()
    {
        await using var factory = new WeddingPlannerFailingResearchFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new WeddingPlannerDataSeeder(db).SeedAsync();
        }

        var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString("N")[..12];
        var workspaceId = await OpenWorkspaceWithApprovedBrandDnaAsync(client, key);
        var response = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "T", "O", ["Q"], "G", "en", null, "DashboardFixture", $"fail-{key}"));
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(0, await verifyDb.WeddingPlannerAgentRuns.CountAsync(x => x.WorkerProfileVersion != null));
        Assert.Equal(0, await verifyDb.WeddingPlannerResearchReportVersions.CountAsync());
        Assert.Equal("FAILED", (await verifyDb.WeddingPlannerResearchJobs.SingleAsync()).Status);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }

    private static async Task<Guid> OpenWorkspaceWithApprovedBrandDnaAsync(HttpClient client, string key)
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
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "Approved for curator", "DashboardFixture", $"dna-ap-{key}"));
        return workspace.WorkspaceId;
    }
}

public sealed class WeddingPlannerPhase4IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase4IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Oidc_isolation_anonymous_and_viewer_enforcement()
    {
        await SeedAsync();
        var dental = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.DentalManilaId, "Dental Manila");
        var restaurant = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "Restaurant Santo Domingo");

        var dentalWs = await (await dental.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.DentalManilaId, "SECURITY_TEST", $"d-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var session = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("SECURITY_TEST", $"ds-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        await dental.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session!.SessionId}/turns",
            new WeddingPlannerTurnRequest("Dental brand note", "SECURITY_TEST", $"turn-{Guid.NewGuid():N}"));
        var dna = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs.WorkspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("SECURITY_TEST", $"dna-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        await dental.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{dna!.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "ok", "SECURITY_TEST", $"dna-ap-{Guid.NewGuid():N}"));

        var jobKey = $"job-{Guid.NewGuid():N}";
        jobKey = jobKey[..Math.Min(jobKey.Length, WeddingPlannerCuratorIdempotency.MaxJobIdempotencyKeyLength)];
        var job = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs.WorkspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "T", "O", ["Q"], "G", "en", null, "SECURITY_TEST", jobKey)))
            .Content.ReadFromJsonAsync<WeddingPlannerResearchJobDto>();

        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/research-jobs/{job!.ResearchJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.GetAsync(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}/contributions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await restaurant.PostAsJsonAsync(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}/decisions",
            new WeddingPlannerResearchReportDecisionRequest("APPROVE", "no", "SECURITY_TEST", $"bad-{Guid.NewGuid():N}"))).StatusCode);

        var anonymous = _factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(
            $"/api/wedding-planner/research-jobs/{job.ResearchJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs.WorkspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "T", "O", ["Q"], "G", "en", null, "SECURITY_TEST", "anon"))).StatusCode);

        var viewer = await CreateViewerClientAsync();
        var viewerJobKey = $"vj-{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWs.WorkspaceId}/research-jobs",
            new CreateWeddingPlannerResearchJobRequest(
                "T", "O", ["Q"], "G", "en", null, "SECURITY_TEST", viewerJobKey[..Math.Min(20, viewerJobKey.Length)]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync(
            $"/api/wedding-planner/research-reports/{job.OutputResearchReportVersionId}/decisions",
            new WeddingPlannerResearchReportDecisionRequest("APPROVE", "no", "SECURITY_TEST", "viewer-dec"))).StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
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

public sealed class WeddingPlannerPhase4BoundaryTests
{
    [Fact]
    public void Phase4_curator_does_not_couple_to_bliss_matching_or_url_fetch()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerCuratorValidation.cs"),
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "IWeddingPlannerResearchProvider.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerCuratorOrchestrationService.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "LocalDeterministicWeddingPlannerResearchProvider.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "RemoteHttpWeddingPlannerResearchProvider.cs")
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("DeterministicRuleEvaluator", source);
            Assert.DoesNotContain("MatchRuleEvaluationService", source);
            Assert.DoesNotContain("MatchFormationService", source);
            Assert.DoesNotContain("Alpha Auto", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Dns.GetHost", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("GetHostEntry", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HttpClient.GetStringAsync", source);
            Assert.DoesNotContain("FetchCitation", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CrawlUrl", source, StringComparison.OrdinalIgnoreCase);
        }

        var validation = File.ReadAllText(Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "WeddingPlannerCuratorValidation.cs"));
        Assert.Contains("Never performs DNS resolution or network I/O", validation, StringComparison.Ordinal);
        Assert.DoesNotContain("using System.Net.Http", validation, StringComparison.Ordinal);
    }

    [Fact]
    public void Architecture_proves_no_returned_url_fetch_method_exists()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var researchFiles = Directory.GetFiles(
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner"),
            "*Research*",
            SearchOption.AllDirectories);
        var curator = File.ReadAllText(Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerCuratorOrchestrationService.cs"));
        foreach (var path in researchFiles.Append(
                     Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerCuratorOrchestrationService.cs")))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("FetchUrl", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DownloadCitation", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HttpMethod.Get", source);
        }

        Assert.DoesNotContain("HttpMethod.Get", curator);
        Assert.Contains("never fetches citation URLs", curator, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class WeddingPlannerFailingResearchFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                    || descriptor.ServiceType == typeof(BlissDbContext)
                    || descriptor.ServiceType == typeof(IWeddingPlannerResearchProvider))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(_dbName));
            services.AddScoped<IWeddingPlannerResearchProvider, FailingWeddingPlannerResearchProvider>();
        });
    }
}

internal sealed class FailingWeddingPlannerResearchProvider : IWeddingPlannerResearchProvider
{
    public string WorkerKey => WeddingPlannerResearchWorkers.LocalDeterministicV1;

    public Task<WeddingPlannerResearchAcquisitionResult> AcquireSourcesAsync(
        WeddingPlannerResearchAcquisitionRequest request,
        CancellationToken cancellationToken = default) =>
        throw new WeddingPlannerResearchProviderException("simulated research outage", "RESEARCH_PROVIDER_TRANSPORT");
}
