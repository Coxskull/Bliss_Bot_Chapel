using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record GenerateRateRecommendationCommand(
    Guid CreatorId,
    Guid AdInventorySlotId,
    Guid GeographicMarketId,
    string PricingModelCode,
    int? DurationSeconds,
    Guid? AdvertiserOpportunityId,
    Guid? BlissMatchId,
    string? IndustryCategory,
    string? CampaignObjective,
    string SourceSystem,
    string IdempotencyKey);

public sealed record RateRecommendationResult(RateRecommendation Recommendation, bool IsReplay);

public sealed class RateRecommendationService
{
    private readonly BlissDbContext _db;

    public RateRecommendationService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<RateRecommendationResult> GenerateAsync(
        GenerateRateRecommendationCommand command,
        CancellationToken cancellationToken = default)
    {
        var sourceSystem = Required(command.SourceSystem, "SourceSystem", 64);
        var idempotencyKey = Required(command.IdempotencyKey, "IdempotencyKey", 128);
        var replay = await RecommendationGraph()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == sourceSystem && x.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (replay is not null) return new(replay, true);

        var creator = await _db.Creators.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == command.CreatorId, cancellationToken)
            ?? throw new InvalidOperationException("CreatorId does not reference a creator.");
        var slot = await _db.AdInventorySlots.AsNoTracking()
            .Include(x => x.ContentItem)
            .SingleOrDefaultAsync(x => x.Id == command.AdInventorySlotId, cancellationToken)
            ?? throw new InvalidOperationException("AdInventorySlotId does not reference inventory.");
        if (slot.ContentItem.CreatorId != creator.Id)
            throw new InvalidOperationException("Inventory does not belong to the selected creator.");

        var market = await _db.GeographicMarkets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == command.GeographicMarketId, cancellationToken)
            ?? throw new InvalidOperationException("GeographicMarketId does not reference a market.");
        var pricingModel = await _db.PricingModels.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == command.PricingModelCode, cancellationToken)
            ?? throw new InvalidOperationException("PricingModelCode is unknown.");
        var ruleVersion = await _db.PricingRuleVersions.AsNoTracking()
            .Where(x => x.IsActive).OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No active PricingRuleVersion exists.");
        var rule = JsonSerializer.Deserialize<PricingRuleDocument>(
            ruleVersion.DocumentJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("PricingRuleVersion document is invalid.");

        var duration = command.DurationSeconds ?? slot.DurationSeconds;
        var calculationAt = DateTime.UtcNow;
        var benchmark = await _db.InventoryRateBenchmarks.AsNoTracking()
            .Include(x => x.ResearchSource)
            .Where(x => x.GeographicMarketId == market.Id
                && x.PricingModelId == pricingModel.Id
                && x.InventorySlotType == slot.SlotType
                && x.EffectiveAt <= calculationAt
                && (!x.SupersededAt.HasValue || x.SupersededAt > calculationAt)
                && (!x.DurationSecondsLow.HasValue || !duration.HasValue || x.DurationSecondsLow <= duration)
                && (!x.DurationSecondsHigh.HasValue || !duration.HasValue || x.DurationSecondsHigh >= duration))
            .OrderByDescending(x => x.EffectiveAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No inventory benchmark matches market, pricing model, slot type, and duration band.");
        if (!benchmark.RangeLow.HasValue || !benchmark.RangeHigh.HasValue)
            throw new InvalidOperationException("The matching benchmark has no complete range.");

        var audience = await _db.CreatorAudienceSnapshots.AsNoTracking()
            .Include(x => x.ResearchSource)
            .Where(x => x.CreatorId == creator.Id)
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var performance = await _db.CreatorPerformanceSnapshots.AsNoTracking()
            .Include(x => x.ResearchSource)
            .Where(x => x.CreatorId == creator.Id)
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var reachMultiplier = ReachMultiplier(performance?.AverageViews, rule);
        var engagementMultiplier = EngagementMultiplier(performance?.EngagementRate, rule);
        var totalMultiplier = reachMultiplier * engagementMultiplier;
        var low = Money(benchmark.RangeLow.Value * totalMultiplier);
        var high = Money(benchmark.RangeHigh.Value * totalMultiplier);
        var target = Money((low + high) / 2m);
        var confidence = Confidence(benchmark, audience, performance);
        var now = calculationAt;

        var recommendation = new RateRecommendation
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            ContentItemId = slot.ContentItemId,
            AdInventorySlotId = slot.Id,
            AdvertiserOpportunityId = command.AdvertiserOpportunityId,
            BlissMatchId = command.BlissMatchId,
            GeographicMarketId = market.Id,
            PricingModelId = pricingModel.Id,
            PricingRuleVersionId = ruleVersion.Id,
            IndustryCategory = Trim(command.IndustryCategory, 128),
            CampaignObjective = Trim(command.CampaignObjective, 256),
            DurationSeconds = duration,
            CurrencyCode = benchmark.CurrencyCode,
            RangeLow = low,
            RangeTarget = target,
            RangeHigh = high,
            EstimatedImpressions = performance?.AverageViews,
            ConfidenceLevel = confidence,
            BenchmarkAsOf = benchmark.EffectiveAt,
            SourceSystem = sourceSystem,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            InputSnapshotJson = JsonSerializer.Serialize(new
            {
                creatorId = creator.Id,
                contentItemId = slot.ContentItemId,
                adInventorySlotId = slot.Id,
                slotType = slot.SlotType,
                durationSeconds = duration,
                geographicMarketId = market.Id,
                marketCode = market.MarketCode,
                pricingModelId = pricingModel.Id,
                pricingModelCode = pricingModel.Code,
                pricingRuleVersionId = ruleVersion.Id,
                inventoryRateBenchmarkId = benchmark.Id,
                creatorAudienceSnapshotId = audience?.Id,
                creatorPerformanceSnapshotId = performance?.Id,
                averageViews = performance?.AverageViews,
                engagementRate = performance?.EngagementRate,
                reachMultiplier,
                engagementMultiplier
            })
        };
        foreach (var factor in new[]
        {
            Factor(recommendation.Id, "BASE_BENCHMARK", "Comparable inventory range",
                benchmark.RangeLow, 1m,
                $"{benchmark.RangeLow:0.##}–{benchmark.RangeHigh:0.##} {benchmark.CurrencyCode}; benchmark {benchmark.Id}.", 1),
            Factor(recommendation.Id, "AVERAGE_VIEWS", "Average verified views",
                performance?.AverageViews, reachMultiplier,
                performance is null ? "No performance snapshot; neutral reach multiplier."
                    : $"{performance.AverageViews?.ToString() ?? "UNKNOWN"} average views from snapshot {performance.Id}.", 2),
            Factor(recommendation.Id, "ENGAGEMENT_RATE", "Engagement rate",
                performance?.EngagementRate, engagementMultiplier,
                performance is null ? "No performance snapshot; neutral engagement multiplier."
                    : $"{performance.EngagementRate?.ToString("0.####") ?? "UNKNOWN"} engagement from snapshot {performance.Id}.", 3),
            Factor(recommendation.Id, "DURATION_BAND", "Inventory duration band",
                duration, 1m,
                $"{duration?.ToString() ?? "UNKNOWN"} seconds matched benchmark band "
                + $"{benchmark.DurationSecondsLow?.ToString() ?? "open"}–{benchmark.DurationSecondsHigh?.ToString() ?? "open"}; no per-minute multiplication.", 4),
            Factor(recommendation.Id, "MARKET", "Geographic market",
                null, 1m, $"{market.MarketCode} market benchmark selected.", 5)
        })
        {
            recommendation.Factors.Add(factor);
        }
        recommendation.Sources.Add(new RateRecommendationSource
        {
            Id = Guid.NewGuid(),
            RateRecommendationId = recommendation.Id,
            ResearchSourceId = benchmark.ResearchSourceId,
            InventoryRateBenchmarkId = benchmark.Id,
            CreatorAudienceSnapshotId = audience?.Id,
            CreatorPerformanceSnapshotId = performance?.Id,
            Role = "MATERIAL_INPUTS"
        });

        _db.RateRecommendations.Add(recommendation);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await RecommendationGraph().SingleAsync(x => x.Id == recommendation.Id, cancellationToken), false);
    }

    public IQueryable<RateRecommendation> RecommendationGraph() =>
        _db.RateRecommendations
            .Include(x => x.Creator)
            .Include(x => x.AdInventorySlot)
            .Include(x => x.GeographicMarket)
            .Include(x => x.PricingModel)
            .Include(x => x.PricingRuleVersion)
            .Include(x => x.Factors)
            .Include(x => x.Sources).ThenInclude(x => x.ResearchSource)
            .AsSplitQuery();

    private static RateRecommendationFactor Factor(
        Guid recommendationId, string code, string label, decimal? value,
        decimal multiplier, string rationale, int order) =>
        new()
        {
            Id = Guid.NewGuid(),
            RateRecommendationId = recommendationId,
            FactorCode = code,
            Label = label,
            NumericValue = value,
            AdjustmentMultiplier = multiplier,
            Rationale = rationale,
            SortOrder = order
        };

    private static decimal ReachMultiplier(int? views, PricingRuleDocument rule) =>
        views switch
        {
            null => 1m,
            < 10_000 => rule.ReachBelow10kMultiplier,
            < 50_000 => rule.Reach10kTo49kMultiplier,
            < 100_000 => rule.Reach50kTo99kMultiplier,
            _ => rule.Reach100kPlusMultiplier
        };

    private static decimal EngagementMultiplier(decimal? rate, PricingRuleDocument rule) =>
        rate switch
        {
            null => 1m,
            < 0.04m => rule.EngagementBelow4PercentMultiplier,
            < 0.07m => rule.Engagement4To699PercentMultiplier,
            _ => rule.Engagement7PercentPlusMultiplier
        };

    private static string Confidence(
        InventoryRateBenchmark benchmark,
        CreatorAudienceSnapshot? audience,
        CreatorPerformanceSnapshot? performance)
    {
        var sourced = benchmark.ResearchSourceId.HasValue
            && audience?.ResearchSourceId.HasValue == true
            && performance?.ResearchSourceId.HasValue == true;
        var verified = sourced
            && benchmark.VerificationStatus == ObservationVerificationStatuses.Verified
            && audience!.VerificationStatus == ObservationVerificationStatuses.Verified
            && performance!.VerificationStatus == ObservationVerificationStatuses.Verified;
        return verified ? EconomicsConfidenceLevels.High
            : sourced ? EconomicsConfidenceLevels.Medium
            : EconomicsConfidenceLevels.Low;
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Required(string? value, string field, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > max)
            throw new InvalidOperationException($"{field} is required and must be at most {max} characters.");
        return result;
    }

    private static string? Trim(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) return null;
        if (result.Length > max) throw new InvalidOperationException($"Value must be at most {max} characters.");
        return result;
    }
}
