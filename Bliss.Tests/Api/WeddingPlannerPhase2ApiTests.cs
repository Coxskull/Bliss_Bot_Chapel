using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Contracts;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Bliss.Infrastructure.WeddingPlanner;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase2ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase2ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Concierge_turn_appends_human_then_planner_with_usage_metadata()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"p2-{Guid.NewGuid():N}";
        var (_, sessionId, workspaceId) = await OpenSessionAsync(client, key);

        var turnResponse = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{sessionId}/turns",
            new WeddingPlannerTurnRequest("audience: local families. offer: whitening.", "DashboardFixture", $"turn-{key}"));
        var turn = await turnResponse.Content.ReadFromJsonAsync<WeddingPlannerTurnDto>();

        Assert.Equal(HttpStatusCode.Created, turnResponse.StatusCode);
        Assert.False(turn!.IsReplay);
        Assert.Equal(1, turn.HumanMessage.SequenceNumber);
        Assert.Equal("OPERATOR", turn.HumanMessage.ActorType);
        Assert.NotNull(turn.PlannerMessage);
        Assert.Equal(2, turn.PlannerMessage!.SequenceNumber);
        Assert.Equal("PLANNER", turn.PlannerMessage.ActorType);
        Assert.Equal("CONCIERGE", turn.AgentRun.LogicalRole);
        Assert.Equal("SUCCEEDED", turn.AgentRun.Status);
        Assert.Equal(LocalDeterministicWeddingPlannerAiProvider.ProviderKey, turn.AgentRun.ProviderKey);
        Assert.Equal(LocalDeterministicWeddingPlannerAiProvider.ModelId, turn.AgentRun.ModelId);
        Assert.Equal(WeddingPlannerPromptPacks.ConciergeV1, turn.AgentRun.PromptPackVersion);
        Assert.Equal(WeddingPlannerWorkers.LocalDeterministicV1, turn.AgentRun.WorkerKey);
        Assert.True(turn.AgentRun.PromptTokens > 0);
        Assert.True(turn.AgentRun.CompletionTokens > 0);
        Assert.True(turn.AgentRun.EstimatedCostUsd >= 0);
        Assert.False(string.IsNullOrWhiteSpace(turn.AgentRun.ProviderRequestId));

        var messages = await client.GetFromJsonAsync<List<WeddingPlannerMessageDto>>(
            $"/api/wedding-planner/sessions/{sessionId}/messages");
        Assert.Equal(2, messages!.Count);
        Assert.Equal(new[] { 1, 2 }, messages.Select(x => x.SequenceNumber).ToArray());

        var runs = await client.GetFromJsonAsync<List<WeddingPlannerAgentRunDto>>(
            $"/api/wedding-planner/sessions/{sessionId}/agent-runs");
        Assert.Single(runs!);
        var run = await client.GetFromJsonAsync<WeddingPlannerAgentRunDto>(
            $"/api/wedding-planner/agent-runs/{turn.AgentRunId}");
        Assert.Equal(turn.AgentRunId, run!.AgentRunId);

        var audit = await client.GetFromJsonAsync<List<WeddingPlannerAuditEventDto>>(
            $"/api/wedding-planner/workspaces/{workspaceId}/audit");
        Assert.Contains(audit!, x => x.Action == "AGENT_RUN_STARTED");
        Assert.Contains(audit!, x => x.Action == "AGENT_RUN_SUCCEEDED");
    }

    [Fact]
    public async Task Turn_replay_does_not_duplicate_messages_or_billing()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"replay-{Guid.NewGuid():N}";
        var (_, sessionId, _) = await OpenSessionAsync(client, key);
        var request = new WeddingPlannerTurnRequest("Please help plan our brand.", "DashboardFixture", $"turn-{key}");

        var first = await (await client.PostAsJsonAsync($"/api/wedding-planner/sessions/{sessionId}/turns", request))
            .Content.ReadFromJsonAsync<WeddingPlannerTurnDto>();
        var secondResponse = await client.PostAsJsonAsync($"/api/wedding-planner/sessions/{sessionId}/turns", request);
        var second = await secondResponse.Content.ReadFromJsonAsync<WeddingPlannerTurnDto>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.True(second!.IsReplay);
        Assert.Equal(first!.AgentRunId, second.AgentRunId);
        Assert.Equal(first.HumanMessage.MessageId, second.HumanMessage.MessageId);
        Assert.Equal(first.PlannerMessage!.MessageId, second.PlannerMessage!.MessageId);

        var messages = await client.GetFromJsonAsync<List<WeddingPlannerMessageDto>>(
            $"/api/wedding-planner/sessions/{sessionId}/messages");
        Assert.Equal(2, messages!.Count);
        Assert.Equal(1, await CountRunsAsync(sessionId));
    }

    [Fact]
    public async Task Clients_still_cannot_forge_planner_messages()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"forge-{Guid.NewGuid():N}";
        var (_, sessionId, _) = await OpenSessionAsync(client, key);

        var ai = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{sessionId}/messages",
            new AppendWeddingPlannerMessageRequest("PLANNER", "Forged", "DashboardFixture", $"ai-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, ai.StatusCode);
        Assert.Contains("out of scope", await ai.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Brand_dna_interpret_approve_supersede_and_reject_are_deterministic()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"dna-{Guid.NewGuid():N}";
        var (_, sessionId, workspaceId) = await OpenSessionAsync(client, key);

        await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{sessionId}/turns",
            new WeddingPlannerTurnRequest("voice: calm. audience: couples. market: Manila.", "DashboardFixture", $"t1-{key}"));

        var interpret = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("DashboardFixture", $"interp-1-{key}"));
        var v1 = await interpret.Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        Assert.Equal(HttpStatusCode.Created, interpret.StatusCode);
        Assert.Equal(1, v1!.VersionNumber);
        Assert.Equal("PROPOSED", v1.Status);
        Assert.Equal("brand-dna.v1", v1.SchemaVersion);
        using (var doc = JsonDocument.Parse(v1.DocumentJson))
        {
            Assert.Equal("brand-dna.v1", doc.RootElement.GetProperty("schemaVersion").GetString());
            Assert.True(doc.RootElement.TryGetProperty("brandVoice", out _));
            Assert.True(doc.RootElement.TryGetProperty("audience", out _));
            Assert.True(doc.RootElement.TryGetProperty("openQuestions", out _));
        }

        var replayInterpret = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("DashboardFixture", $"interp-1-{key}"));
        var replayed = await replayInterpret.Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        Assert.Equal(HttpStatusCode.OK, replayInterpret.StatusCode);
        Assert.True(replayed!.IsReplay);
        Assert.Equal(v1.BrandDnaVersionId, replayed.BrandDnaVersionId);

        var missingRationale = await client.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{v1.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", string.Empty, "DashboardFixture", $"missing-rationale-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, missingRationale.StatusCode);

        var approve = await client.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{v1.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "Looks solid", "DashboardFixture", $"approve-1-{key}"));
        var approved = await approve.Content.ReadFromJsonAsync<WeddingPlannerBrandDnaDecisionDto>();
        Assert.Equal(HttpStatusCode.Created, approve.StatusCode);
        Assert.Equal("APPROVED", approved!.Version.Status);
        Assert.True(approved.Version.IsCurrentApproved);

        var interpret2 = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("DashboardFixture", $"interp-2-{key}"));
        var v2 = await interpret2.Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        Assert.Equal(2, v2!.VersionNumber);

        var approve2 = await client.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{v2.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "Newer brief", "DashboardFixture", $"approve-2-{key}"));
        var approved2 = await approve2.Content.ReadFromJsonAsync<WeddingPlannerBrandDnaDecisionDto>();
        Assert.Equal("APPROVED", approved2!.Version.Status);
        Assert.True(approved2.Version.IsCurrentApproved);

        var list = await client.GetFromJsonAsync<WeddingPlannerBrandDnaListDto>(
            $"/api/wedding-planner/workspaces/{workspaceId}/brand-dna");
        Assert.Equal(v2.BrandDnaVersionId, list!.CurrentApprovedBrandDnaVersionId);
        Assert.Equal("SUPERSEDED", list.Versions.Single(x => x.BrandDnaVersionId == v1.BrandDnaVersionId).Status);
        Assert.Equal(v1.DocumentJson, list.Versions.Single(x => x.BrandDnaVersionId == v1.BrandDnaVersionId).DocumentJson);

        var interpret3 = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("DashboardFixture", $"interp-3-{key}"));
        var v3 = await interpret3.Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();
        var reject = await client.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{v3!.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("REJECT", "Too vague", "DashboardFixture", $"reject-3-{key}"));
        var rejected = await reject.Content.ReadFromJsonAsync<WeddingPlannerBrandDnaDecisionDto>();
        Assert.Equal("REJECTED", rejected!.Version.Status);
        Assert.False(rejected.Version.IsCurrentApproved);

        var illegal = await client.PostAsJsonAsync(
            $"/api/wedding-planner/brand-dna/{v3.BrandDnaVersionId}/decisions",
            new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "Too late", "DashboardFixture", $"illegal-{key}"));
        Assert.Equal(HttpStatusCode.BadRequest, illegal.StatusCode);

        var loaded = await client.GetFromJsonAsync<WeddingPlannerBrandDnaVersionDto>(
            $"/api/wedding-planner/brand-dna/{v1.BrandDnaVersionId}");
        Assert.Equal("SUPERSEDED", loaded!.Status);
        Assert.Equal(v1.Summary, loaded.Summary);
    }

    [Fact]
    public async Task Provider_failure_persists_failed_run_and_returns_502()
    {
        await SeedAsync();
        await using var failingFactory = new WeddingPlannerFailingProviderFactory();
        using var scope = failingFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();

        var client = failingFactory.CreateClient();
        var key = $"fail-{Guid.NewGuid():N}";
        var (_, sessionId, _) = await OpenSessionAsync(client, key);

        var response = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{sessionId}/turns",
            new WeddingPlannerTurnRequest("This will fail.", "DashboardFixture", $"turn-{key}"));
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("provider failed", body, StringComparison.OrdinalIgnoreCase);

        using var verifyScope = failingFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var run = await verifyDb.WeddingPlannerAgentRuns.SingleAsync();
        Assert.Equal("FAILED", run.Status);
        Assert.Null(run.OutputMessageId);
        Assert.Equal(1, await verifyDb.WeddingPlannerConversationMessages.CountAsync());
        Assert.Equal("OPERATOR", (await verifyDb.WeddingPlannerConversationMessages.SingleAsync()).ActorType);
    }

    private async Task<int> CountRunsAsync(Guid sessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        return await db.WeddingPlannerAgentRuns.CountAsync(x => x.SessionId == sessionId);
    }

    private async Task<(Guid AdvertiserId, Guid SessionId, Guid WorkspaceId)> OpenSessionAsync(HttpClient client, string key)
    {
        var workspace = await (await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(Phase1DataSeeder.AdvertiserId, "DashboardFixture", $"ws-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var session = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("DashboardFixture", $"session-{key}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        return (workspace.AdvertiserId, session!.SessionId, workspace.WorkspaceId);
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }
}

public sealed class WeddingPlannerPhase2IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase2IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Cross_tenant_cannot_access_turns_runs_or_brand_dna()
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
        var turn = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{dentalSession!.SessionId}/turns",
            new WeddingPlannerTurnRequest("Dental brand note", "SECURITY_TEST", $"turn-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerTurnDto>();
        var dna = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/brand-dna/interpret",
            new InterpretWeddingPlannerBrandDnaRequest("SECURITY_TEST", $"dna-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerBrandDnaVersionDto>();

        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.PostAsJsonAsync(
                $"/api/wedding-planner/sessions/{dentalSession.SessionId}/turns",
                new WeddingPlannerTurnRequest("Nope", "SECURITY_TEST", $"bad-turn-{Guid.NewGuid():N}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.GetAsync($"/api/wedding-planner/agent-runs/{turn!.AgentRunId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.GetAsync($"/api/wedding-planner/sessions/{dentalSession.SessionId}/agent-runs")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.GetAsync($"/api/wedding-planner/brand-dna/{dna!.BrandDnaVersionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.PostAsJsonAsync(
                $"/api/wedding-planner/workspaces/{dentalWorkspace.WorkspaceId}/brand-dna/interpret",
                new InterpretWeddingPlannerBrandDnaRequest("SECURITY_TEST", $"bad-dna-{Guid.NewGuid():N}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await restaurant.PostAsJsonAsync(
                $"/api/wedding-planner/brand-dna/{dna.BrandDnaVersionId}/decisions",
                new WeddingPlannerBrandDnaDecisionRequest("APPROVE", "Unauthorized attempt", "SECURITY_TEST", $"bad-dec-{Guid.NewGuid():N}"))).StatusCode);

        var anonymous = _factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/wedding-planner/sessions/{dentalSession.SessionId}/turns",
                new WeddingPlannerTurnRequest("anon", "SECURITY_TEST", "anon"))).StatusCode);

        var viewer = _factory.CreateSecureClient();
        viewer.DefaultRequestHeaders.Add("X-Test-Name", "Read Only");
        viewer.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.viewer");
        var session = await viewer.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        viewer.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await viewer.PostAsJsonAsync(
                $"/api/wedding-planner/sessions/{dentalSession.SessionId}/turns",
                new WeddingPlannerTurnRequest("viewer", "SECURITY_TEST", "viewer-turn"))).StatusCode);
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

public sealed class WeddingPlannerFailingProviderFactory : WebApplicationFactory<Program>
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
                    || descriptor.ServiceType == typeof(IWeddingPlannerAiProvider))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(_dbName));
            services.AddScoped<IWeddingPlannerAiProvider, FailingWeddingPlannerAiProvider>();
        });
    }
}

internal sealed class FailingWeddingPlannerAiProvider : IWeddingPlannerAiProvider
{
    public string WorkerKey => WeddingPlannerWorkers.LocalDeterministicV1;

    public Task<WeddingPlannerAiCompletionResult> CompleteAsync(
        WeddingPlannerAiCompletionRequest request,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("simulated provider outage");
}

public sealed class WeddingPlannerPhase2BoundaryTests
{
    [Fact]
    public void Phase2_source_does_not_reference_bliss_matching_alpha_auto_or_n8n()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerOrchestrationService.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "LocalDeterministicWeddingPlannerAiProvider.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "OpenAiCompatibleWeddingPlannerAiProvider.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "WeddingPlannerAiOptions.cs"),
            Path.Combine(root, "Bliss.Api", "Controllers", "WeddingPlannerController.cs"),
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "IWeddingPlannerAiProvider.cs")
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("DeterministicRuleEvaluator", source);
            Assert.DoesNotContain("MatchRuleEvaluationService", source);
            Assert.DoesNotContain("Alpha Auto", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Phase2_core_orchestration_has_no_vendor_sdk_or_http_coupling()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
        var files = new[]
        {
            Path.Combine(root, "Bliss.Infrastructure", "Persistence", "WeddingPlannerOrchestrationService.cs"),
            Path.Combine(root, "Bliss.Infrastructure", "WeddingPlanner", "LocalDeterministicWeddingPlannerAiProvider.cs"),
            Path.Combine(root, "Bliss.Api", "Controllers", "WeddingPlannerController.cs"),
            Path.Combine(root, "Bliss.Domain", "WeddingPlanner", "IWeddingPlannerAiProvider.cs")
        };

        foreach (var path in files)
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("OpenAI", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ChatGPT", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("HttpClient", source);
        }
    }
}
