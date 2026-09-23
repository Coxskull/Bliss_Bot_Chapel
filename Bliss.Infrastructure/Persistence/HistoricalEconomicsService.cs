using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record RecordHistoricalPlacementEconomicsCommand(
    Guid CampaignPlacementId,
    Guid QuoteOutcomeId,
    Guid QuoteLineItemId,
    Guid? CompensationIllustrationId,
    Guid? SupersedesHistoricalPlacementEconomicsId,
    decimal? ContractedLineAmount,
    long? ActualImpressions,
    long? ActualViews,
    long? ActualListens,
    long? ActualEngagements,
    long? ActualConversions,
    DateTime MeasurementAsOf,
    string? Notes,
    string SourceSystem,
    string IdempotencyKey);

public sealed record RecordCampaignPerformanceEconomicsCommand(
    Guid CampaignId,
    Guid? SupersedesCampaignPerformanceEconomicsId,
    long? ActualImpressions,
    long? ActualViews,
    long? ActualListens,
    long? ActualEngagements,
    decimal? EngagementRate,
    long? Conversions,
    decimal? ConversionValue,
    string? ConversionValueCurrencyCode,
    DateTime MeasurementAsOf,
    string? Notes,
    string SourceSystem,
    string IdempotencyKey);

public sealed record HistoricalPlacementEconomicsResult(
    HistoricalPlacementEconomics Record,
    bool IsReplay);

public sealed record CampaignPerformanceEconomicsResult(
    CampaignPerformanceEconomics Record,
    bool IsReplay);

/// <summary>
/// Appends analytical actuals. It never mutates commercial or operational
/// source records and never creates settlement obligations.
/// </summary>
public sealed class HistoricalEconomicsService
{
    private readonly BlissDbContext _db;

    public HistoricalEconomicsService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<HistoricalPlacementEconomicsResult> RecordPlacementAsync(
        RecordHistoricalPlacementEconomicsCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64)
            .ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await PlacementGraph().SingleOrDefaultAsync(
            x => x.SourceSystem == source && x.IdempotencyKey == key,
            cancellationToken);
        if (replay is not null) return new(replay, true);

        ValidateMeasurements(
            command.ActualImpressions,
            command.ActualViews,
            command.ActualListens,
            command.ActualEngagements,
            command.ActualConversions);
        ValidateMeasurementDate(command.MeasurementAsOf);

        var placement = await _db.CampaignPlacements.AsNoTracking()
            .Include(x => x.Campaign)
            .Include(x => x.AdInventorySlot)
            .SingleOrDefaultAsync(
                x => x.Id == command.CampaignPlacementId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "CampaignPlacementId does not reference a placement.");
        var outcome = await _db.QuoteOutcomes.AsNoTracking()
            .Include(x => x.Quote)
            .Include(x => x.QuoteVersion).ThenInclude(x => x.LineItems)
            .SingleOrDefaultAsync(x => x.Id == command.QuoteOutcomeId, cancellationToken)
            ?? throw new InvalidOperationException(
                "QuoteOutcomeId does not reference a quote outcome.");
        if (outcome.Response != QuoteOutcomeResponses.Accepted
            || !outcome.Amount.HasValue
            || outcome.Amount <= 0)
        {
            throw new InvalidOperationException(
                "Historical placement economics requires an ACCEPTED quote outcome.");
        }

        var line = outcome.QuoteVersion.LineItems.SingleOrDefault(
            x => x.Id == command.QuoteLineItemId)
            ?? throw new InvalidOperationException(
                "QuoteLineItemId must belong to the accepted quote version.");
        var recommendation = await _db.RateRecommendations.AsNoTracking()
            .Include(x => x.Sources).ThenInclude(x => x.InventoryRateBenchmark)
            .SingleAsync(x => x.Id == line.RateRecommendationId, cancellationToken);
        if (recommendation.AdInventorySlotId != placement.AdInventorySlotId)
        {
            throw new InvalidOperationException(
                "The quote recommendation inventory does not match the placement.");
        }
        if (placement.BlissMatchId.HasValue
            && recommendation.BlissMatchId.HasValue
            && placement.BlissMatchId != recommendation.BlissMatchId)
        {
            throw new InvalidOperationException(
                "The quote recommendation match does not match the placement.");
        }
        if (placement.Campaign.AdvertiserOpportunityId.HasValue
            && outcome.Quote.AdvertiserOpportunityId.HasValue
            && placement.Campaign.AdvertiserOpportunityId
                != outcome.Quote.AdvertiserOpportunityId)
        {
            throw new InvalidOperationException(
                "The accepted quote opportunity does not match the campaign.");
        }

        var contractedAmount = outcome.QuoteVersion.LineItems.Count == 1
            ? outcome.Amount.Value
            : command.ContractedLineAmount
                ?? throw new InvalidOperationException(
                    "ContractedLineAmount is required for a multi-line accepted quote.");
        if (contractedAmount <= 0 || contractedAmount > outcome.Amount.Value)
        {
            throw new InvalidOperationException(
                "ContractedLineAmount must be positive and cannot exceed the accepted total.");
        }
        if (line.CurrencyCode != outcome.CurrencyCode)
        {
            throw new InvalidOperationException(
                "Quote line and accepted outcome currencies must match.");
        }

        HistoricalPlacementEconomics? supersedes = null;
        if (command.SupersedesHistoricalPlacementEconomicsId.HasValue)
        {
            supersedes = await _db.HistoricalPlacementEconomics.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == command.SupersedesHistoricalPlacementEconomicsId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "SupersedesHistoricalPlacementEconomicsId does not reference history.");
            if (supersedes.CampaignPlacementId != placement.Id)
            {
                throw new InvalidOperationException(
                    "A correction must supersede history for the same placement.");
            }
            if (await _db.HistoricalPlacementEconomics.AsNoTracking().AnyAsync(
                x => x.SupersedesHistoricalPlacementEconomicsId == supersedes.Id,
                cancellationToken))
            {
                throw new InvalidOperationException(
                    "The historical placement record was already superseded.");
            }
        }

        CompensationIllustration? illustration = null;
        decimal? alphaAmount = null;
        decimal? creatorAmount = null;
        decimal? otherAmount = null;
        if (command.CompensationIllustrationId.HasValue)
        {
            if (outcome.QuoteVersion.LineItems.Count != 1)
            {
                throw new InvalidOperationException(
                    "A placement-level compensation snapshot requires a single-line quote.");
            }
            illustration = await _db.CompensationIllustrations.AsNoTracking()
                .Include(x => x.Lines)
                .SingleOrDefaultAsync(
                    x => x.Id == command.CompensationIllustrationId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "CompensationIllustrationId does not reference an illustration.");
            if (illustration.QuoteOutcomeId != outcome.Id
                || illustration.QuoteVersionId != outcome.QuoteVersionId
                || illustration.CurrencyCode != outcome.CurrencyCode)
            {
                throw new InvalidOperationException(
                    "Compensation illustration must reference the accepted quote outcome.");
            }
            alphaAmount = illustration.Lines
                .Where(x => x.ParticipantRole == CompensationParticipantRoles.Alpha)
                .Sum(x => x.Amount);
            creatorAmount = illustration.Lines
                .Where(x => x.ParticipantRole == CompensationParticipantRoles.Creator)
                .Sum(x => x.Amount);
            otherAmount = illustration.Lines
                .Where(x => x.ParticipantRole == CompensationParticipantRoles.OtherAuthorized)
                .Sum(x => x.Amount);
        }

        var benchmark = recommendation.Sources
            .Select(x => x.InventoryRateBenchmark)
            .FirstOrDefault(x => x is not null);
        var comparableBenchmark = benchmark?.CurrencyCode == outcome.CurrencyCode
            ? benchmark
            : null;
        decimal? effectiveCpm = command.ActualImpressions > 0
            ? Ratio(contractedAmount * 1000m, command.ActualImpressions.Value)
            : null;
        decimal? effectiveCpv = command.ActualViews > 0
            ? Ratio(contractedAmount, command.ActualViews.Value)
            : null;
        decimal? recommendationVariance = recommendation.RangeTarget > 0
            ? Percentage(contractedAmount, recommendation.RangeTarget)
            : null;
        decimal? benchmarkMidpoint =
            comparableBenchmark?.RangeLow.HasValue == true
            && comparableBenchmark.RangeHigh.HasValue
                ? (comparableBenchmark.RangeLow.Value
                    + comparableBenchmark.RangeHigh.Value) / 2m
                : null;

        var now = DateTime.UtcNow;
        var record = new HistoricalPlacementEconomics
        {
            Id = Guid.NewGuid(),
            CampaignPlacementId = placement.Id,
            QuoteVersionId = outcome.QuoteVersionId,
            QuoteOutcomeId = outcome.Id,
            QuoteLineItemId = line.Id,
            RateRecommendationId = recommendation.Id,
            CompensationIllustrationId = illustration?.Id,
            SupersedesHistoricalPlacementEconomicsId = supersedes?.Id,
            QuotedAmount = line.LineAmount,
            ContractedAmount = Money(contractedAmount),
            CurrencyCode = outcome.CurrencyCode,
            ActualImpressions = command.ActualImpressions,
            ActualViews = command.ActualViews,
            ActualListens = command.ActualListens,
            ActualEngagements = command.ActualEngagements,
            ActualConversions = command.ActualConversions,
            RecommendationLow = recommendation.RangeLow,
            RecommendationTarget = recommendation.RangeTarget,
            RecommendationHigh = recommendation.RangeHigh,
            ExternalBenchmarkLow = comparableBenchmark?.RangeLow,
            ExternalBenchmarkHigh = comparableBenchmark?.RangeHigh,
            EffectiveCpm = effectiveCpm,
            EffectiveCpv = effectiveCpv,
            ContractedVsRecommendationTargetPercentage = recommendationVariance,
            ContractedVsExternalMidpointPercentage = benchmarkMidpoint > 0
                ? Percentage(contractedAmount, benchmarkMidpoint.Value)
                : null,
            AlphaCompensationAmount = alphaAmount,
            CreatorCompensationAmount = creatorAmount,
            OtherCompensationAmount = otherAmount,
            MeasurementAsOf = command.MeasurementAsOf.ToUniversalTime(),
            Notes = Optional(command.Notes, 2000),
            SourceSystem = source,
            IdempotencyKey = key,
            RecordedAt = now,
            InputSnapshotJson = JsonSerializer.Serialize(new
            {
                campaignPlacementId = placement.Id,
                campaignId = placement.CampaignId,
                adInventorySlotId = placement.AdInventorySlotId,
                quoteId = outcome.QuoteId,
                quoteVersionId = outcome.QuoteVersionId,
                quoteOutcomeId = outcome.Id,
                quoteLineItemId = line.Id,
                rateRecommendationId = recommendation.Id,
                inventoryRateBenchmarkId = comparableBenchmark?.Id,
                compensationIllustrationId = illustration?.Id,
                supersedesHistoricalPlacementEconomicsId = supersedes?.Id,
                measurementAsOf = command.MeasurementAsOf.ToUniversalTime()
            })
        };

        _db.HistoricalPlacementEconomics.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        return new(
            await PlacementGraph().SingleAsync(x => x.Id == record.Id, cancellationToken),
            false);
    }

    public async Task<CampaignPerformanceEconomicsResult> RecordCampaignAsync(
        RecordCampaignPerformanceEconomicsCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64)
            .ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await CampaignGraph().SingleOrDefaultAsync(
            x => x.SourceSystem == source && x.IdempotencyKey == key,
            cancellationToken);
        if (replay is not null) return new(replay, true);

        ValidateMeasurements(
            command.ActualImpressions,
            command.ActualViews,
            command.ActualListens,
            command.ActualEngagements,
            command.Conversions);
        if (command.EngagementRate is < 0 or > 1)
            throw new InvalidOperationException("EngagementRate must be between 0 and 1.");
        if (command.ConversionValue is < 0)
            throw new InvalidOperationException("ConversionValue cannot be negative.");
        var conversionCurrency = Optional(command.ConversionValueCurrencyCode, 8)
            ?.ToUpperInvariant();
        if (command.ConversionValue.HasValue != (conversionCurrency is not null))
        {
            throw new InvalidOperationException(
                "ConversionValue and ConversionValueCurrencyCode must be supplied together.");
        }
        ValidateMeasurementDate(command.MeasurementAsOf);

        var campaign = await _db.Campaigns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == command.CampaignId, cancellationToken)
            ?? throw new InvalidOperationException("CampaignId does not reference a campaign.");

        CampaignPerformanceEconomics? supersedes = null;
        if (command.SupersedesCampaignPerformanceEconomicsId.HasValue)
        {
            supersedes = await _db.CampaignPerformanceEconomics.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == command.SupersedesCampaignPerformanceEconomicsId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "SupersedesCampaignPerformanceEconomicsId does not reference history.");
            if (supersedes.CampaignId != campaign.Id)
                throw new InvalidOperationException(
                    "A correction must supersede performance for the same campaign.");
            if (await _db.CampaignPerformanceEconomics.AsNoTracking().AnyAsync(
                x => x.SupersedesCampaignPerformanceEconomicsId == supersedes.Id,
                cancellationToken))
                throw new InvalidOperationException(
                    "The campaign performance record was already superseded.");
        }

        var history = await _db.HistoricalPlacementEconomics.AsNoTracking()
            .Include(x => x.CampaignPlacement)
            .Where(x => x.CampaignPlacement.CampaignId == campaign.Id)
            .ToListAsync(cancellationToken);
        var supersededIds = history
            .Where(x => x.SupersedesHistoricalPlacementEconomicsId.HasValue)
            .Select(x => x.SupersedesHistoricalPlacementEconomicsId!.Value)
            .ToHashSet();
        var currentPlacementHistory = history
            .Where(x => !supersededIds.Contains(x.Id))
            .ToList();
        var currencies = currentPlacementHistory
            .Select(x => x.CurrencyCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var contractedCurrency = currencies.Count == 1 ? currencies[0] : null;
        decimal? contractedAmount = contractedCurrency is null
            ? null
            : currentPlacementHistory.Sum(x => x.ContractedAmount);
        decimal? effectiveCpm = contractedAmount.HasValue && command.ActualImpressions > 0
            ? Ratio(contractedAmount.Value * 1000m, command.ActualImpressions.Value)
            : null;
        decimal? effectiveCpv = contractedAmount.HasValue && command.ActualViews > 0
            ? Ratio(contractedAmount.Value, command.ActualViews.Value)
            : null;

        var now = DateTime.UtcNow;
        var record = new CampaignPerformanceEconomics
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            SupersedesCampaignPerformanceEconomicsId = supersedes?.Id,
            ActualImpressions = command.ActualImpressions,
            ActualViews = command.ActualViews,
            ActualListens = command.ActualListens,
            ActualEngagements = command.ActualEngagements,
            EngagementRate = command.EngagementRate,
            Conversions = command.Conversions,
            ConversionValue = command.ConversionValue,
            ConversionValueCurrencyCode = conversionCurrency,
            AlphaPlacementCount = currentPlacementHistory.Count,
            AlphaContractedAmount = contractedAmount,
            AlphaContractedCurrencyCode = contractedCurrency,
            EffectiveCpm = effectiveCpm,
            EffectiveCpv = effectiveCpv,
            MeasurementAsOf = command.MeasurementAsOf.ToUniversalTime(),
            Notes = Optional(command.Notes, 2000),
            SourceSystem = source,
            IdempotencyKey = key,
            RecordedAt = now,
            InputSnapshotJson = JsonSerializer.Serialize(new
            {
                campaignId = campaign.Id,
                supersedesCampaignPerformanceEconomicsId = supersedes?.Id,
                placementHistoryIds = currentPlacementHistory.Select(x => x.Id),
                alphaPlacementCount = currentPlacementHistory.Count,
                alphaContractedAmount = contractedAmount,
                alphaContractedCurrencyCode = contractedCurrency,
                measurementAsOf = command.MeasurementAsOf.ToUniversalTime()
            })
        };
        _db.CampaignPerformanceEconomics.Add(record);
        await _db.SaveChangesAsync(cancellationToken);
        return new(
            await CampaignGraph().SingleAsync(x => x.Id == record.Id, cancellationToken),
            false);
    }

    public IQueryable<HistoricalPlacementEconomics> PlacementGraph() =>
        _db.HistoricalPlacementEconomics
            .Include(x => x.CampaignPlacement).ThenInclude(x => x.Campaign)
            .Include(x => x.CampaignPlacement).ThenInclude(x => x.AdInventorySlot)
            .Include(x => x.QuoteVersion)
            .Include(x => x.QuoteOutcome)
            .Include(x => x.QuoteLineItem)
            .Include(x => x.RateRecommendation).ThenInclude(x => x.PricingModel)
            .Include(x => x.CompensationIllustration)
            .AsSplitQuery();

    public IQueryable<CampaignPerformanceEconomics> CampaignGraph() =>
        _db.CampaignPerformanceEconomics
            .Include(x => x.Campaign)
            .AsSplitQuery();

    private static void ValidateMeasurements(params long?[] values)
    {
        if (values.All(x => !x.HasValue))
            throw new InvalidOperationException(
                "At least one actual measurement is required.");
        if (values.Any(x => x < 0))
            throw new InvalidOperationException("Actual measurements cannot be negative.");
    }

    private static void ValidateMeasurementDate(DateTime value)
    {
        if (value == default || value.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
            throw new InvalidOperationException(
                "MeasurementAsOf is required and cannot be in the future.");
    }

    private static decimal? Percentage(decimal actual, decimal baseline) =>
        baseline <= 0 ? null : Ratio((actual - baseline) * 100m, baseline);

    private static decimal Ratio(decimal numerator, decimal denominator) =>
        Math.Round(numerator / denominator, 6, MidpointRounding.AwayFromZero);

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Required(string? value, string field, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result) || result.Length > max)
            throw new InvalidOperationException(
                $"{field} is required and must be at most {max} characters.");
        return result;
    }

    private static string? Optional(string? value, int max)
    {
        var result = value?.Trim();
        if (string.IsNullOrWhiteSpace(result)) return null;
        if (result.Length > max)
            throw new InvalidOperationException($"Value must be at most {max} characters.");
        return result;
    }
}
