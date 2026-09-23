namespace Bliss.Domain.Entities;

/// <summary>An immutable, explainable recommendation range. It is not a quote.</summary>
public class RateRecommendation
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? ContentItemId { get; set; }
    public Guid? AdInventorySlotId { get; set; }
    public Guid? AdvertiserOpportunityId { get; set; }
    public Guid? BlissMatchId { get; set; }
    public Guid GeographicMarketId { get; set; }
    public Guid PricingModelId { get; set; }
    public Guid PricingRuleVersionId { get; set; }
    public string? IndustryCategory { get; set; }
    public string? CampaignObjective { get; set; }
    public int? DurationSeconds { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal RangeLow { get; set; }
    public decimal RangeTarget { get; set; }
    public decimal RangeHigh { get; set; }
    public int? EstimatedImpressions { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public DateTime? BenchmarkAsOf { get; set; }
    public string InputSnapshotJson { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Creator Creator { get; set; } = null!;
    public ContentItem? ContentItem { get; set; }
    public AdInventorySlot? AdInventorySlot { get; set; }
    public AdvertiserOpportunity? AdvertiserOpportunity { get; set; }
    public BlissMatch? BlissMatch { get; set; }
    public GeographicMarket GeographicMarket { get; set; } = null!;
    public PricingModel PricingModel { get; set; } = null!;
    public PricingRuleVersion PricingRuleVersion { get; set; } = null!;
    public ICollection<RateRecommendationFactor> Factors { get; set; } =
        new List<RateRecommendationFactor>();
    public ICollection<RateRecommendationSource> Sources { get; set; } =
        new List<RateRecommendationSource>();
}
