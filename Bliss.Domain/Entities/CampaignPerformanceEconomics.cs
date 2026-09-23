namespace Bliss.Domain.Entities;

/// <summary>
/// Append-only campaign-level performance snapshot and Alpha economics rollup.
/// </summary>
public class CampaignPerformanceEconomics
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? SupersedesCampaignPerformanceEconomicsId { get; set; }
    public long? ActualImpressions { get; set; }
    public long? ActualViews { get; set; }
    public long? ActualListens { get; set; }
    public long? ActualEngagements { get; set; }
    public decimal? EngagementRate { get; set; }
    public long? Conversions { get; set; }
    public decimal? ConversionValue { get; set; }
    public string? ConversionValueCurrencyCode { get; set; }
    public int AlphaPlacementCount { get; set; }
    public decimal? AlphaContractedAmount { get; set; }
    public string? AlphaContractedCurrencyCode { get; set; }
    public decimal? EffectiveCpm { get; set; }
    public decimal? EffectiveCpv { get; set; }
    public DateTime MeasurementAsOf { get; set; }
    public string? Notes { get; set; }
    public string InputSnapshotJson { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
    public CampaignPerformanceEconomics? Supersedes { get; set; }
}
