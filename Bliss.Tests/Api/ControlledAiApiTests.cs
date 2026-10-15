using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Api.Controllers;
using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bliss.Tests.Api;

public sealed class ControlledAiApiTests : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory factory;
    public ControlledAiApiTests(WeddingPlannerOidcFactory factory) => this.factory = factory;
    private sealed record Submitted(ControlledAiRun Run, bool IsReplay);
    private sealed record Detail(ControlledAiRun Run, List<ControlledAiEvent> Events, bool CampaignReady);
    private sealed record Attachment(AcademyProductionCandidate Candidate);
    private sealed class Scenario(WebApplicationFactory<Program> app) : IAsyncDisposable
    {
        public WebApplicationFactory<Program> App { get; } = app;
        public HttpClient Owner = null!;
        public Guid Workspace, Session, Brief;
        public ControlledAiLifecycleTests.Provider Provider = null!;
        public string Path => $"/api/wedding-planner/workspaces/{Workspace}/ai";
        public SubmitAiRun Request(string key = "text") => new("TEXT", Session, null, null, "Plan one campaign idea", key, true);
        public async Task RunOne() { using var scope = App.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<ControlledAiService>().RunOne(default); }
        public ValueTask DisposeAsync() { App.Dispose(); return ValueTask.CompletedTask; }
    }
    private async Task<HttpClient> Client(WebApplicationFactory<Program> app, Guid advertiser)
    {
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        client.DefaultRequestHeaders.Add("X-Test-Name", $"AI Owner {Guid.NewGuid():N}");
        client.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.advertiser");
        client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiser.ToString());
        var response = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", response!.CsrfToken); return client;
    }
    private async Task<Scenario> Setup(bool enabled = true)
    {
        var runtime = ControlledAiProviderTests.Runtime(); runtime.ControlledAi.Enabled = enabled;
        var provider = new ControlledAiLifecycleTests.Provider(); var dbName = Guid.NewGuid().ToString();
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<BlissRuntimeOptions>(); services.AddSingleton(runtime);
            services.RemoveAll<IControlledAiProvider>(); services.AddSingleton<IControlledAiProvider>(provider);
            foreach (var descriptor in services.Where(x => x.ImplementationType == typeof(ControlledAiWorker)).ToList()) services.Remove(descriptor);
            services.RemoveAll<BlissDbContext>(); services.RemoveAll<DbContextOptions<BlissDbContext>>();
            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(dbName));
        }));
        var scenario = new Scenario(app) { Provider = provider }; var advertiserId = Guid.NewGuid();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            db.Add(new Advertiser { Id = advertiserId, Name = "TEST AI Owner", CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
            var planner = scope.ServiceProvider.GetRequiredService<WeddingPlannerService>();
            var workspace = await planner.OpenPrimaryWorkspaceAsync(advertiserId, "TEST", "workspace", true, null, "OPERATOR", "tester", "test");
            scenario.Workspace = workspace.WorkspaceId;
            scenario.Session = (await planner.CreateSessionAsync(workspace.WorkspaceId, "TEST", "session", true, null, "OPERATOR", "tester", "test")).SessionId;
            var brand = new WeddingPlannerBrandDna { Id = Guid.NewGuid(), WorkspaceId = workspace.WorkspaceId, Version = 1,
                Status = "CONFIRMED", BrandName = "TEST AI Pharmacy", Niche = "pharmacy", Market = "Taipei", Language = "English", Goals = "Awareness", IdempotencyKey = "brand", CreatedAt = DateTime.UtcNow };
            db.Add(brand); await db.SaveChangesAsync();
            scenario.Brief = (await scope.ServiceProvider.GetRequiredService<AcademyProductionService>().CreateAsync(workspace,
                new CreateProductionBrief(brand.Id, "pharmacy", "Neighborhood care", "Visit", "brief"), "OPERATOR", "tester", "test", default)).Row.Id;
        }
        scenario.Owner = await Client(app, advertiserId); return scenario;
    }
    [Fact]
    public async Task Text_runs_enforce_consent_tenant_csrf_and_preserve_draft_history()
    {
        await using var s = await Setup(); var path = s.Path + "/runs";
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.PostAsJsonAsync(path, s.Request() with { ConsentToShare = false })).StatusCode);
        var first = await s.Owner.PostAsJsonAsync(path, s.Request()); Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var run = (await first.Content.ReadFromJsonAsync<Submitted>())!.Run;
        Assert.Equal(HttpStatusCode.OK, (await s.Owner.PostAsJsonAsync(path, s.Request())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Owner.PostAsJsonAsync(path, s.Request() with { Prompt = "Changed" })).StatusCode);
        await s.RunOne();
        var detail = await s.Owner.GetFromJsonAsync<Detail>($"{path}/{run.Id}");
        Assert.Equal("SUCCEEDED_PENDING_REVIEW", detail!.Run.Status); Assert.False(detail.CampaignReady); Assert.Equal(3, detail.Events.Count);
        Assert.Null(detail.Run.ActualUsd); Assert.DoesNotContain("test-not-a-live-key", detail.Run.InputSnapshotJson);
        var other = await Client(s.App, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{path}/{run.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"{path}/{run.Id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path, s.Request())).StatusCode);
        var anonymous = s.App.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        s.Owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.PostAsJsonAsync(path, s.Request("csrf"))).StatusCode);
        Assert.Equal(1, s.Provider.Calls);
    }
    [Fact]
    public async Task Image_draft_is_private_and_explicit_attachment_creates_one_unreviewed_candidate()
    {
        await using var s = await Setup(); var path = s.Path + "/runs";
        var request = new SubmitAiRun("IMAGE", null, s.Brief, null, "", "image", true);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Owner.PostAsJsonAsync(path, request with { Prompt = "Override" })).StatusCode);
        var queued = await s.Owner.PostAsJsonAsync(path, request); queued.EnsureSuccessStatusCode();
        var run = (await queued.Content.ReadFromJsonAsync<Submitted>())!.Run; await s.RunOne();
        Assert.DoesNotContain("imageBase64", s.Provider.Requests[0].InputJson);
        Assert.Contains("productionRecipe", s.Provider.Requests[0].InputJson);
        var imagePath = $"{path}/{run.Id}/image";
        var image = await s.Owner.GetAsync(imagePath); Assert.Equal(Convert.FromBase64String(AcademyProductionTests.Pixel), await image.Content.ReadAsByteArrayAsync());
        var other = await Client(s.App, Guid.NewGuid()); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(imagePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.App.CreateClient().GetAsync(imagePath)).StatusCode);
        var attached = await s.Owner.PostAsJsonAsync($"{path}/{run.Id}/attach-candidate", new AttachAiCandidate(0, "AI draft"));
        attached.EnsureSuccessStatusCode(); var candidate = (await attached.Content.ReadFromJsonAsync<Attachment>())!.Candidate;
        Assert.Equal(HttpStatusCode.OK, (await s.Owner.PostAsJsonAsync($"{path}/{run.Id}/attach-candidate", new AttachAiCandidate(0, "AI draft"))).StatusCode);
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Single(await db.AcademyProductionCandidates.ToListAsync()); Assert.Empty(await db.AcademyProductionReviews.ToListAsync());
        Assert.Equal(candidate.Id, (await db.ControlledAiRuns.SingleAsync()).AttachedCandidateId);
        Assert.Single(await db.ControlledAiEvents.Where(x => x.Action == "IMAGE_ATTACHED").ToListAsync());
    }
    [Fact]
    public async Task Disabled_provider_never_reserves_or_dispatches()
    {
        await using var s = await Setup(false);
        var response = await s.Owner.PostAsJsonAsync(s.Path + "/runs", s.Request()); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await s.Owner.GetStringAsync(s.Path + "/readiness"));
        Assert.False(document.RootElement.GetProperty("textReady").GetBoolean());
        using var scope = s.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Empty(await db.ControlledAiRuns.ToListAsync()); Assert.Empty(await db.ControlledAiDailyUsage.ToListAsync()); Assert.Equal(0, s.Provider.Calls);
    }
}
