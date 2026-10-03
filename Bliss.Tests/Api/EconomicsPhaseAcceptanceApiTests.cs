using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhaseAcceptanceApiTests
{
    [Fact]
    public async Task Owner_acceptance_cites_no_amount_when_history_is_empty()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/acceptance");
        Assert.False(before!.GetProperty("accepted").GetBoolean());
        Assert.Equal(0, before.GetProperty("placementCount").GetInt32());
        Assert.Equal(0, before.GetProperty("campaignCount").GetInt32());
        Assert.Contains("No historical actual is on file", before.GetProperty("historyLine").GetString());
        Assert.False(before.GetProperty("repricingAuthorized").GetBoolean());
        Assert.False(before.GetProperty("settlementAuthorized").GetBoolean());
        Assert.False(before.GetProperty("recommendationRewritten").GetBoolean());
        Assert.Equal("NOT_SENT", before.GetProperty("delivery").GetString());

        var stored = await client.PostAsJsonAsync("/api/operations/acceptance", new { phase = 9 });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.True(first.GetProperty("accepted").GetBoolean());
        Assert.False(first.GetProperty("duplicate").GetBoolean());
        Assert.Equal(0, first.GetProperty("placementCount").GetInt32());
        Assert.Contains("accepted by the owner", first.GetProperty("notice").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/acceptance", new { phase = 9 });
        var second = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.False(second.GetProperty("written").GetBoolean());

        var reprice = await client.PostAsJsonAsync("/api/operations/acceptance", new { phase = 9, reprice = true });
        Assert.Equal(HttpStatusCode.BadRequest, reprice.StatusCode);
        var refused = await reprice.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not changed", refused!.GetProperty("error").GetString());
        Assert.False(refused.GetProperty("recommendationRewritten").GetBoolean());
    }

    [Fact]
    public async Task A_stored_placement_actual_is_cited_and_the_recommendation_stays()
    {
        await using var factory = new BlissApiFactory();
        Guid recommendationId;
        decimal target;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var fixture = await EconomicsPhase9TestData.SeedAsync(db, "acceptance");
            var service = new HistoricalEconomicsService(db);
            await service.RecordPlacementAsync(new(
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
                "TEST placement actual",
                "PHASE9_TEST",
                "acceptance-actual"));
            recommendationId = fixture.RateRecommendationId;
            target = await db.RateRecommendations.AsNoTracking()
                .Where(item => item.Id == recommendationId)
                .Select(item => item.RangeTarget)
                .SingleAsync();
        }

        var client = factory.CreateClient();
        var stored = await client.PostAsJsonAsync("/api/operations/acceptance", new { phase = 9 });
        var body = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(body!.GetProperty("recommendationMatches").GetBoolean());
        Assert.False(body.GetProperty("recommendationRewritten").GetBoolean());
        Assert.Contains("Contracted 215 PHP", body.GetProperty("historyLine").GetString());
        Assert.True(body.GetProperty("placementCount").GetInt32() >= 1);

        using var check = factory.Services.CreateScope();
        var after = check.ServiceProvider.GetRequiredService<BlissDbContext>();
        var targetAfter = await after.RateRecommendations.AsNoTracking()
            .Where(item => item.Id == recommendationId)
            .Select(item => item.RangeTarget)
            .SingleAsync();
        Assert.Equal(target, targetAfter);
    }
}
