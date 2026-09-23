using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase3PersistenceTests
{
    [Fact]
    public async Task Market_versions_and_dated_fx_observations_preserve_history()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();

        var profiles = await db.MarketEconomicProfiles
            .Where(x => x.GeographicMarketId == EconomicsDataSeeder.ManilaId)
            .OrderBy(x => x.Version).ToListAsync();
        var fx = await db.ExchangeRateObservations.OrderBy(x => x.ObservedAt).ToListAsync();

        Assert.Equal(new[] { 1, 2 }, profiles.Select(x => x.Version));
        Assert.NotNull(profiles[0].SupersededAt);
        Assert.Null(profiles[1].SupersededAt);
        Assert.Equal(2, fx.Count);
        Assert.NotEqual(fx[0].Rate, fx[1].Rate);
    }

    [Fact]
    public async Task Duration_bands_are_data_and_do_not_imply_linear_pricing()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();

        var shortPlacement = await db.InventoryRateBenchmarks.SingleAsync(
            x => x.DurationSecondsLow == 30);
        var longSegment = await db.InventoryRateBenchmarks.SingleAsync(
            x => x.DurationSecondsLow == 300);

        Assert.Equal(10, longSegment.DurationSecondsLow / shortPlacement.DurationSecondsLow);
        Assert.True(longSegment.RangeLow / shortPlacement.RangeLow < 10);
        Assert.True(longSegment.RangeHigh / shortPlacement.RangeHigh < 10);
    }
}
