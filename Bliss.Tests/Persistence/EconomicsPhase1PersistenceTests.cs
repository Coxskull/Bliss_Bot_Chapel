using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase1PersistenceTests
{
    [Fact]
    public async Task Seeder_persists_six_markets_and_all_pricing_models_as_data()
    {
        await using var db = TestDb.CreateContext();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();

        Assert.Equal(6, await db.GeographicMarkets.CountAsync());
        Assert.Equal(10, await db.PricingModels.CountAsync());
        Assert.Contains(await db.PricingModels.Select(x => x.Code).ToListAsync(),
            x => x == PricingModelCodes.Hybrid);
        Assert.All(await db.ResearchSources.ToListAsync(), x => Assert.False(x.IsApproved));
    }

    [Fact]
    public async Task New_observation_preserves_older_market_history()
    {
        await using var db = TestDb.CreateContext();
        await new EconomicsDataSeeder(db).SeedAsync();
        var originalCount = await db.MarketBenchmarkObservations
            .CountAsync(x => x.GeographicMarketId == EconomicsDataSeeder.ManilaId);

        db.MarketBenchmarkObservations.Add(new MarketBenchmarkObservation
        {
            Id = Guid.NewGuid(),
            GeographicMarketId = EconomicsDataSeeder.ManilaId,
            ResearchSourceId = EconomicsDataSeeder.TestSourceId,
            Metric = "TEST_DIGITAL_AD_COST_INDEX",
            NumericValue = 111,
            RetrievedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ConfidenceLevel = EconomicsConfidenceLevels.Low,
            VerificationStatus = ObservationVerificationStatuses.Estimated
        });
        await db.SaveChangesAsync();

        Assert.Equal(originalCount + 1, await db.MarketBenchmarkObservations
            .CountAsync(x => x.GeographicMarketId == EconomicsDataSeeder.ManilaId));
    }
}
