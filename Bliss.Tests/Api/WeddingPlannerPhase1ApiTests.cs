using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class WeddingPlannerPhase1ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public WeddingPlannerPhase1ApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Authorized_advertiser_can_open_session_and_resume_durable_messages()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var key = $"wp-{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/wedding-planner/workspaces", OpenRequest(Phase1DataSeeder.AdvertiserId, key));
        var first = await created.Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var replayed = await client.PostAsJsonAsync("/api/wedding-planner/workspaces", OpenRequest(Phase1DataSeeder.AdvertiserId, $"other-{key}"));
        var replay = await replayed.Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.False(first!.IsReplay);
        Assert.True(replay!.IsReplay);
        Assert.Equal(first.WorkspaceId, replay.WorkspaceId);
        Assert.True(first.IsPrimary);

        var sessionResponse = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{first.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("DashboardFixture", $"session-{key}"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);
        Assert.False(session!.IsReplay);

        var replaySession = await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{first.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("DashboardFixture", $"session-{key}"));
        var replayedSession = await replaySession.Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        Assert.Equal(HttpStatusCode.OK, replaySession.StatusCode);
        Assert.True(replayedSession!.IsReplay);
        Assert.Equal(session.SessionId, replayedSession.SessionId);

        var messageResponse = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session.SessionId}/messages",
            new AppendWeddingPlannerMessageRequest("OPERATOR", "Resume this planning note.", "DashboardFixture", $"msg-{key}"));
        var message = await messageResponse.Content.ReadFromJsonAsync<WeddingPlannerMessageDto>();
        Assert.Equal(HttpStatusCode.Created, messageResponse.StatusCode);
        Assert.Equal(1, message!.SequenceNumber);

        var replayMessage = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session.SessionId}/messages",
            new AppendWeddingPlannerMessageRequest("OPERATOR", "Resume this planning note.", "DashboardFixture", $"msg-{key}"));
        Assert.Equal(HttpStatusCode.OK, replayMessage.StatusCode);

        var loadedSession = await client.GetFromJsonAsync<WeddingPlannerSessionDto>($"/api/wedding-planner/sessions/{session.SessionId}");
        var loadedMessages = await client.GetFromJsonAsync<List<WeddingPlannerMessageDto>>($"/api/wedding-planner/sessions/{session.SessionId}/messages");
        var loadedMessage = await client.GetAsync($"/api/wedding-planner/messages/{message.MessageId}");
        var audit = await client.GetFromJsonAsync<List<WeddingPlannerAuditEventDto>>($"/api/wedding-planner/workspaces/{first.WorkspaceId}/audit");

        Assert.NotNull(loadedSession);
        Assert.Equal(session.SessionId, loadedSession!.SessionId);
        Assert.Single(loadedMessages!);
        Assert.Equal(HttpStatusCode.OK, loadedMessage.StatusCode);
        Assert.NotNull(audit);
        Assert.Contains(audit, x => x.Action == "WORKSPACE_OPENED");
        Assert.Contains(audit, x => x.Action == "SESSION_CREATED");
        Assert.Contains(audit, x => x.Action == "MESSAGE_APPENDED");
    }

    [Fact]
    public async Task Invalid_ids_and_ai_actors_fail_safely()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var missing = Guid.NewGuid();

        var workspace = await client.GetAsync($"/api/wedding-planner/workspaces/{missing}");
        var session = await client.GetAsync($"/api/wedding-planner/sessions/{missing}");
        var message = await client.GetAsync($"/api/wedding-planner/messages/{missing}");
        var put = await client.PutAsJsonAsync($"/api/wedding-planner/workspaces/{missing}", new { });
        var planner = await client.PostAsJsonAsync("/api/wedding-planner/workspaces", OpenRequest(missing, $"missing-{Guid.NewGuid():N}"));

        Assert.Equal(HttpStatusCode.NotFound, workspace.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, session.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, message.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, planner.StatusCode);

        var opened = await (await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            OpenRequest(WeddingPlannerDataSeeder.DentalManilaId, $"dental-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var createdSession = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{opened!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("DashboardFixture", $"s-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        var ai = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{createdSession!.SessionId}/messages",
            new AppendWeddingPlannerMessageRequest("PLANNER", "Pretend this is AI.", "DashboardFixture", $"ai-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.BadRequest, ai.StatusCode);
        Assert.Contains("out of scope", await ai.Content.ReadAsStringAsync());
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
    }

    private static OpenWeddingPlannerWorkspaceRequest OpenRequest(Guid advertiserId, string key) =>
        new(advertiserId, "DashboardFixture", key);
}

public sealed class WeddingPlannerPhase1IsolationTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public WeddingPlannerPhase1IsolationTests(WeddingPlannerOidcFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_and_cross_tenant_access_are_rejected()
    {
        await SeedAsync();
        var anonymous = _factory.CreateSecureClient();
        var anonymousGet = await anonymous.GetAsync("/api/wedding-planner/workspaces");
        var anonymousPost = await anonymous.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.DentalManilaId, "SECURITY_TEST", "anon"));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousGet.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousPost.StatusCode);

        var dental = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.DentalManilaId, "Dental Manila");
        var restaurant = await CreateAdvertiserClientAsync(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "Restaurant Santo Domingo");

        var dentalOpen = await dental.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.DentalManilaId, "SECURITY_TEST", $"dental-{Guid.NewGuid():N}"));
        var dentalWorkspace = await dentalOpen.Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        Assert.Equal(HttpStatusCode.Created, dentalOpen.StatusCode);

        var restaurantOpen = await restaurant.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "SECURITY_TEST", $"rest-{Guid.NewGuid():N}"));
        var restaurantWorkspace = await restaurantOpen.Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        Assert.Equal(HttpStatusCode.Created, restaurantOpen.StatusCode);

        var stolenWorkspace = await dental.GetAsync($"/api/wedding-planner/workspaces/{restaurantWorkspace!.WorkspaceId}");
        Assert.Equal(HttpStatusCode.NotFound, stolenWorkspace.StatusCode);

        var hijackOpen = await dental.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.RestaurantSantoDomingoId, "SECURITY_TEST", $"hijack-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Forbidden, hijackOpen.StatusCode);

        var dentalSession = await (await dental.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{dentalWorkspace!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest("SECURITY_TEST", $"ds-{Guid.NewGuid():N}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        var stolenSession = await restaurant.GetAsync($"/api/wedding-planner/sessions/{dentalSession!.SessionId}");
        var stolenWrite = await restaurant.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{dentalSession.SessionId}/messages",
            new AppendWeddingPlannerMessageRequest("ADVERTISER", "Should not land in Dental.", "SECURITY_TEST", $"bad-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.NotFound, stolenSession.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stolenWrite.StatusCode);

        var viewer = _factory.CreateSecureClient();
        viewer.DefaultRequestHeaders.Add("X-Test-Name", "Read Only");
        viewer.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.viewer");
        var viewerList = await viewer.GetAsync("/api/wedding-planner/workspaces");
        var viewerWrite = await viewer.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.DentalManilaId, "SECURITY_TEST", "viewer"));
        Assert.Equal(HttpStatusCode.OK, viewerList.StatusCode);
        Assert.Equal("[]", (await viewerList.Content.ReadAsStringAsync()).Trim());
        Assert.Equal(HttpStatusCode.BadRequest, viewerWrite.StatusCode);
        Assert.Contains("CSRF", await viewerWrite.Content.ReadAsStringAsync());

        var session = await viewer.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        viewer.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        var viewerWriteAfterCsrf = await viewer.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(WeddingPlannerDataSeeder.DentalManilaId, "SECURITY_TEST", "viewer-2"));
        Assert.Equal(HttpStatusCode.Forbidden, viewerWriteAfterCsrf.StatusCode);
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

public sealed class WeddingPlannerOidcFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Enabled", "true");
        builder.UseSetting("Authentication:Authority", "https://identity.example.test");
        builder.UseSetting("Authentication:ClientId", "bliss-tests");
        builder.UseSetting("Runtime:WriteRateLimitPermitLimit", "1000");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                    || descriptor.ServiceType == typeof(BlissDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(_dbName));
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
        });
    }

    public HttpClient CreateSecureClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
}

public sealed class WeddingPlannerPhase1BoundaryTests
{
    [Fact]
    public void Phase1_source_does_not_invoke_ai_or_alpha_auto()
    {
        var service = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Infrastructure", "Persistence", "WeddingPlannerService.cs"));
        var controller = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Api", "Controllers", "WeddingPlannerController.cs"));
        foreach (var source in new[] { service, controller })
        {
            Assert.DoesNotContain("OpenAI", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ChatGPT", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Alpha Auto", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DeterministicRuleEvaluator", source);
            Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
        }
    }
}
