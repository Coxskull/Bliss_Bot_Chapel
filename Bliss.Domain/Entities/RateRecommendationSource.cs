namespace Bliss.Domain.Entities;

/// <summary>Exact persisted inputs supporting one recommendation.</summary>
public class RateRecommendationSource
{
    public Guid Id { get; set; }
    public Guid RateRecommendationId { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public Guid? InventoryRateBenchmarkId { get; set; }
    public Guid? CreatorAudienceSnapshotId { get; set; }
    public Guid? CreatorPerformanceSnapshotId { get; set; }
    public string Role { get; set; } = string.Empty;

    public RateRecommendation RateRecommendation { get; set; } = null!;
    public ResearchSource? ResearchSource { get; set; }
    public InventoryRateBenchmark? InventoryRateBenchmark { get; set; }
    public CreatorAudienceSnapshot? CreatorAudienceSnapshot { get; set; }
    public CreatorPerformanceSnapshot? CreatorPerformanceSnapshot { get; set; }
}
