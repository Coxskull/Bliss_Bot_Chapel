using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Fictional Phase 2 creator metrics. Inputs only; never rates.
/// </summary>
public sealed class EconomicsPhase2DataSeeder
{
    public static readonly Guid AudienceSnapshot1Id =
        Guid.Parse("ec000005-0000-0000-0000-000000000001");
    public static readonly Guid AudienceSnapshot2Id =
        Guid.Parse("ec000005-0000-0000-0000-000000000002");
    public static readonly Guid PerformanceSnapshot1Id =
        Guid.Parse("ec000006-0000-0000-0000-000000000001");
    public static readonly Guid PerformanceSnapshot2Id =
        Guid.Parse("ec000006-0000-0000-0000-000000000002");

    private readonly BlissDbContext _db;

    public EconomicsPhase2DataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!await _db.Creators.AnyAsync(x => x.Id == Phase1DataSeeder.CreatorId, cancellationToken)
            || !await _db.ResearchSources.AnyAsync(x => x.Id == EconomicsDataSeeder.TestSourceId, cancellationToken))
        {
            return;
        }

        var audienceSnapshots = new[]
        {
            new CreatorAudienceSnapshot
            {
                Id = AudienceSnapshot1Id,
                CreatorId = Phase1DataSeeder.CreatorId,
                GeographicMarketId = EconomicsDataSeeder.ManilaId,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                CapturedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                Subscribers = 38_500,
                FemalePercentage = 64m,
                MalePercentage = 34m,
                PrimaryAgeRange = "18-34",
                PrimaryGeography = "Metro Manila",
                Language = "English / Tagalog",
                ConfidenceLevel = EconomicsConfidenceLevels.Low,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                CreatedAt = new DateTime(2026, 9, 23, 1, 0, 0, DateTimeKind.Utc)
            },
            new CreatorAudienceSnapshot
            {
                Id = AudienceSnapshot2Id,
                CreatorId = Phase1DataSeeder.CreatorId,
                GeographicMarketId = EconomicsDataSeeder.ManilaId,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                CapturedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                Subscribers = 42_000,
                FemalePercentage = 68m,
                MalePercentage = null,
                PrimaryAgeRange = "18-34",
                PrimaryGeography = "Metro Manila",
                Language = "English / Tagalog",
                ConfidenceLevel = EconomicsConfidenceLevels.Medium,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                CreatedAt = new DateTime(2026, 9, 23, 1, 0, 0, DateTimeKind.Utc)
            }
        };

        foreach (var snapshot in audienceSnapshots)
        {
            if (!await _db.CreatorAudienceSnapshots.AnyAsync(x => x.Id == snapshot.Id, cancellationToken))
            {
                _db.CreatorAudienceSnapshots.Add(snapshot);
            }
        }

        var performanceSnapshots = new[]
        {
            new CreatorPerformanceSnapshot
            {
                Id = PerformanceSnapshot1Id,
                CreatorId = Phase1DataSeeder.CreatorId,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                CapturedAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                AverageViews = 31_000,
                DailyViews = 4_800,
                WeeklyViews = 33_600,
                MonthlyViews = 144_000,
                HistoricalReach = 820_000,
                EngagementRate = 0.061m,
                RetentionRate = null,
                PublishingFrequencyPerWeek = 3m,
                Platform = "YOUTUBE",
                ContentFormat = "VIDEO",
                ConfidenceLevel = EconomicsConfidenceLevels.Low,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                CreatedAt = new DateTime(2026, 9, 23, 1, 0, 0, DateTimeKind.Utc)
            },
            new CreatorPerformanceSnapshot
            {
                Id = PerformanceSnapshot2Id,
                CreatorId = Phase1DataSeeder.CreatorId,
                ContentItemId = Phase1DataSeeder.ContentItem1Id,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                CapturedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                AverageViews = 42_000,
                DailyViews = 6_200,
                WeeklyViews = 43_400,
                MonthlyViews = 186_000,
                HistoricalReach = 1_020_000,
                EngagementRate = 0.074m,
                RetentionRate = 0.58m,
                PublishingFrequencyPerWeek = 3m,
                Platform = "YOUTUBE",
                ContentFormat = "VIDEO",
                ConfidenceLevel = EconomicsConfidenceLevels.Medium,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                CreatedAt = new DateTime(2026, 9, 23, 1, 0, 0, DateTimeKind.Utc)
            }
        };

        foreach (var snapshot in performanceSnapshots)
        {
            if (!await _db.CreatorPerformanceSnapshots.AnyAsync(x => x.Id == snapshot.Id, cancellationToken))
            {
                _db.CreatorPerformanceSnapshots.Add(snapshot);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
