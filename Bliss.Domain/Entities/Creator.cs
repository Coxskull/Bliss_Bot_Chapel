namespace Bliss.Domain.Entities;

public class Creator
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? PrimaryLanguage { get; set; }
    /// <summary>
    /// Coarse audience size when known. Not a rate and not a linear multiplier
    /// for the future Economics engine.
    /// </summary>
    public int? AudienceSize { get; set; }

    /// <summary>
    /// Nullable so UNKNOWN remains distinguishable from a measured zero.
    /// </summary>
    public decimal? FemalePercentage { get; set; }

    /// <summary>
    /// Nullable so UNKNOWN remains distinguishable from a measured zero.
    /// </summary>
    public decimal? MalePercentage { get; set; }

    public string? PrimaryAgeRange { get; set; }
    public string? PrimaryGeography { get; set; }
    public string? EngagementLevel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<CreatorPlatform> Platforms { get; set; } = new List<CreatorPlatform>();
    public ICollection<ContentItem> ContentItems { get; set; } = new List<ContentItem>();
    public ICollection<BlissMatch> BlissMatches { get; set; } = new List<BlissMatch>();
}
