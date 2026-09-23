using Bliss.Domain.Common;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

/// <summary>Fictional Phase 3 context fixtures. None are production rates or FX.</summary>
public sealed class EconomicsPhase3DataSeeder
{
    private readonly BlissDbContext _db;

    public EconomicsPhase3DataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var cpm = await _db.PricingModels.SingleOrDefaultAsync(
            x => x.Code == PricingModelCodes.Cpm, cancellationToken);
        if (cpm is null
            || !await _db.GeographicMarkets.AnyAsync(x => x.Id == EconomicsDataSeeder.ManilaId, cancellationToken)
            || !await _db.ResearchSources.AnyAsync(x => x.Id == EconomicsDataSeeder.TestSourceId, cancellationToken))
        {
            return;
        }

        var createdAt = new DateTime(2026, 9, 23, 2, 0, 0, DateTimeKind.Utc);
        var marketProfiles = new[]
        {
            new MarketEconomicProfile
            {
                Id = Guid.Parse("ec000007-0000-0000-0000-000000000001"),
                GeographicMarketId = EconomicsDataSeeder.ManilaId,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                Version = 1,
                PurchasingPowerIndex = 91m,
                CompetitionLevel = "MEDIUM",
                AudienceScarcityLevel = "MEDIUM",
                Notes = "Synthetic TEST profile; not market truth.",
                ConfidenceLevel = EconomicsConfidenceLevels.Low,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                EffectiveAt = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                SupersededAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = createdAt
            },
            new MarketEconomicProfile
            {
                Id = Guid.Parse("ec000007-0000-0000-0000-000000000002"),
                GeographicMarketId = EconomicsDataSeeder.ManilaId,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                Version = 2,
                PurchasingPowerIndex = 94m,
                CompetitionLevel = "HIGH",
                AudienceScarcityLevel = "MEDIUM",
                Notes = "Synthetic TEST profile; not market truth.",
                ConfidenceLevel = EconomicsConfidenceLevels.Medium,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                EffectiveAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = createdAt
            }
        };
        await AddMissingAsync(_db.MarketEconomicProfiles, marketProfiles, cancellationToken);

        var industryProfiles = new[]
        {
            new IndustryEconomicProfile
            {
                Id = Guid.Parse("ec000008-0000-0000-0000-000000000001"),
                GeographicMarketId = EconomicsDataSeeder.ManilaId,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                Category = "WOMENS_FOOTWEAR",
                Version = 1,
                AcquisitionCostLow = 480m,
                AcquisitionCostHigh = 900m,
                CurrencyCode = "PHP",
                Notes = "Synthetic TEST acquisition-cost range; not an advertiser quote.",
                ConfidenceLevel = EconomicsConfidenceLevels.Low,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                EffectiveAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = createdAt
            }
        };
        await AddMissingAsync(_db.IndustryEconomicProfiles, industryProfiles, cancellationToken);

        var benchmarks = new[]
        {
            new InventoryRateBenchmark
            {
                Id = Guid.Parse("ec000009-0000-0000-0000-000000000001"),
                GeographicMarketId = EconomicsDataSeeder.ManilaId,
                PricingModelId = cpm.Id,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                InventorySlotType = InventorySlotTypes.MidRoll,
                Platform = "YOUTUBE",
                ContentFormat = "VIDEO",
                DurationSecondsLow = 30,
                DurationSecondsHigh = 60,
                RangeLow = 180m,
                RangeHigh = 260m,
                CurrencyCode = "PHP",
                ConfidenceLevel = EconomicsConfidenceLevels.Low,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                EffectiveAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = createdAt
            },
            new InventoryRateBenchmark
            {
                Id = Guid.Parse("ec000009-0000-0000-0000-000000000002"),
                GeographicMarketId = EconomicsDataSeeder.ManilaId,
                PricingModelId = cpm.Id,
                ResearchSourceId = EconomicsDataSeeder.TestSourceId,
                InventorySlotType = InventorySlotTypes.SponsoredSegment,
                Platform = "YOUTUBE",
                ContentFormat = "VIDEO",
                DurationSecondsLow = 300,
                DurationSecondsHigh = 600,
                RangeLow = 420m,
                RangeHigh = 680m,
                CurrencyCode = "PHP",
                ConfidenceLevel = EconomicsConfidenceLevels.Low,
                VerificationStatus = ObservationVerificationStatuses.Estimated,
                EffectiveAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = createdAt
            }
        };
        await AddMissingAsync(_db.InventoryRateBenchmarks, benchmarks, cancellationToken);

        var exchangeRates = new[]
        {
            Exchange(Guid.Parse("ec000010-0000-0000-0000-000000000001"), 58.20m,
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc), createdAt),
            Exchange(Guid.Parse("ec000010-0000-0000-0000-000000000002"), 58.50m,
                new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), createdAt)
        };
        await AddMissingAsync(_db.ExchangeRateObservations, exchangeRates, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static ExchangeRateObservation Exchange(Guid id, decimal rate, DateTime observedAt, DateTime createdAt) =>
        new()
        {
            Id = id,
            ResearchSourceId = EconomicsDataSeeder.TestSourceId,
            BaseCurrencyCode = "USD",
            QuoteCurrencyCode = "PHP",
            Rate = rate,
            ObservedAt = observedAt,
            RetrievedAt = createdAt,
            ConfidenceLevel = EconomicsConfidenceLevels.Low,
            VerificationStatus = ObservationVerificationStatuses.Estimated,
            Notes = "Synthetic TEST FX observation; not suitable for settlement.",
            CreatedAt = createdAt
        };

    private async Task AddMissingAsync<T>(
        DbSet<T> set, IEnumerable<T> rows, CancellationToken cancellationToken) where T : class
    {
        foreach (var row in rows)
        {
            var id = (Guid)typeof(T).GetProperty("Id")!.GetValue(row)!;
            if (!await set.AnyAsync(x => EF.Property<Guid>(x, "Id") == id, cancellationToken))
            {
                set.Add(row);
            }
        }
    }
}
