using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase9HistoricalTests
{
    [Fact]
    public async Task Placement_actual_is_explainable_replayable_and_non_settling()
    {
        await using var db = TestDb.CreateContext();
        var fixture = await EconomicsPhase9TestData.SeedAsync(db, "placement-main");
        var service = new HistoricalEconomicsService(db);
        var command = PlacementCommand(fixture, "placement-actual");
        var recommendationBefore = await db.RateRecommendations.AsNoTracking()
            .SingleAsync(x => x.Id == fixture.RateRecommendationId);
        var quoteBefore = await db.Quotes.AsNoTracking().SingleAsync();
        var placementBefore = await db.CampaignPlacements.AsNoTracking()
            .SingleAsync(x => x.Id == fixture.CampaignPlacementId);

        var created = await service.RecordPlacementAsync(command);
        var replay = await service.RecordPlacementAsync(command);

        Assert.False(created.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.Equal(created.Record.Id, replay.Record.Id);
        Assert.Equal(215m, created.Record.QuotedAmount);
        Assert.Equal(215m, created.Record.ContractedAmount);
        Assert.Equal(4.3m, created.Record.EffectiveCpm);
        Assert.Equal(0.005m, created.Record.EffectiveCpv);
        Assert.Equal(-11.157025m,
            created.Record.ContractedVsRecommendationTargetPercentage);
        Assert.Equal(-2.272727m,
            created.Record.ContractedVsExternalMidpointPercentage);
        Assert.Equal(180m, created.Record.ExternalBenchmarkLow);
        Assert.Equal(260m, created.Record.ExternalBenchmarkHigh);
        Assert.Equal(38.70m, created.Record.AlphaCompensationAmount);
        Assert.Equal(154.80m, created.Record.CreatorCompensationAmount);
        Assert.Equal(21.50m, created.Record.OtherCompensationAmount);
        Assert.Equal(
            recommendationBefore.RangeTarget,
            (await db.RateRecommendations.AsNoTracking()
                .SingleAsync(x => x.Id == fixture.RateRecommendationId)).RangeTarget);
        Assert.Equal(
            quoteBefore.Status,
            (await db.Quotes.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(
            placementBefore.Status,
            (await db.CampaignPlacements.AsNoTracking()
                .SingleAsync(x => x.Id == fixture.CampaignPlacementId)).Status);
    }

    [Fact]
    public async Task Corrections_append_and_campaign_rollup_uses_current_history()
    {
        await using var db = TestDb.CreateContext();
        var fixture = await EconomicsPhase9TestData.SeedAsync(db, "correction");
        var service = new HistoricalEconomicsService(db);
        var original = await service.RecordPlacementAsync(
            PlacementCommand(fixture, "original"));
        var correctedCommand = PlacementCommand(fixture, "corrected") with
        {
            SupersedesHistoricalPlacementEconomicsId = original.Record.Id,
            ActualImpressions = 52_000,
            ActualViews = 44_000,
            ActualEngagements = 3_700
        };
        var corrected = await service.RecordPlacementAsync(correctedCommand);
        var campaign = await service.RecordCampaignAsync(new(
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
            "TEST campaign snapshot",
            "PHASE9_TEST",
            "campaign-rollup"));

        Assert.NotEqual(original.Record.Id, corrected.Record.Id);
        Assert.Equal(original.Record.Id,
            corrected.Record.SupersedesHistoricalPlacementEconomicsId);
        Assert.Equal(2, await db.HistoricalPlacementEconomics.CountAsync());
        Assert.Equal(50_000, original.Record.ActualImpressions);
        Assert.Equal(52_000, corrected.Record.ActualImpressions);
        Assert.Equal(1, campaign.Record.AlphaPlacementCount);
        Assert.Equal(215m, campaign.Record.AlphaContractedAmount);
        Assert.Equal("PHP", campaign.Record.AlphaContractedCurrencyCode);
        Assert.Equal(4.3m, campaign.Record.EffectiveCpm);
        Assert.Equal(0.005m, campaign.Record.EffectiveCpv);
    }

    [Fact]
    public async Task Campaign_rollup_uses_latest_measurement_per_placement()
    {
        await using var db = TestDb.CreateContext();
        var fixture = await EconomicsPhase9TestData.SeedAsync(db, "latest");
        var service = new HistoricalEconomicsService(db);
        await service.RecordPlacementAsync(
            PlacementCommand(fixture, "earlier") with
            {
                MeasurementAsOf = DateTime.UtcNow.AddHours(-2)
            });
        await service.RecordPlacementAsync(
            PlacementCommand(fixture, "later") with
            {
                MeasurementAsOf = DateTime.UtcNow.AddHours(-1),
                ActualImpressions = 55_000
            });

        var campaign = await service.RecordCampaignAsync(new(
            fixture.CampaignId,
            null,
            55_000,
            43_000,
            null,
            3_500,
            0.07m,
            120,
            1_800m,
            "PHP",
            DateTime.UtcNow.AddMinutes(-30),
            "TEST latest placement rollup",
            "PHASE9_TEST",
            "campaign-latest"));

        Assert.Equal(2, await db.HistoricalPlacementEconomics.CountAsync());
        Assert.Equal(1, campaign.Record.AlphaPlacementCount);
        Assert.Equal(215m, campaign.Record.AlphaContractedAmount);
    }

    [Fact]
    public async Task Invalid_relationships_and_measurements_are_rejected()
    {
        await using var db = TestDb.CreateContext();
        var fixture = await EconomicsPhase9TestData.SeedAsync(db, "invalid");
        var service = new HistoricalEconomicsService(db);

        var negative = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordPlacementAsync(PlacementCommand(fixture, "negative") with
            {
                ActualViews = -1
            }));
        Assert.Contains("negative", negative.Message);

        var outcome = await db.QuoteOutcomes.SingleAsync(
            x => x.Id == fixture.QuoteOutcomeId);
        outcome.Response = "DECLINED";
        outcome.Amount = null;
        await db.SaveChangesAsync();
        var declined = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordPlacementAsync(PlacementCommand(fixture, "declined")));
        Assert.Contains("ACCEPTED", declined.Message);
        Assert.Empty(await db.HistoricalPlacementEconomics.ToListAsync());
    }

    private static RecordHistoricalPlacementEconomicsCommand PlacementCommand(
        EconomicsPhase9Fixture fixture,
        string key) =>
        new(
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
            key);
}
