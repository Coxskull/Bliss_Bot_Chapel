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
}
