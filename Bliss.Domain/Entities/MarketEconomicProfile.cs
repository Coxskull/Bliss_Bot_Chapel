namespace Bliss.Domain.Entities;

/// <summary>Versioned local advertising context; not a rate recommendation.</summary>
public class MarketEconomicProfile
{
    public Guid Id { get; set; }
    public Guid GeographicMarketId { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public int Version { get; set; }
    public decimal? PurchasingPowerIndex { get; set; }
    public string? CompetitionLevel { get; set; }
    public string? AudienceScarcityLevel { get; set; }
    public string? Notes { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public string VerificationStatus { get; set; } = "UNKNOWN";
    public DateTime EffectiveAt { get; set; }
    public DateTime? SupersededAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public GeographicMarket GeographicMarket { get; set; } = null!;
    public ResearchSource? ResearchSource { get; set; }
}
