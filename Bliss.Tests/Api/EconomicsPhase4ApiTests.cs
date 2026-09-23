using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase4ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase4ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Recommendation_endpoint_returns_and_replays_explainable_range()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new Phase2DataSeeder(db).SeedAsync();
            await new EconomicsDataSeeder(db).SeedAsync();
            await new EconomicsPhase2DataSeeder(db).SeedAsync();
            await new EconomicsPhase3DataSeeder(db).SeedAsync();
            await new EconomicsPhase4DataSeeder(db).SeedAsync();
        }

        var client = _factory.CreateClient();
        var key = $"phase4-api-{Guid.NewGuid():N}";
        var request = new
        {
            creatorId = Phase1DataSeeder.CreatorId,
            adInventorySlotId = Guid.Parse("99999999-9999-9999-9999-999999999992"),
            geographicMarketId = EconomicsDataSeeder.ManilaId,
            pricingModelCode = "CPM",
            durationSeconds = 60,
            advertiserOpportunityId = Phase1DataSeeder.OpportunityAId,
            blissMatchId = Phase1DataSeeder.MatchAId,
            industryCategory = "WOMENS_FOOTWEAR",
            campaignObjective = "Reach women 18-34 in Manila",
            sourceSystem = "TEST",
            idempotencyKey = key
        };

        var created = await client.PostAsJsonAsync("/api/economics/recommendations", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var root = createdJson.RootElement;
        Assert.Equal(198m, root.GetProperty("rangeLow").GetDecimal());
        Assert.Equal(242m, root.GetProperty("rangeTarget").GetDecimal());
        Assert.Equal(286m, root.GetProperty("rangeHigh").GetDecimal());
        Assert.Equal("MEDIUM", root.GetProperty("confidenceLevel").GetString());
        Assert.Equal(42_000, root.GetProperty("estimatedImpressions").GetInt32());
        Assert.Equal(5, root.GetProperty("factors").GetArrayLength());
        Assert.Equal(1, root.GetProperty("sources").GetArrayLength());
        Assert.False(root.GetProperty("isReplay").GetBoolean());
        var id = root.GetProperty("id").GetGuid();

        var replay = await client.PostAsJsonAsync("/api/economics/recommendations", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(id, replayJson.RootElement.GetProperty("id").GetGuid());
        Assert.True(replayJson.RootElement.GetProperty("isReplay").GetBoolean());

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/economics/recommendations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/economics/recommendations")).StatusCode);

        var rules = await client.GetAsync("/api/economics/pricing-rule-versions");
        Assert.Equal(HttpStatusCode.OK, rules.StatusCode);
        Assert.Contains("economics-rate/1.0.0", await rules.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Recommendation_endpoint_rejects_unknown_inputs()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/economics/recommendations", new
        {
            creatorId = Guid.NewGuid(),
            adInventorySlotId = Guid.NewGuid(),
            geographicMarketId = Guid.NewGuid(),
            pricingModelCode = "UNKNOWN",
            durationSeconds = 60,
            sourceSystem = "TEST",
            idempotencyKey = $"phase4-invalid-{Guid.NewGuid():N}"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("CreatorId does not reference a creator",
            await response.Content.ReadAsStringAsync());
    }
}
