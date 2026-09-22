using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Contracts;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase3ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase3ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Color_profile_compute_approve_supersede_reject_and_no_agent_run()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"p3-{Guid.NewGuid():N}";
        var workspaceId = await OpenWorkspaceWithApprovedBrandDnaAsync(client, key);

        var invalidHex = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "not-a-color", null, null, null, null, null, "DashboardFixture", $"bad-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, invalidHex.StatusCode);

        var compute = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#e11", null, null, null, null, "warm notes", "DashboardFixture", $"cp-1-{key}"));
        var v1 = await compute.Content.ReadFromJsonAsync<WeddingPlannerColorProfileVersionDto>();
        Assert.Equal(HttpStatusCode.Created, compute.StatusCode);
        Assert.Equal(1, v1!.VersionNumber);
        Assert.Equal("PROPOSED", v1.Status);
        Assert.Equal("color-profile.v1", v1.SchemaVersion);
        Assert.Equal("aci.hsl.v1", v1.AlgorithmVersion);
        Assert.Equal("#EE1111", JsonDocument.Parse(v1.DocumentJson).RootElement.GetProperty("seeds").GetProperty("primaryHex").GetString());
        Assert.Contains("\"notes\":\"warm notes\"", v1.DocumentJson);

        var replay = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#e11", null, null, null, null, "ignored", "DashboardFixture", $"cp-1-{key}"));
        var replayed = await replay.Content.ReadFromJsonAsync<WeddingPlannerColorProfileVersionDto>();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replayed!.IsReplay);
        Assert.Equal(v1.ColorProfileVersionId, replayed.ColorProfileVersionId);

        var missingRationale = await client.PostAsJsonAsync(
            $"/api/wedding-planner/color-profiles/{v1.ColorProfileVersionId}/decisions",
            new WeddingPlannerColorProfileDecisionRequest("APPROVE", "", "DashboardFixture", $"miss-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, missingRationale.StatusCode);

        var approve = await client.PostAsJsonAsync(
            $"/api/wedding-planner/color-profiles/{v1.ColorProfileVersionId}/decisions",
            new WeddingPlannerColorProfileDecisionRequest("APPROVE", "Looks good", "DashboardFixture", $"ap1-{key}"));
        var approved = await approve.Content.ReadFromJsonAsync<WeddingPlannerColorProfileDecisionDto>();
        Assert.Equal(HttpStatusCode.Created, approve.StatusCode);
        Assert.Equal("APPROVED", approved!.Version.Status);
        Assert.True(approved.Version.IsCurrentApproved);

        var compute2 = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#336699", "#ff00aa", null, "#FFFFFF", null, null, "DashboardFixture", $"cp-2-{key}"));
        var v2 = await compute2.Content.ReadFromJsonAsync<WeddingPlannerColorProfileVersionDto>();
        Assert.Equal(2, v2!.VersionNumber);

        var approve2 = await client.PostAsJsonAsync(
            $"/api/wedding-planner/color-profiles/{v2.ColorProfileVersionId}/decisions",
            new WeddingPlannerColorProfileDecisionRequest("APPROVE", "Newer", "DashboardFixture", $"ap2-{key}"));
        Assert.Equal(HttpStatusCode.Created, approve2.StatusCode);

        var list = await client.GetFromJsonAsync<WeddingPlannerColorProfileListDto>(
            $"/api/wedding-planner/workspaces/{workspaceId}/color-profiles");
        Assert.Equal(v2.ColorProfileVersionId, list!.CurrentApprovedColorProfileVersionId);
        Assert.Equal("SUPERSEDED", list.Versions.Single(x => x.ColorProfileVersionId == v1.ColorProfileVersionId).Status);
        Assert.Equal(v1.DocumentJson, list.Versions.Single(x => x.ColorProfileVersionId == v1.ColorProfileVersionId).DocumentJson);

        var compute3 = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#010101", null, null, null, null, null, "DashboardFixture", $"cp-3-{key}"));
        var v3 = await compute3.Content.ReadFromJsonAsync<WeddingPlannerColorProfileVersionDto>();
        var reject = await client.PostAsJsonAsync(
            $"/api/wedding-planner/color-profiles/{v3!.ColorProfileVersionId}/decisions",
            new WeddingPlannerColorProfileDecisionRequest("REJECT", "Too dark", "DashboardFixture", $"rej-{key}"));
        var rejected = await reject.Content.ReadFromJsonAsync<WeddingPlannerColorProfileDecisionDto>();
        Assert.Equal("REJECTED", rejected!.Version.Status);
        Assert.False(rejected.Version.IsCurrentApproved);

        var illegal = await client.PostAsJsonAsync(
            $"/api/wedding-planner/color-profiles/{v3.ColorProfileVersionId}/decisions",
            new WeddingPlannerColorProfileDecisionRequest("APPROVE", "late", "DashboardFixture", $"late-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, illegal.StatusCode);

        var loaded = await client.GetFromJsonAsync<WeddingPlannerColorProfileVersionDto>(
            $"/api/wedding-planner/color-profiles/{v1.ColorProfileVersionId}");
        Assert.Equal("SUPERSEDED", loaded!.Status);
        Assert.Equal(v1.InputSha256, loaded.InputSha256);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(0, await db.WeddingPlannerAgentRuns.CountAsync(x =>
            x.IdempotencyKey == $"cp-1-{key}"
            || x.IdempotencyKey == $"cp-2-{key}"
            || x.IdempotencyKey == $"cp-3-{key}"));
    }

    [Fact]
    public async Task Compute_without_approved_brand_dna_returns_400()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"nodna-{Guid.NewGuid():N}";
        var workspace = await (await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(Phase1DataSeeder.AdvertiserId, "DashboardFixture", $"ws-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();

        var response = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace!.WorkspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#336699", null, null, null, null, null, "DashboardFixture", $"cp-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Brand DNA", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid> OpenWorkspaceWithApprovedBrandDnaAsync(HttpClient client, string key)
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
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "Approved for color", "DashboardFixture", $"dna-ap-{key}"));
        return workspace.WorkspaceId;
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }
}

public sealed class WeddingPlannerPhase3IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase3IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Cross_tenant_anonymous_and_viewer_cannot_access_color_profiles()
    {
        await SeedAsync();
        var dental = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.DentalManilaId, "Dental Manila");
        var restaurant = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "Restaurant Santo Domingo");

        var dentalWorkspace = await (await dental.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.DentalManilaId, "SECURITY_TEST", $"d-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var dentalSession = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWorkspace!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("SECURITY_TEST", $"ds-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        await dental.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{dentalSession!.SessionId}/turns",
            new WeddingPlannerTurnRequest("Dental brand note", "SECURITY_TEST", $"turn-{Guid.NewGuid():N}"));
        var dna = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("SECURITY_TEST", $"dna-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        await dental.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{dna!.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "ok", "SECURITY_TEST", $"dna-ap-{Guid.NewGuid():N}"));

        var profile = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/color-profiles/compute",
            new ComputeWeddingPlannerColorProfileRequest(
                "#336699", null, null, null, null, null, "SECURITY_TEST", $"cp-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerColorProfileVersionDto>();

        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.GetAsync($"/api/wedding-planner/color-profiles/{profile!.ColorProfileVersionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.GetAsync($"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/color-profiles")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.PostAsJsonAsync(
                $"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/color-profiles/compute",
                new ComputeWeddingPlannerColorProfileRequest(
                    "#112233", null, null, null, null, null, "SECURITY_TEST", $"bad-cp-{Guid.NewGuid():N}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.PostAsJsonAsync(
                $"/api/wedding-planner/color-profiles/{profile.ColorProfileVersionId}/decisions",
                new WeddingPlannerColorProfileDecisionRequest("APPROVE", "no", "SECURITY_TEST", $"bad-dec-{Guid.NewGuid():N}"))).StatusCode);

        var anonymous = _factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/color-profiles/compute",
                new ComputeWeddingPlannerColorProfileRequest(
                    "#112233", null, null, null, null, null, "SECURITY_TEST", "anon"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/wedding-planner/color-profiles/{profile.ColorProfileVersionId}")).StatusCode);

        var viewer = _factory.CreateSecureClient();
        viewer.DefaultRequestHeaders.Add("X-Test-Name", "Read Only");
        viewer.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.viewer");
        var session = await viewer.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        viewer.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.PostAsJsonAsync(
                $"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/color-profiles/compute",
                new ComputeWeddingPlannerColorProfileRequest(
                    "#112233", null, null, null, null, null, "SECURITY_TEST", "viewer-cp"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.PostAsJsonAsync(
                $"/api/wedding-planner/color-profiles/{profile.ColorProfileVersionId}/decisions",
                new WeddingPlannerColorProfileDecisionRequest("APPROVE", "no", "SECURITY_TEST", "viewer-dec"))).StatusCode);
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

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }
}

public sealed class WeddingPlannerPhase3BoundaryTests
{
    [Fact]
    public void Phase3_color_intelligence_does_not_couple_to_ai_provider_agent_runs_or_bliss_matching()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "AciHslV1.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerColorIntelligenceService.cs"),
            Path.Combine(root, "Bliss.Domain", "Entities", "WeddingPlannerColorProfileVersion.cs"),
            Path.Combine(root, "Bliss.Domain", "Entities", "WeddingPlannerColorProfileDecision.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Configurations", "WeddingPlannerColorProfileVersionConfiguration.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "Configurations", "WeddingPlannerColorProfileDecisionConfiguration.cs")
        };

        foreach (var path in files)
        {
            Assert.True(File.Exists(path), $"Missing {path}");
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("IWeddingPlannerAiProvider", source);
            Assert.DoesNotContain("WeddingPlannerAgentRun", source);
            Assert.DoesNotContain("DeterministicRuleEvaluator", source);
            Assert.DoesNotContain("MatchRuleEvaluationService", source);
            Assert.DoesNotContain("Alpha Auto", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CompleteAsync", source);
        }

        var controller = File.ReadAllText(Path.Combine(root, "Bliss.Api", "Controllers", "WeddingPlannerController.cs"));
        Assert.Contains("WeddingPlannerColorIntelligenceService", controller);
        Assert.Contains("color-profiles/compute", controller);
    }
}
