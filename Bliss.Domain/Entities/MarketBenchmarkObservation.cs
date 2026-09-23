namespace Bliss.Domain.Entities;

/// <summary>
/// One append-only market fact with provenance. It is not an Alpha rate.
/// </summary>
public class MarketBenchmarkObservation
{
    public Guid Id { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public Guid? GeographicMarketId { get; set; }
    public string? IndustryCategory { get; set; }
    public string? Platform { get; set; }
    public string? InventorySlotType { get; set; }
    public string Metric { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public decimal? RangeLow { get; set; }
    public decimal? RangeHigh { get; set; }
    public string? CurrencyCode { get; set; }
    public DateOnly? PublicationDate { get; set; }
    public DateTime RetrievedAt { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public string VerificationStatus { get; set; } = "UNKNOWN";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public ResearchSource? ResearchSource { get; set; }
    public GeographicMarket? GeographicMarket { get; set; }
}
