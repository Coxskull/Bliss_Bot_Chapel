namespace Bliss.Domain.Entities;

/// <summary>Versioned category economics, optionally scoped to one market.</summary>
public class IndustryEconomicProfile
{
    public Guid Id { get; set; }
    public Guid? GeographicMarketId { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Version { get; set; }
    public decimal? AcquisitionCostLow { get; set; }
    public decimal? AcquisitionCostHigh { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Notes { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public string VerificationStatus { get; set; } = "UNKNOWN";
    public DateTime EffectiveAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public GeographicMarket? GeographicMarket { get; set; }
    public ResearchSource? ResearchSource { get; set; }
}
