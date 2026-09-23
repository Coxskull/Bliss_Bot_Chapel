using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

/// <summary>
/// Fictional Phase 1 economics fixtures. These rows prove structure and
/// provenance; they are not production Alpha benchmarks or recommended rates.
/// </summary>
public sealed class EconomicsDataSeeder
{
    public static readonly Guid ManilaId = Guid.Parse("ec000001-0000-0000-0000-000000000001");
    public static readonly Guid MedellinId = Guid.Parse("ec000001-0000-0000-0000-000000000002");
    public static readonly Guid PanamaCityId = Guid.Parse("ec000001-0000-0000-0000-000000000003");
    public static readonly Guid SantoDomingoId = Guid.Parse("ec000001-0000-0000-0000-000000000004");
    public static readonly Guid KualaLumpurId = Guid.Parse("ec000001-0000-0000-0000-000000000005");
    public static readonly Guid JakartaId = Guid.Parse("ec000001-0000-0000-0000-000000000006");
    public static readonly Guid TestSourceId = Guid.Parse("ec000002-0000-0000-0000-000000000001");

    private readonly BlissDbContext _db;

    public EconomicsDataSeeder(BlissDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var createdAt = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        var markets = new[]
        {
            Market(ManilaId, "PH", "Manila", "Metro Manila", "PH-MNL", "PHP", createdAt),
            Market(MedellinId, "CO", "Medellín", "Valle de Aburrá", "CO-MDE", "COP", createdAt),
            Market(PanamaCityId, "PA", "Panama City", "Panama Metro", "PA-PTY", "PAB", createdAt),
            Market(SantoDomingoId, "DO", "Santo Domingo", "Greater Santo Domingo", "DO-SDQ", "DOP", createdAt),
            Market(KualaLumpurId, "MY", "Kuala Lumpur", "Klang Valley", "MY-KUL", "MYR", createdAt),
            Market(JakartaId, "ID", "Jakarta", "Jabodetabek", "ID-JKT", "IDR", createdAt)
        };
        foreach (var market in markets)
        {
            if (!await _db.GeographicMarkets.AnyAsync(x => x.Id == market.Id, cancellationToken))
            {
                _db.GeographicMarkets.Add(market);
            }
        }

        var models = new[]
        {
            Model(1, PricingModelCodes.Cpm, "Cost per 1,000 impressions", "Exposure-priced inventory."),
            Model(2, PricingModelCodes.Cpv, "Cost per view", "Verified-view pricing where applicable."),
            Model(3, PricingModelCodes.FlatPlacement, "Flat placement", "Fixed amount for one placement."),
            Model(4, PricingModelCodes.FixedCampaign, "Fixed campaign", "Fixed amount for a campaign scope."),
            Model(5, PricingModelCodes.Sponsorship, "Sponsorship", "Sponsorship package pricing."),
            Model(6, PricingModelCodes.HostRead, "Host-read / integrated", "Creator-integrated message or segment."),
            Model(7, PricingModelCodes.Cpa, "Cost per acquisition", "Performance-based acquisition pricing."),
            Model(8, PricingModelCodes.Cpl, "Cost per lead", "Performance-based qualified-lead pricing."),
            Model(9, PricingModelCodes.Cps, "Cost per sale", "Performance-based sale pricing."),
            Model(10, PricingModelCodes.Hybrid, "Hybrid", "A versioned combination of pricing bases.")
        };
        foreach (var model in models)
        {
            if (!await _db.PricingModels.AnyAsync(x => x.Id == model.Id, cancellationToken))
            {
                _db.PricingModels.Add(model);
            }
        }

        if (!await _db.ResearchSources.AnyAsync(x => x.Id == TestSourceId, cancellationToken))
        {
            _db.ResearchSources.Add(new ResearchSource
            {
                Id = TestSourceId,
                Name = "TEST Synthetic Market Fixture",
                SourceUrl = "https://example.test/bliss-economics-phase-1",
                SourceType = "TEST_FIXTURE",
                IsApproved = false,
                CreatedAt = createdAt
            });
        }

        var observations = markets.Select((market, index) => new MarketBenchmarkObservation
        {
            Id = Guid.Parse($"ec000003-0000-0000-0000-{index + 1:000000000000}"),
            ResearchSourceId = TestSourceId,
            GeographicMarketId = market.Id,
            Metric = "TEST_DIGITAL_AD_COST_INDEX",
            NumericValue = 100 + (index * 7),
            PublicationDate = new DateOnly(2026, 9, 1),
            RetrievedAt = createdAt,
            ConfidenceLevel = EconomicsConfidenceLevels.Low,
            VerificationStatus = ObservationVerificationStatuses.Estimated,
            Notes = "Synthetic non-production fixture. Not an Alpha rate, quote, CPM, or recommendation.",
            CreatedAt = createdAt
        });
        foreach (var observation in observations)
        {
            if (!await _db.MarketBenchmarkObservations.AnyAsync(x => x.Id == observation.Id, cancellationToken))
            {
                _db.MarketBenchmarkObservations.Add(observation);
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static GeographicMarket Market(
        Guid id, string country, string city, string metro, string code, string currency, DateTime createdAt) =>
        new()
        {
            Id = id,
            CountryCode = country,
            CityName = city,
            MetroName = metro,
            MarketCode = code,
            CurrencyCode = currency,
            CreatedAt = createdAt
        };

    private static PricingModel Model(int suffix, string code, string name, string description) =>
        new()
        {
            Id = Guid.Parse($"ec000004-0000-0000-0000-{suffix:000000000000}"),
            Code = code,
            Name = name,
            Description = description
        };
}
