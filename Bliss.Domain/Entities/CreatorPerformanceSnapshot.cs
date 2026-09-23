namespace Bliss.Domain.Entities;

/// <summary>
/// Append-only reach and engagement measurements for a creator or content item.
/// These metrics are future economics inputs, not rates.
/// </summary>
public class CreatorPerformanceSnapshot
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? ContentItemId { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public DateTime CapturedAt { get; set; }
    public int? AverageViews { get; set; }
    public int? DailyViews { get; set; }
    public int? WeeklyViews { get; set; }
    public int? MonthlyViews { get; set; }
    public int? HistoricalReach { get; set; }
    public decimal? EngagementRate { get; set; }
    public decimal? RetentionRate { get; set; }
    public decimal? PublishingFrequencyPerWeek { get; set; }
    public string? Platform { get; set; }
    public string? ContentFormat { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public string VerificationStatus { get; set; } = "UNKNOWN";
    public DateTime CreatedAt { get; set; }

    public Creator Creator { get; set; } = null!;
    public ContentItem? ContentItem { get; set; }
    public ResearchSource? ResearchSource { get; set; }
}
