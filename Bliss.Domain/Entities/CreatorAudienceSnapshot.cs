namespace Bliss.Domain.Entities;

/// <summary>
/// Append-only audience composition measured for one creator at one time.
/// Missing measurements remain null and are not treated as zero.
/// </summary>
public class CreatorAudienceSnapshot
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? GeographicMarketId { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public DateTime CapturedAt { get; set; }
    public int? Subscribers { get; set; }
    public decimal? FemalePercentage { get; set; }
    public decimal? MalePercentage { get; set; }
    public string? PrimaryAgeRange { get; set; }
    public string? PrimaryGeography { get; set; }
    public string? Language { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public string VerificationStatus { get; set; } = "UNKNOWN";
    public DateTime CreatedAt { get; set; }

    public Creator Creator { get; set; } = null!;
    public GeographicMarket? GeographicMarket { get; set; }
    public ResearchSource? ResearchSource { get; set; }
}
