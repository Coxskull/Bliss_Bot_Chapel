using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Domain.Common;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase8HandshakeApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase8HandshakeApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Planner_requests_and_reads_explainable_economics_result()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var workspace = await (await client.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(
                Phase1DataSeeder.AdvertiserId,
                "PHASE8_API_TEST",
                $"workspace-{suffix}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var session = await (await client.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest(
                "PHASE8_API_TEST",
                $"session-{suffix}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        var request = Request($"recommendation-{suffix}");

        var createdResponse = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session!.SessionId}/economics/recommendations",
            request);
        var created = await createdResponse.Content
            .ReadFromJsonAsync<WeddingPlannerEconomicsRequestDto>();
        var replayResponse = await client.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session.SessionId}/economics/recommendations",
            request);
        var replay = await replayResponse.Content
            .ReadFromJsonAsync<WeddingPlannerEconomicsRequestDto>();

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.NotNull(created);
        Assert.NotNull(replay);
        Assert.False(created!.IsReplay);
        Assert.True(replay!.IsReplay);
        Assert.Equal(created.Id, replay.Id);
        Assert.Equal(created.Recommendation.Id, replay.Recommendation.Id);
        Assert.Equal("RECOMMENDATION_READY", created.Status);
        Assert.Equal(198m, created.Recommendation.RangeLow);
        Assert.Equal(242m, created.Recommendation.RangeTarget);
        Assert.Equal(286m, created.Recommendation.RangeHigh);
        Assert.Equal("CPM", created.Recommendation.PricingModelCode);
        Assert.Equal(5, created.Recommendation.Factors.Count);
        Assert.Single(created.Recommendation.Sources);

        var loaded = await client.GetFromJsonAsync<WeddingPlannerEconomicsRequestDto>(
            $"/api/wedding-planner/economics/recommendations/{created.Id}");
        var listed = await client.GetFromJsonAsync<List<WeddingPlannerEconomicsRequestDto>>(
            $"/api/wedding-planner/sessions/{session.SessionId}/economics/recommendations");
        Assert.Equal(created.Id, loaded!.Id);
        Assert.Single(listed!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Empty(await db.Quotes.Where(
            x => x.SourceSystem == "PHASE8_API_TEST").ToListAsync());
        Assert.Empty(await db.CampaignPlacementRuns.Where(
            x => x.SourceSystem == "PHASE8_API_TEST").ToListAsync());
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();
        await new EconomicsPhase4DataSeeder(db).SeedAsync();
        var match = await db.BlissMatches.SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId);
        match.Status = EntityStatuses.Approved;
        await db.SaveChangesAsync();
    }

    private static RequestWeddingPlannerEconomicsRecommendationRequest Request(string key) =>
        new(
            Phase1DataSeeder.MatchAId,
            Guid.Parse("99999999-9999-9999-9999-999999999992"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            60,
            "WELLNESS",
            "Reach women 18-34 in Manila",
            "PHASE8_API_TEST",
            key);
}

public sealed class EconomicsPhase8HandshakeIsolationTests
    : IClassFixture<WeddingPlannerOidcFactory>
{
    private readonly WeddingPlannerOidcFactory _factory;

    public EconomicsPhase8HandshakeIsolationTests(WeddingPlannerOidcFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Advertiser_handshake_is_tenant_scoped_and_direct_economics_write_stays_forbidden()
    {
        await SeedAsync();
        var sunrise = await CreateAdvertiserClientAsync(
            Phase1DataSeeder.AdvertiserId, "Sunrise advertiser");
        var dental = await CreateAdvertiserClientAsync(
            WeddingPlannerDataSeeder.DentalManilaId, "Dental advertiser");
        var suffix = Guid.NewGuid().ToString("N");
        var workspace = await (await sunrise.PostAsJsonAsync(
            "/api/wedding-planner/workspaces",
            new OpenWeddingPlannerWorkspaceRequest(
                Phase1DataSeeder.AdvertiserId,
                "PHASE8_SECURITY_TEST",
                $"workspace-{suffix}")))
            .Content.ReadFromJsonAsync<WeddingPlannerWorkspaceDto>();
        var session = await (await sunrise.PostAsJsonAsync(
            $"/api/wedding-planner/workspaces/{workspace!.WorkspaceId}/sessions",
            new CreateWeddingPlannerSessionRequest(
                "PHASE8_SECURITY_TEST",
                $"session-{suffix}")))
            .Content.ReadFromJsonAsync<WeddingPlannerSessionDto>();
        var created = await sunrise.PostAsJsonAsync(
            $"/api/wedding-planner/sessions/{session!.SessionId}/economics/recommendations",
            RequestWeddingPlannerEconomicsRecommendation($"request-{suffix}"));
        var dto = await created.Content
            .ReadFromJsonAsync<WeddingPlannerEconomicsRequestDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await dental.GetAsync(
                $"/api/wedding-planner/economics/recommendations/{dto!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await dental.PostAsJsonAsync(
                $"/api/wedding-planner/sessions/{session.SessionId}/economics/recommendations",
                RequestWeddingPlannerEconomicsRecommendation($"stolen-{suffix}"))).StatusCode);

        var direct = await sunrise.PostAsJsonAsync(
            "/api/economics/recommendations",
            new
            {
                creatorId = Phase1DataSeeder.CreatorId,
                adInventorySlotId =
                    Guid.Parse("99999999-9999-9999-9999-999999999992"),
                geographicMarketId = EconomicsDataSeeder.ManilaId,
                pricingModelCode = "CPM",
                sourceSystem = "PHASE8_SECURITY_TEST",
                idempotencyKey = $"direct-{suffix}"
            });
        Assert.Equal(HttpStatusCode.Forbidden, direct.StatusCode);
    }

    private async Task<HttpClient> CreateAdvertiserClientAsync(Guid advertiserId, string name)
    {
        var client = _factory.CreateSecureClient();
        client.DefaultRequestHeaders.Add("X-Test-Name", name);
        client.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.advertiser");
        client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id", advertiserId.ToString());
        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session!.CsrfToken);
        return client;
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new WeddingPlannerDataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();
        await new EconomicsPhase4DataSeeder(db).SeedAsync();
        var match = await db.BlissMatches.SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId);
        match.Status = EntityStatuses.Approved;
        await db.SaveChangesAsync();
    }

    private static RequestWeddingPlannerEconomicsRecommendationRequest
        RequestWeddingPlannerEconomicsRecommendation(string key) =>
        new(
            Phase1DataSeeder.MatchAId,
            Guid.Parse("99999999-9999-9999-9999-999999999992"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            60,
            "WELLNESS",
            "Reach women 18-34 in Manila",
            "PHASE8_SECURITY_TEST",
            key);
}
