namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable-open escalation case created only by human ESCALATE decision.
/// AI and rules cannot insert this row.
/// </summary>
public class WeddingPlannerQaEscalationCase
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid QaReviewReportVersionId { get; set; }
    public Guid QaReviewDecisionId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = "OPEN";
    public string RationaleSnapshot { get; set; } = string.Empty;
    public string SelectedVariantId { get; set; } = string.Empty;
    public Guid ApprovedCreativePackageVersionId { get; set; }
    public Guid SelectedCreativeAssetId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerQaReviewReportVersion QaReviewReportVersion { get; set; } = null!;
    public WeddingPlannerQaReviewDecision QaReviewDecision { get; set; } = null!;
    public WeddingPlannerQaEscalationResolution? Resolution { get; set; }
}
