namespace Bliss.Domain.Entities;

/// <summary>
/// Append-only commercial and delivery actuals for one planned placement.
/// This is analytical history, never a payable or settlement.
/// </summary>
public class HistoricalPlacementEconomics
{
    public Guid Id { get; set; }
    public Guid CampaignPlacementId { get; set; }
    public Guid QuoteVersionId { get; set; }
    public Guid QuoteOutcomeId { get; set; }
    public Guid QuoteLineItemId { get; set; }
    public Guid RateRecommendationId { get; set; }
    public Guid? CompensationIllustrationId { get; set; }
    public Guid? SupersedesHistoricalPlacementEconomicsId { get; set; }

    public decimal QuotedAmount { get; set; }
    public decimal ContractedAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public long? ActualImpressions { get; set; }
    public long? ActualViews { get; set; }
    public long? ActualListens { get; set; }
    public long? ActualEngagements { get; set; }
    public long? ActualConversions { get; set; }

    public decimal RecommendationLow { get; set; }
    public decimal RecommendationTarget { get; set; }
    public decimal RecommendationHigh { get; set; }
    public decimal? ExternalBenchmarkLow { get; set; }
    public decimal? ExternalBenchmarkHigh { get; set; }
    public decimal? EffectiveCpm { get; set; }
    public decimal? EffectiveCpv { get; set; }
    public decimal? ContractedVsRecommendationTargetPercentage { get; set; }
    public decimal? ContractedVsExternalMidpointPercentage { get; set; }

    public decimal? AlphaCompensationAmount { get; set; }
    public decimal? CreatorCompensationAmount { get; set; }
    public decimal? OtherCompensationAmount { get; set; }

    public DateTime MeasurementAsOf { get; set; }
    public string? Notes { get; set; }
    public string InputSnapshotJson { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }

    public CampaignPlacement CampaignPlacement { get; set; } = null!;
    public QuoteVersion QuoteVersion { get; set; } = null!;
    public QuoteOutcome QuoteOutcome { get; set; } = null!;
    public QuoteLineItem QuoteLineItem { get; set; } = null!;
    public RateRecommendation RateRecommendation { get; set; } = null!;
    public CompensationIllustration? CompensationIllustration { get; set; }
    public HistoricalPlacementEconomics? Supersedes { get; set; }
}
