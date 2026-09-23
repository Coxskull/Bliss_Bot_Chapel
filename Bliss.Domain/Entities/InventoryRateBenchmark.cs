namespace Bliss.Domain.Entities;

/// <summary>
/// A sourced comparable range for inventory. It is not an Alpha quote or rate.
/// </summary>
public class InventoryRateBenchmark
{
    public Guid Id { get; set; }
    public Guid? GeographicMarketId { get; set; }
    public Guid PricingModelId { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public string InventorySlotType { get; set; } = string.Empty;
    public string? Platform { get; set; }
    public string? ContentFormat { get; set; }
    public int? DurationSecondsLow { get; set; }
    public int? DurationSecondsHigh { get; set; }
    public decimal? RangeLow { get; set; }
    public decimal? RangeHigh { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public string VerificationStatus { get; set; } = "UNKNOWN";
    public DateTime EffectiveAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public GeographicMarket? GeographicMarket { get; set; }
    public PricingModel PricingModel { get; set; } = null!;
    public ResearchSource? ResearchSource { get; set; }
}
