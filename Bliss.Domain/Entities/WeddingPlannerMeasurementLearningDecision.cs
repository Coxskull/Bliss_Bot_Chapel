namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable operator/admin ACCEPT | REJECT decision for a measurement-learning report.
/// Never mutates DocumentJson or contribution rows. Advertisers/reviewers cannot record decisions.
/// </summary>
public class WeddingPlannerMeasurementLearningDecision
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid MeasurementLearningReportVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerMeasurementLearningReportVersion MeasurementLearningReportVersion { get; set; } = null!;
}
