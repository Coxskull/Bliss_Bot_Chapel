using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase2PersistenceTests
{
    [Fact]
    public async Task Creator_retains_versioned_audience_and_performance_history()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();

        Assert.Equal(2, await db.CreatorAudienceSnapshots
            .CountAsync(x => x.CreatorId == Phase1DataSeeder.CreatorId));
        Assert.Equal(2, await db.CreatorPerformanceSnapshots
            .CountAsync(x => x.CreatorId == Phase1DataSeeder.CreatorId));
        Assert.True(await db.CreatorPerformanceSnapshots.AnyAsync(x => x.ContentItemId == null));
        Assert.True(await db.CreatorPerformanceSnapshots.AnyAsync(
            x => x.ContentItemId == Phase1DataSeeder.ContentItem1Id));
    }

    [Fact]
    public async Task Unknown_measurements_remain_null_when_new_snapshot_is_added()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();

        var latest = await db.CreatorAudienceSnapshots.SingleAsync(
            x => x.Id == EconomicsPhase2DataSeeder.AudienceSnapshot2Id);
        var creatorWide = await db.CreatorPerformanceSnapshots.SingleAsync(
            x => x.Id == EconomicsPhase2DataSeeder.PerformanceSnapshot1Id);

        Assert.Null(latest.MalePercentage);
        Assert.Null(creatorWide.RetentionRate);

        db.CreatorAudienceSnapshots.Add(new CreatorAudienceSnapshot
        {
            Id = Guid.NewGuid(),
            CreatorId = Phase1DataSeeder.CreatorId,
            CapturedAt = DateTime.UtcNow,
            ConfidenceLevel = "UNKNOWN",
            VerificationStatus = "UNKNOWN",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        Assert.Equal(3, await db.CreatorAudienceSnapshots.CountAsync());
        Assert.Null((await db.CreatorAudienceSnapshots.OrderByDescending(x => x.CapturedAt)
            .FirstAsync()).Subscribers);
    }
}
