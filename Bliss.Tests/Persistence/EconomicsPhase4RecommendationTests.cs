using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase4RecommendationTests
{
    [Fact]
    public async Task Recommendation_is_a_replayable_explainable_range()
    {
        await using var db = TestDb.CreateContext();
        await SeedAsync(db);
        var service = new RateRecommendationService(db);
        var command = MidRollCommand("phase4-range-1");

        var created = await service.GenerateAsync(command);
        var replay = await service.GenerateAsync(command);

        Assert.False(created.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.Equal(created.Recommendation.Id, replay.Recommendation.Id);
        Assert.Equal(198m, created.Recommendation.RangeLow);
        Assert.Equal(242m, created.Recommendation.RangeTarget);
        Assert.Equal(286m, created.Recommendation.RangeHigh);
        Assert.Equal("MEDIUM", created.Recommendation.ConfidenceLevel);
        Assert.Equal(42_000, created.Recommendation.EstimatedImpressions);
        Assert.Equal(5, created.Recommendation.Factors.Count);
        Assert.Single(created.Recommendation.Sources);
        Assert.Contains(created.Recommendation.Factors,
            x => x.FactorCode == "DURATION_BAND"
                && x.Rationale.Contains("no per-minute multiplication"));
    }

    [Fact]
    public async Task Ten_times_duration_is_not_ten_times_the_range()
    {
        await using var db = TestDb.CreateContext();
        await SeedAsync(db);
        var service = new RateRecommendationService(db);

        var midRoll = await service.GenerateAsync(MidRollCommand("phase4-duration-short"));
        var sponsored = await service.GenerateAsync(new GenerateRateRecommendationCommand(
            Phase2DataSeeder.BrazilCreatorId,
            Guid.Parse("99999999-9999-9999-9999-999999999998"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            600,
            null,
            null,
            "WOMENS_FOOTWEAR",
            "TEST long integrated segment",
            "TEST",
            "phase4-duration-long"));

        Assert.Equal(10, sponsored.Recommendation.DurationSeconds / midRoll.Recommendation.DurationSeconds);
        Assert.True(sponsored.Recommendation.RangeTarget < midRoll.Recommendation.RangeTarget * 10);
    }

    [Fact]
    public async Task Historical_output_does_not_change_when_inputs_change()
    {
        await using var db = TestDb.CreateContext();
        await SeedAsync(db);
        var service = new RateRecommendationService(db);
        var generated = await service.GenerateAsync(MidRollCommand("phase4-history"));
        var originalTarget = generated.Recommendation.RangeTarget;

        var benchmark = await db.InventoryRateBenchmarks.SingleAsync(
            x => x.Id == generated.Recommendation.Sources.Single().InventoryRateBenchmarkId);
        benchmark.RangeLow = 9_999m;
        benchmark.RangeHigh = 19_999m;
        await db.SaveChangesAsync();

        var historical = await db.RateRecommendations.SingleAsync(x => x.Id == generated.Recommendation.Id);
        Assert.Equal(originalTarget, historical.RangeTarget);
    }

    [Fact]
    public async Task Missing_creator_provenance_cannot_produce_high_confidence()
    {
        await using var db = TestDb.CreateContext();
        await SeedAsync(db);
        var service = new RateRecommendationService(db);

        var result = await service.GenerateAsync(new GenerateRateRecommendationCommand(
            Phase2DataSeeder.BrazilCreatorId,
            Guid.Parse("99999999-9999-9999-9999-999999999998"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            600,
            null,
            null,
            "WOMENS_FOOTWEAR",
            "TEST provenance confidence",
            "TEST",
            "phase4-confidence"));

        Assert.Equal("LOW", result.Recommendation.ConfidenceLevel);
        Assert.Null(result.Recommendation.EstimatedImpressions);
    }

    private static GenerateRateRecommendationCommand MidRollCommand(string key) =>
        new(
            Phase1DataSeeder.CreatorId,
            Guid.Parse("99999999-9999-9999-9999-999999999992"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            60,
            Phase1DataSeeder.OpportunityAId,
            Phase1DataSeeder.MatchAId,
            "WOMENS_FOOTWEAR",
            "Reach women 18-34 in Manila",
            "TEST",
            key);

    private static async Task SeedAsync(BlissDbContext db)
    {
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();
        await new EconomicsPhase4DataSeeder(db).SeedAsync();
    }
}
