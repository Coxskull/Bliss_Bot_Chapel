namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable human ACCEPT / RETURN_FOR_REVISION / ESCALATE decision for a QA review report.
/// Never mutates DocumentJson or contribution rows. Advertisers cannot record decisions.
/// </summary>
public class WeddingPlannerQaReviewDecision
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid QaReviewReportVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string SelectedVariantId { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public bool? VisualReviewConfirmed { get; set; }
    public bool? CopyReviewConfirmed { get; set; }
    public bool? ProvenanceReviewConfirmed { get; set; }
    public bool? SyntheticMarkerAcknowledged { get; set; }
    public string? EscalationCategory { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerQaReviewReportVersion QaReviewReportVersion { get; set; } = null!;
    public WeddingPlannerQaEscalationCase? EscalationCase { get; set; }
}
