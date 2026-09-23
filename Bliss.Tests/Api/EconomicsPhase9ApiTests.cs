using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase9ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase9ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Historical_learning_api_appends_actuals_and_campaign_snapshot()
    {
        var suffix = Guid.NewGuid().ToString("N");
        EconomicsPhase9Fixture fixture;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            fixture = await EconomicsPhase9TestData.SeedAsync(db, $"api-{suffix}");
        }
        var client = _factory.CreateClient();
        var request = new RecordHistoricalPlacementEconomicsRequest(
            fixture.CampaignPlacementId,
            fixture.QuoteOutcomeId,
            fixture.QuoteLineItemId,
            fixture.CompensationIllustrationId,
            null,
            null,
            50_000,
            43_000,
            null,
            3_500,
            120,
            DateTime.UtcNow.AddHours(-1),
            "API actual",
            "PHASE9_API_TEST",
            $"placement-{suffix}");

        var createdResponse = await client.PostAsJsonAsync(
            "/api/economics/historical-placements", request);
        var created = await createdResponse.Content
            .ReadFromJsonAsync<HistoricalPlacementEconomicsDto>();
        var replayResponse = await client.PostAsJsonAsync(
            "/api/economics/historical-placements", request);
        var replay = await replayResponse.Content
            .ReadFromJsonAsync<HistoricalPlacementEconomicsDto>();

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.False(created!.IsReplay);
        Assert.True(replay!.IsReplay);
        Assert.Equal(created.Id, replay.Id);
        Assert.Equal(215m, created.ContractedAmount);
        Assert.Equal(4.3m, created.EffectiveCpm);
        Assert.Equal(-11.157025m,
            created.ContractedVsRecommendationTargetPercentage);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync(
                $"/api/economics/historical-placements/{created.Id}")).StatusCode);

        var campaignResponse = await client.PostAsJsonAsync(
            "/api/economics/campaign-performance",
            new RecordCampaignPerformanceEconomicsRequest(
                fixture.CampaignId,
                null,
                50_000,
                43_000,
                null,
                3_500,
                0.07m,
                120,
                1_800m,
                "PHP",
                DateTime.UtcNow.AddHours(-1),
                "API campaign actual",
                "PHASE9_API_TEST",
                $"campaign-{suffix}"));
        var campaign = await campaignResponse.Content
            .ReadFromJsonAsync<CampaignPerformanceEconomicsDto>();
        Assert.Equal(HttpStatusCode.Created, campaignResponse.StatusCode);
        Assert.Equal(1, campaign!.AlphaPlacementCount);
        Assert.Equal(215m, campaign.AlphaContractedAmount);
        Assert.Equal(4.3m, campaign.EffectiveCpm);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync(
                $"/api/economics/campaign-performance/{campaign.Id}")).StatusCode);
        Assert.NotEmpty((await client.GetFromJsonAsync<
            List<HistoricalPlacementEconomicsDto>>(
                "/api/economics/historical-placements"))!);
        Assert.NotEmpty((await client.GetFromJsonAsync<
            List<CampaignPerformanceEconomicsDto>>(
                "/api/economics/campaign-performance"))!);
    }

    [Fact]
    public async Task Historical_api_rejects_unknown_measurements()
    {
        var suffix = Guid.NewGuid().ToString("N");
        EconomicsPhase9Fixture fixture;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            fixture = await EconomicsPhase9TestData.SeedAsync(db, $"unknown-{suffix}");
        }
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/economics/historical-placements",
            new RecordHistoricalPlacementEconomicsRequest(
                fixture.CampaignPlacementId,
                fixture.QuoteOutcomeId,
                fixture.QuoteLineItemId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                DateTime.UtcNow,
                null,
                "PHASE9_API_TEST",
                $"unknown-{suffix}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            "At least one actual measurement",
            await response.Content.ReadAsStringAsync());
    }
}
