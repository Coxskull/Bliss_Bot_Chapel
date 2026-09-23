using Bliss.Api.Contracts;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

/// <summary>
/// Economics reference data, deterministic recommendations, and human-controlled quotes.
/// </summary>
[ApiController]
[Route("api/economics")]
public sealed class EconomicsController : ControllerBase
{
    private readonly BlissDbContext _db;
    private readonly RateRecommendationService _recommendations;
    private readonly QuoteService _quotes;
    private readonly CompensationIllustrationService _compensation;
    private readonly EconomicsResearchService _research;
    private readonly HistoricalEconomicsService _history;
    private readonly OperatorIdentity _operatorIdentity;

    public EconomicsController(
        BlissDbContext db,
        RateRecommendationService recommendations,
        QuoteService quotes,
        CompensationIllustrationService compensation,
        EconomicsResearchService research,
        HistoricalEconomicsService history,
        OperatorIdentity operatorIdentity)
    {
        _db = db;
        _recommendations = recommendations;
        _quotes = quotes;
        _compensation = compensation;
        _research = research;
        _history = history;
        _operatorIdentity = operatorIdentity;
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

    [HttpGet("pricing-rule-versions")]
    public async Task<ActionResult<IReadOnlyList<PricingRuleVersionDto>>> GetPricingRuleVersions(
        CancellationToken cancellationToken) =>
        Ok(await _db.PricingRuleVersions.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PricingRuleVersionDto(
                x.Id, x.Version, x.Name, x.DocumentJson, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken));

    [HttpGet("recommendations")]
    public async Task<ActionResult<IReadOnlyList<RateRecommendationDto>>> GetRecommendations(
        CancellationToken cancellationToken)
    {
        var items = await _recommendations.RecommendationGraph()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => ToDto(x, false)).ToList());
    }

    [HttpGet("recommendations/{id:guid}")]
    public async Task<ActionResult<RateRecommendationDto>> GetRecommendation(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await _recommendations.RecommendationGraph()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToDto(item, false));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("recommendations")]
    public async Task<ActionResult<RateRecommendationDto>> GenerateRecommendation(
        GenerateRateRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        RateRecommendationResult result;
        try
        {
            result = await _recommendations.GenerateAsync(new GenerateRateRecommendationCommand(
                request.CreatorId,
                request.AdInventorySlotId,
                request.GeographicMarketId,
                request.PricingModelCode,
                request.DurationSeconds,
                request.AdvertiserOpportunityId,
                request.BlissMatchId,
                request.IndustryCategory,
                request.CampaignObjective,
                request.SourceSystem,
                request.IdempotencyKey), cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var dto = ToDto(result.Recommendation, result.IsReplay);
        return result.IsReplay
            ? Ok(dto)
            : CreatedAtAction(nameof(GetRecommendation), new { id = dto.Id }, dto);
    }

    [HttpGet("quotes")]
    public async Task<ActionResult<IReadOnlyList<QuoteDto>>> GetQuotes(
        CancellationToken cancellationToken)
    {
        var items = await _quotes.QuoteGraph()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => ToDto(x, Guid.Empty, null, false)).ToList());
    }

    [HttpGet("quotes/{id:guid}")]
    public async Task<ActionResult<QuoteDto>> GetQuote(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await _quotes.QuoteGraph()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null
            ? NotFound()
            : Ok(ToDto(item, Guid.Empty, null, false));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("quotes")]
    public async Task<ActionResult<QuoteDto>> CreateQuote(
        CreateQuoteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _quotes.CreateAsync(new CreateQuoteCommand(
                request.AdvertiserOpportunityId,
                _operatorIdentity.ResolveLabel(User, request.RequestedBy),
                request.RevisionReason,
                (request.LineItems ?? []).Select(ToCommand).ToList(),
                request.SourceSystem,
                request.IdempotencyKey), cancellationToken);
            var dto = ToDto(result.Quote, result.ActionId, result.NewQuoteVersionId, result.IsReplay);
            return result.IsReplay
                ? Ok(dto)
                : CreatedAtAction(nameof(GetQuote), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("quotes/{id:guid}/versions")]
    public async Task<ActionResult<QuoteDto>> ReviseQuote(
        Guid id,
        ReviseQuoteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _quotes.ReviseAsync(new ReviseQuoteCommand(
                id,
                _operatorIdentity.ResolveLabel(User, request.CreatedBy),
                request.RevisionReason,
                (request.LineItems ?? []).Select(ToCommand).ToList(),
                request.SourceSystem,
                request.IdempotencyKey), cancellationToken);
            return Ok(ToDto(
                result.Quote, result.ActionId, result.NewQuoteVersionId, result.IsReplay));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("quotes/{id:guid}/approvals")]
    public async Task<ActionResult<QuoteDto>> DecideQuoteApproval(
        Guid id,
        DecideQuoteApprovalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _quotes.DecideApprovalAsync(new DecideQuoteApprovalCommand(
                id,
                request.QuoteVersionId,
                request.Decision,
                _operatorIdentity.ResolveLabel(User, request.ReviewerLabel),
                request.Rationale,
                request.SourceSystem,
                request.IdempotencyKey), cancellationToken);
            return Ok(ToDto(result.Quote, result.ActionId, null, result.IsReplay));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("quotes/{id:guid}/outcomes")]
    public async Task<ActionResult<QuoteDto>> RecordQuoteOutcome(
        Guid id,
        RecordQuoteOutcomeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _quotes.RecordOutcomeAsync(new RecordQuoteOutcomeCommand(
                id,
                request.QuoteVersionId,
                request.Response,
                _operatorIdentity.ResolveLabel(User, request.ActorLabel),
                request.Rationale,
                request.NegotiatedLineItems?.Select(ToCommand).ToList(),
                request.SourceSystem,
                request.IdempotencyKey), cancellationToken);
            return Ok(ToDto(
                result.Quote, result.ActionId, result.NewQuoteVersionId, result.IsReplay));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("compensation-rule-versions")]
    public async Task<ActionResult<IReadOnlyList<CompensationRuleVersionDto>>>
        GetCompensationRuleVersions(CancellationToken cancellationToken)
    {
        var items = await _db.CompensationRuleVersions.AsNoTracking()
            .Include(x => x.Allocations)
            .OrderByDescending(x => x.EffectiveAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => new CompensationRuleVersionDto(
            x.Id,
            x.Version,
            x.Name,
            x.DocumentJson,
            x.IsActive,
            x.EffectiveAt,
            x.CreatedAt,
            x.Allocations.OrderBy(a => a.SortOrder)
                .Select(a => new CompensationRuleAllocationDto(
                    a.Id, a.ParticipantRole, a.ParticipantLabel,
                    a.Percentage, a.SortOrder))
                .ToList())).ToList());
    }

    [HttpGet("compensation-illustrations")]
    public async Task<ActionResult<IReadOnlyList<CompensationIllustrationDto>>>
        GetCompensationIllustrations(CancellationToken cancellationToken)
    {
        var items = await _compensation.IllustrationGraph()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => ToDto(x, false)).ToList());
    }

    [HttpGet("compensation-illustrations/{id:guid}")]
    public async Task<ActionResult<CompensationIllustrationDto>>
        GetCompensationIllustration(Guid id, CancellationToken cancellationToken)
    {
        var item = await _compensation.IllustrationGraph()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToDto(item, false));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("compensation-illustrations")]
    public async Task<ActionResult<CompensationIllustrationDto>>
        GenerateCompensationIllustration(
            GenerateCompensationIllustrationRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _compensation.GenerateAsync(
                new GenerateCompensationIllustrationCommand(
                    request.QuoteId,
                    request.QuoteVersionId,
                    request.CompensationRuleVersionId,
                    request.SourceSystem,
                    request.IdempotencyKey),
                cancellationToken);
            var dto = ToDto(result.Illustration, result.IsReplay);
            return result.IsReplay
                ? Ok(dto)
                : CreatedAtAction(
                    nameof(GetCompensationIllustration), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("research-runs")]
    public async Task<ActionResult<IReadOnlyList<EconomicsResearchRunDto>>>
        GetResearchRuns(string? status, CancellationToken cancellationToken)
    {
        var query = _research.ResearchGraph();
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.ToUpperInvariant());
        }
        var items = await query.OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => ToDto(x, Guid.Empty, false)).ToList());
    }

    [HttpGet("research-runs/{id:guid}")]
    public async Task<ActionResult<EconomicsResearchRunDto>> GetResearchRun(
        Guid id, CancellationToken cancellationToken)
    {
        var item = await _research.ResearchGraph()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToDto(item, Guid.Empty, false));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("research-runs")]
    public async Task<ActionResult<EconomicsResearchRunDto>> QueueResearchRun(
        QueueEconomicsResearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _research.QueueAsync(new QueueEconomicsResearchCommand(
                request.GeographicMarketId,
                request.Metric,
                request.IndustryCategory,
                request.Platform,
                request.InventorySlotType,
                request.ResearchQuestion,
                _operatorIdentity.ResolveLabel(User, request.RequestedBy),
                request.SourceSystem,
                request.IdempotencyKey), cancellationToken);
            var dto = ToDto(result.Run, result.ActionId, result.IsReplay);
            return result.IsReplay
                ? Ok(dto)
                : CreatedAtAction(nameof(GetResearchRun), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("research-runs/{id:guid}/candidates")]
    public async Task<ActionResult<EconomicsResearchRunDto>> StageResearchCandidate(
        Guid id,
        StageEconomicsResearchCandidateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _research.StageCandidateAsync(
                new StageEconomicsResearchCandidateCommand(
                    id,
                    request.NumericValue,
                    request.RangeLow,
                    request.RangeHigh,
                    request.CurrencyCode,
                    request.SourceName,
                    request.SourceUrl,
                    request.SourceType,
                    request.PublicationDate,
                    request.RetrievedAt,
                    request.ConfidenceLevel,
                    request.VerificationStatus,
                    request.ExtractionModel,
                    request.RawPayloadJson,
                    request.SourceSystem,
                    request.IdempotencyKey),
                cancellationToken);
            return Ok(ToDto(result.Run, result.ActionId, result.IsReplay));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("research-candidates/{id:guid}/review")]
    public async Task<ActionResult<EconomicsResearchRunDto>> ReviewResearchCandidate(
        Guid id,
        ReviewEconomicsResearchCandidateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _research.ReviewCandidateAsync(
                new ReviewEconomicsResearchCandidateCommand(
                    id,
                    request.Decision,
                    _operatorIdentity.ResolveLabel(User, request.ReviewerLabel),
                    request.Rationale,
                    request.SourceSystem,
                    request.IdempotencyKey),
                cancellationToken);
            return Ok(ToDto(result.Run, result.ActionId, result.IsReplay));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("historical-placements")]
    public async Task<ActionResult<IReadOnlyList<HistoricalPlacementEconomicsDto>>>
        GetHistoricalPlacements(CancellationToken cancellationToken)
    {
        var items = await _history.PlacementGraph()
            .OrderByDescending(x => x.RecordedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => ToDto(x, false)).ToList());
    }

    [HttpGet("historical-placements/{id:guid}")]
    public async Task<ActionResult<HistoricalPlacementEconomicsDto>>
        GetHistoricalPlacement(Guid id, CancellationToken cancellationToken)
    {
        var item = await _history.PlacementGraph()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToDto(item, false));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("historical-placements")]
    public async Task<ActionResult<HistoricalPlacementEconomicsDto>>
        RecordHistoricalPlacement(
            RecordHistoricalPlacementEconomicsRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _history.RecordPlacementAsync(
                new RecordHistoricalPlacementEconomicsCommand(
                    request.CampaignPlacementId,
                    request.QuoteOutcomeId,
                    request.QuoteLineItemId,
                    request.CompensationIllustrationId,
                    request.SupersedesHistoricalPlacementEconomicsId,
                    request.ContractedLineAmount,
                    request.ActualImpressions,
                    request.ActualViews,
                    request.ActualListens,
                    request.ActualEngagements,
                    request.ActualConversions,
                    request.MeasurementAsOf,
                    request.Notes,
                    request.SourceSystem,
                    request.IdempotencyKey),
                cancellationToken);
            var dto = ToDto(result.Record, result.IsReplay);
            return result.IsReplay
                ? Ok(dto)
                : CreatedAtAction(nameof(GetHistoricalPlacement), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("campaign-performance")]
    public async Task<ActionResult<IReadOnlyList<CampaignPerformanceEconomicsDto>>>
        GetCampaignPerformance(CancellationToken cancellationToken)
    {
        var items = await _history.CampaignGraph()
            .OrderByDescending(x => x.RecordedAt)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(x => ToDto(x, false)).ToList());
    }

    [HttpGet("campaign-performance/{id:guid}")]
    public async Task<ActionResult<CampaignPerformanceEconomicsDto>>
        GetCampaignPerformanceRecord(Guid id, CancellationToken cancellationToken)
    {
        var item = await _history.CampaignGraph()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? NotFound() : Ok(ToDto(item, false));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("campaign-performance")]
    public async Task<ActionResult<CampaignPerformanceEconomicsDto>>
        RecordCampaignPerformance(
            RecordCampaignPerformanceEconomicsRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            var result = await _history.RecordCampaignAsync(
                new RecordCampaignPerformanceEconomicsCommand(
                    request.CampaignId,
                    request.SupersedesCampaignPerformanceEconomicsId,
                    request.ActualImpressions,
                    request.ActualViews,
                    request.ActualListens,
                    request.ActualEngagements,
                    request.EngagementRate,
                    request.Conversions,
                    request.ConversionValue,
                    request.ConversionValueCurrencyCode,
                    request.MeasurementAsOf,
                    request.Notes,
                    request.SourceSystem,
                    request.IdempotencyKey),
                cancellationToken);
            var dto = ToDto(result.Record, result.IsReplay);
            return result.IsReplay
                ? Ok(dto)
                : CreatedAtAction(
                    nameof(GetCampaignPerformanceRecord), new { id = dto.Id }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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

    private static RateRecommendationDto ToDto(RateRecommendation x, bool isReplay) =>
        new(
            x.Id,
            x.CreatorId,
            x.Creator.Name,
            x.ContentItemId,
            x.AdInventorySlotId,
            x.AdInventorySlot?.SlotType,
            x.GeographicMarketId,
            x.GeographicMarket.MarketCode,
            x.PricingModelId,
            x.PricingModel.Code,
            x.PricingRuleVersionId,
            x.PricingRuleVersion.Version,
            x.IndustryCategory,
            x.CampaignObjective,
            x.DurationSeconds,
            x.CurrencyCode,
            x.RangeLow,
            x.RangeTarget,
            x.RangeHigh,
            x.EstimatedImpressions,
            x.ConfidenceLevel,
            x.BenchmarkAsOf,
            x.InputSnapshotJson,
            x.SourceSystem,
            x.IdempotencyKey,
            x.CreatedAt,
            x.Factors.OrderBy(f => f.SortOrder)
                .Select(f => new RateRecommendationFactorDto(
                    f.FactorCode, f.Label, f.NumericValue, f.AdjustmentMultiplier,
                    f.Rationale, f.SortOrder)).ToList(),
            x.Sources.Select(s => new RateRecommendationSourceDto(
                s.ResearchSourceId, s.ResearchSource?.Name,
                s.InventoryRateBenchmarkId, s.CreatorAudienceSnapshotId,
                s.CreatorPerformanceSnapshotId, s.Role)).ToList(),
            isReplay);

    private static QuoteLineCommand ToCommand(QuoteLineRequest request) =>
        new(
            request.RateRecommendationId,
            request.Description,
            request.Quantity,
            request.UnitAmount);

    private static QuoteDto ToDto(
        Quote quote,
        Guid actionId,
        Guid? newQuoteVersionId,
        bool isReplay) =>
        new(
            quote.Id,
            quote.AdvertiserOpportunityId,
            quote.AdvertiserOpportunity?.Name,
            quote.CurrencyCode,
            quote.Status,
            quote.CurrentVersionNumber,
            quote.RequestedBy,
            quote.CreatedAt,
            quote.UpdatedAt,
            quote.Versions.OrderByDescending(x => x.VersionNumber)
                .Select(version => new QuoteVersionDto(
                    version.Id,
                    version.ParentVersionId,
                    version.VersionNumber,
                    version.CurrencyCode,
                    version.SubtotalAmount,
                    version.TotalAmount,
                    version.RevisionReason,
                    version.CreatedBy,
                    version.CreatedAt,
                    version.LineItems.OrderBy(x => x.SortOrder)
                        .Select(line => new QuoteLineItemDto(
                            line.Id,
                            line.RateRecommendationId,
                            line.Description,
                            line.Quantity,
                            line.UnitAmount,
                            line.LineAmount,
                            line.CurrencyCode,
                            line.RecommendationLow,
                            line.RecommendationTarget,
                            line.RecommendationHigh))
                        .ToList()))
                .ToList(),
            quote.ApprovalDecisions.OrderByDescending(x => x.CreatedAt)
                .Select(x => new QuoteApprovalDecisionDto(
                    x.Id, x.QuoteVersionId, x.Decision, x.ReviewerLabel,
                    x.Rationale, x.CreatedAt))
                .ToList(),
            quote.Outcomes.OrderByDescending(x => x.CreatedAt)
                .Select(x => new QuoteOutcomeDto(
                    x.Id, x.QuoteVersionId, x.NewQuoteVersionId, x.Response,
                    x.Amount, x.CurrencyCode, x.ActorLabel, x.Rationale, x.CreatedAt))
                .ToList(),
            actionId,
            newQuoteVersionId,
            isReplay);

    private static CompensationIllustrationDto ToDto(
        CompensationIllustration illustration,
        bool isReplay) =>
        new(
            illustration.Id,
            illustration.QuoteId,
            illustration.QuoteVersionId,
            illustration.QuoteVersion.VersionNumber,
            illustration.QuoteOutcomeId,
            illustration.CompensationRuleVersionId,
            illustration.CompensationRuleVersion.Version,
            illustration.GrossAmount,
            illustration.CurrencyCode,
            illustration.InputSnapshotJson,
            illustration.SourceSystem,
            illustration.IdempotencyKey,
            illustration.CreatedAt,
            illustration.Lines.OrderBy(x => x.SortOrder)
                .Select(x => new CompensationIllustrationLineDto(
                    x.Id,
                    x.CompensationRuleAllocationId,
                    x.ParticipantRole,
                    x.ParticipantLabel,
                    x.Percentage,
                    x.Amount,
                    x.SortOrder))
                .ToList(),
            isReplay);

    private static EconomicsResearchRunDto ToDto(
        EconomicsResearchRun run,
        Guid actionId,
        bool isReplay) =>
        new(
            run.Id,
            run.GeographicMarketId,
            run.GeographicMarket.MarketCode,
            run.Metric,
            run.IndustryCategory,
            run.Platform,
            run.InventorySlotType,
            run.ResearchQuestion,
            run.Status,
            run.RequestedBy,
            run.SourceSystem,
            run.IdempotencyKey,
            run.CreatedAt,
            run.UpdatedAt,
            run.Candidates.OrderBy(x => x.CreatedAt)
                .Select(candidate => new EconomicsResearchCandidateDto(
                    candidate.Id,
                    candidate.NumericValue,
                    candidate.RangeLow,
                    candidate.RangeHigh,
                    candidate.CurrencyCode,
                    candidate.SourceName,
                    candidate.SourceUrl,
                    candidate.SourceType,
                    candidate.PublicationDate,
                    candidate.RetrievedAt,
                    candidate.ConfidenceLevel,
                    candidate.VerificationStatus,
                    candidate.ExtractionModel,
                    candidate.RawPayloadJson,
                    candidate.Status,
                    candidate.PromotedObservationId,
                    candidate.CreatedAt,
                    candidate.ReviewDecisions.OrderBy(x => x.CreatedAt)
                        .Select(review => new EconomicsResearchReviewDecisionDto(
                            review.Id,
                            review.MarketBenchmarkObservationId,
                            review.Decision,
                            review.ReviewerLabel,
                            review.Rationale,
                            review.CreatedAt))
                        .ToList()))
                .ToList(),
            actionId,
            isReplay);

    private static HistoricalPlacementEconomicsDto ToDto(
        HistoricalPlacementEconomics x,
        bool isReplay) =>
        new(
            x.Id,
            x.CampaignPlacementId,
            x.CampaignPlacement.CampaignId,
            x.CampaignPlacement.Campaign.Name,
            x.QuoteVersionId,
            x.QuoteOutcomeId,
            x.QuoteLineItemId,
            x.RateRecommendationId,
            x.CompensationIllustrationId,
            x.SupersedesHistoricalPlacementEconomicsId,
            x.CampaignPlacement.AdInventorySlot.SlotType,
            x.RateRecommendation.PricingModel.Code,
            x.QuotedAmount,
            x.ContractedAmount,
            x.CurrencyCode,
            x.ActualImpressions,
            x.ActualViews,
            x.ActualListens,
            x.ActualEngagements,
            x.ActualConversions,
            x.RecommendationLow,
            x.RecommendationTarget,
            x.RecommendationHigh,
            x.ExternalBenchmarkLow,
            x.ExternalBenchmarkHigh,
            x.EffectiveCpm,
            x.EffectiveCpv,
            x.ContractedVsRecommendationTargetPercentage,
            x.ContractedVsExternalMidpointPercentage,
            x.AlphaCompensationAmount,
            x.CreatorCompensationAmount,
            x.OtherCompensationAmount,
            x.MeasurementAsOf,
            x.Notes,
            x.InputSnapshotJson,
            x.SourceSystem,
            x.IdempotencyKey,
            x.RecordedAt,
            isReplay);

    private static CampaignPerformanceEconomicsDto ToDto(
        CampaignPerformanceEconomics x,
        bool isReplay) =>
        new(
            x.Id,
            x.CampaignId,
            x.Campaign.Name,
            x.SupersedesCampaignPerformanceEconomicsId,
            x.ActualImpressions,
            x.ActualViews,
            x.ActualListens,
            x.ActualEngagements,
            x.EngagementRate,
            x.Conversions,
            x.ConversionValue,
            x.ConversionValueCurrencyCode,
            x.AlphaPlacementCount,
            x.AlphaContractedAmount,
            x.AlphaContractedCurrencyCode,
            x.EffectiveCpm,
            x.EffectiveCpv,
            x.MeasurementAsOf,
            x.Notes,
            x.InputSnapshotJson,
            x.SourceSystem,
            x.IdempotencyKey,
            x.RecordedAt,
            isReplay);
}
