using Bliss.Api.Contracts;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

/// <summary>
/// Read-only Economics Phase 1 reference data. This controller does not calculate rates.
/// </summary>
[ApiController]
[Route("api/economics")]
public sealed class EconomicsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public EconomicsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet("markets")]
    public async Task<ActionResult<IReadOnlyList<GeographicMarketDto>>> GetMarkets(
        CancellationToken cancellationToken) =>
        Ok(await _db.GeographicMarkets.AsNoTracking()
            .OrderBy(x => x.CountryCode).ThenBy(x => x.CityName)
            .Select(x => new GeographicMarketDto(
                x.Id, x.CountryCode, x.CityName, x.MetroName, x.MarketCode,
                x.CurrencyCode, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken));

    [HttpGet("markets/{id:guid}")]
    public async Task<ActionResult<GeographicMarketDto>> GetMarket(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.GeographicMarkets.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new GeographicMarketDto(
                x.Id, x.CountryCode, x.CityName, x.MetroName, x.MarketCode,
                x.CurrencyCode, x.IsActive, x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("pricing-models")]
    public async Task<ActionResult<IReadOnlyList<PricingModelDto>>> GetPricingModels(
        CancellationToken cancellationToken) =>
        Ok(await _db.PricingModels.AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new PricingModelDto(x.Id, x.Code, x.Name, x.Description, x.IsActive))
            .ToListAsync(cancellationToken));

    [HttpGet("pricing-models/{id:guid}")]
    public async Task<ActionResult<PricingModelDto>> GetPricingModel(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.PricingModels.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PricingModelDto(x.Id, x.Code, x.Name, x.Description, x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("research-sources")]
    public async Task<ActionResult<IReadOnlyList<ResearchSourceDto>>> GetResearchSources(
        CancellationToken cancellationToken) =>
        Ok(await _db.ResearchSources.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ResearchSourceDto(
                x.Id, x.Name, x.SourceUrl, x.SourceType, x.IsApproved, x.CreatedAt))
            .ToListAsync(cancellationToken));

    [HttpGet("research-sources/{id:guid}")]
    public async Task<ActionResult<ResearchSourceDto>> GetResearchSource(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.ResearchSources.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ResearchSourceDto(
                x.Id, x.Name, x.SourceUrl, x.SourceType, x.IsApproved, x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("observations")]
    public async Task<ActionResult<IReadOnlyList<MarketBenchmarkObservationDto>>> GetObservations(
        Guid? marketId,
        string? metric,
        CancellationToken cancellationToken)
    {
        var query = _db.MarketBenchmarkObservations.AsNoTracking().AsQueryable();
        if (marketId.HasValue)
        {
            query = query.Where(x => x.GeographicMarketId == marketId);
        }
        if (!string.IsNullOrWhiteSpace(metric))
        {
            query = query.Where(x => x.Metric == metric);
        }

        return Ok(await ProjectObservations(query.OrderByDescending(x => x.RetrievedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("observations/{id:guid}")]
    public async Task<ActionResult<MarketBenchmarkObservationDto>> GetObservation(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await ProjectObservations(
                _db.MarketBenchmarkObservations.AsNoTracking().Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("audience-snapshots")]
    public async Task<ActionResult<IReadOnlyList<CreatorAudienceSnapshotDto>>> GetAudienceSnapshots(
        Guid? creatorId, CancellationToken cancellationToken)
    {
        var query = _db.CreatorAudienceSnapshots.AsNoTracking().AsQueryable();
        if (creatorId.HasValue)
        {
            query = query.Where(x => x.CreatorId == creatorId);
        }

        return Ok(await ProjectAudienceSnapshots(
                query.OrderByDescending(x => x.CapturedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("audience-snapshots/{id:guid}")]
    public async Task<ActionResult<CreatorAudienceSnapshotDto>> GetAudienceSnapshot(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await ProjectAudienceSnapshots(
                _db.CreatorAudienceSnapshots.AsNoTracking().Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("performance-snapshots")]
    public async Task<ActionResult<IReadOnlyList<CreatorPerformanceSnapshotDto>>> GetPerformanceSnapshots(
        Guid? creatorId, CancellationToken cancellationToken)
    {
        var query = _db.CreatorPerformanceSnapshots.AsNoTracking().AsQueryable();
        if (creatorId.HasValue)
        {
            query = query.Where(x => x.CreatorId == creatorId);
        }

        return Ok(await ProjectPerformanceSnapshots(
                query.OrderByDescending(x => x.CapturedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("performance-snapshots/{id:guid}")]
    public async Task<ActionResult<CreatorPerformanceSnapshotDto>> GetPerformanceSnapshot(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await ProjectPerformanceSnapshots(
                _db.CreatorPerformanceSnapshots.AsNoTracking().Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("market-profiles")]
    public async Task<ActionResult<IReadOnlyList<MarketEconomicProfileDto>>> GetMarketProfiles(
        Guid? marketId, CancellationToken cancellationToken)
    {
        var query = _db.MarketEconomicProfiles.AsNoTracking().AsQueryable();
        if (marketId.HasValue) query = query.Where(x => x.GeographicMarketId == marketId);
        return Ok(await ProjectMarketProfiles(query
                .OrderBy(x => x.GeographicMarket.MarketCode).ThenByDescending(x => x.Version))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("market-profiles/{id:guid}")]
    public async Task<ActionResult<MarketEconomicProfileDto>> GetMarketProfile(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await ProjectMarketProfiles(_db.MarketEconomicProfiles.AsNoTracking()
                .Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("industry-profiles")]
    public async Task<ActionResult<IReadOnlyList<IndustryEconomicProfileDto>>> GetIndustryProfiles(
        Guid? marketId, string? category, CancellationToken cancellationToken)
    {
        var query = _db.IndustryEconomicProfiles.AsNoTracking().AsQueryable();
        if (marketId.HasValue) query = query.Where(x => x.GeographicMarketId == marketId);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        return Ok(await ProjectIndustryProfiles(query
                .OrderBy(x => x.Category).ThenByDescending(x => x.Version))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("industry-profiles/{id:guid}")]
    public async Task<ActionResult<IndustryEconomicProfileDto>> GetIndustryProfile(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await ProjectIndustryProfiles(_db.IndustryEconomicProfiles.AsNoTracking()
                .Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("inventory-benchmarks")]
    public async Task<ActionResult<IReadOnlyList<InventoryRateBenchmarkDto>>> GetInventoryBenchmarks(
        Guid? marketId, CancellationToken cancellationToken)
    {
        var query = _db.InventoryRateBenchmarks.AsNoTracking().AsQueryable();
        if (marketId.HasValue) query = query.Where(x => x.GeographicMarketId == marketId);
        return Ok(await ProjectInventoryBenchmarks(query.OrderByDescending(x => x.EffectiveAt))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("inventory-benchmarks/{id:guid}")]
    public async Task<ActionResult<InventoryRateBenchmarkDto>> GetInventoryBenchmark(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await ProjectInventoryBenchmarks(_db.InventoryRateBenchmarks.AsNoTracking()
                .Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("exchange-rates")]
    public async Task<ActionResult<IReadOnlyList<ExchangeRateObservationDto>>> GetExchangeRates(
        string? baseCurrency, string? quoteCurrency, CancellationToken cancellationToken)
    {
        var query = _db.ExchangeRateObservations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(baseCurrency))
            query = query.Where(x => x.BaseCurrencyCode == baseCurrency);
        if (!string.IsNullOrWhiteSpace(quoteCurrency))
            query = query.Where(x => x.QuoteCurrencyCode == quoteCurrency);
        return Ok(await ProjectExchangeRates(query.OrderByDescending(x => x.ObservedAt))
            .ToListAsync(cancellationToken));
    }

    [HttpGet("exchange-rates/{id:guid}")]
    public async Task<ActionResult<ExchangeRateObservationDto>> GetExchangeRate(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await ProjectExchangeRates(_db.ExchangeRateObservations.AsNoTracking()
                .Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    private static IQueryable<MarketBenchmarkObservationDto> ProjectObservations(
        IQueryable<MarketBenchmarkObservation> query) =>
        query.Select(x => new MarketBenchmarkObservationDto(
            x.Id,
            x.ResearchSourceId,
            x.ResearchSource == null ? null : x.ResearchSource.Name,
            x.ResearchSource == null ? null : x.ResearchSource.SourceUrl,
            x.GeographicMarketId,
            x.GeographicMarket == null ? null : x.GeographicMarket.MarketCode,
            x.IndustryCategory,
            x.Platform,
            x.InventorySlotType,
            x.Metric,
            x.NumericValue,
            x.RangeLow,
            x.RangeHigh,
            x.CurrencyCode,
            x.PublicationDate,
            x.RetrievedAt,
            x.ConfidenceLevel,
            x.VerificationStatus,
            x.Notes,
            x.CreatedAt));

    private static IQueryable<CreatorAudienceSnapshotDto> ProjectAudienceSnapshots(
        IQueryable<CreatorAudienceSnapshot> query) =>
        query.Select(x => new CreatorAudienceSnapshotDto(
            x.Id,
            x.CreatorId,
            x.Creator.Name,
            x.GeographicMarketId,
            x.GeographicMarket == null ? null : x.GeographicMarket.MarketCode,
            x.ResearchSourceId,
            x.ResearchSource == null ? null : x.ResearchSource.Name,
            x.CapturedAt,
            x.Subscribers,
            x.FemalePercentage,
            x.MalePercentage,
            x.PrimaryAgeRange,
            x.PrimaryGeography,
            x.Language,
            x.ConfidenceLevel,
            x.VerificationStatus,
            x.CreatedAt));

    private static IQueryable<CreatorPerformanceSnapshotDto> ProjectPerformanceSnapshots(
        IQueryable<CreatorPerformanceSnapshot> query) =>
        query.Select(x => new CreatorPerformanceSnapshotDto(
            x.Id,
            x.CreatorId,
            x.Creator.Name,
            x.ContentItemId,
            x.ContentItem == null ? null : x.ContentItem.Title,
            x.ResearchSourceId,
            x.ResearchSource == null ? null : x.ResearchSource.Name,
            x.CapturedAt,
            x.AverageViews,
            x.DailyViews,
            x.WeeklyViews,
            x.MonthlyViews,
            x.HistoricalReach,
            x.EngagementRate,
            x.RetentionRate,
            x.PublishingFrequencyPerWeek,
            x.Platform,
            x.ContentFormat,
            x.ConfidenceLevel,
            x.VerificationStatus,
            x.CreatedAt));

    private static IQueryable<MarketEconomicProfileDto> ProjectMarketProfiles(
        IQueryable<MarketEconomicProfile> query) =>
        query.Select(x => new MarketEconomicProfileDto(
            x.Id, x.GeographicMarketId, x.GeographicMarket.MarketCode,
            x.ResearchSourceId, x.ResearchSource == null ? null : x.ResearchSource.Name,
            x.Version, x.PurchasingPowerIndex, x.CompetitionLevel, x.AudienceScarcityLevel,
            x.Notes, x.ConfidenceLevel, x.VerificationStatus, x.EffectiveAt,
            x.SupersededAt, x.CreatedAt));

    private static IQueryable<IndustryEconomicProfileDto> ProjectIndustryProfiles(
        IQueryable<IndustryEconomicProfile> query) =>
        query.Select(x => new IndustryEconomicProfileDto(
            x.Id, x.GeographicMarketId,
            x.GeographicMarket == null ? null : x.GeographicMarket.MarketCode,
            x.ResearchSourceId, x.ResearchSource == null ? null : x.ResearchSource.Name,
            x.Category, x.Version, x.AcquisitionCostLow, x.AcquisitionCostHigh,
            x.CurrencyCode, x.Notes, x.ConfidenceLevel, x.VerificationStatus,
            x.EffectiveAt, x.SupersededAt, x.CreatedAt));

    private static IQueryable<InventoryRateBenchmarkDto> ProjectInventoryBenchmarks(
        IQueryable<InventoryRateBenchmark> query) =>
        query.Select(x => new InventoryRateBenchmarkDto(
            x.Id, x.GeographicMarketId,
            x.GeographicMarket == null ? null : x.GeographicMarket.MarketCode,
            x.PricingModelId, x.PricingModel.Code,
            x.ResearchSourceId, x.ResearchSource == null ? null : x.ResearchSource.Name,
            x.InventorySlotType, x.Platform, x.ContentFormat,
            x.DurationSecondsLow, x.DurationSecondsHigh, x.RangeLow, x.RangeHigh,
            x.CurrencyCode, x.ConfidenceLevel, x.VerificationStatus,
            x.EffectiveAt, x.SupersededAt, x.CreatedAt));

    private static IQueryable<ExchangeRateObservationDto> ProjectExchangeRates(
        IQueryable<ExchangeRateObservation> query) =>
        query.Select(x => new ExchangeRateObservationDto(
            x.Id, x.ResearchSourceId,
            x.ResearchSource == null ? null : x.ResearchSource.Name,
            x.BaseCurrencyCode, x.QuoteCurrencyCode, x.Rate,
            x.ObservedAt, x.RetrievedAt, x.ConfidenceLevel, x.VerificationStatus,
            x.Notes, x.CreatedAt));
}
