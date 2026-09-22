namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable resolution for an OPEN escalation case (one successful resolution per case).
/// WAIVE_AND_ACCEPT is operator/admin only and requires exact blocker-code acknowledgment.
/// </summary>
public class WeddingPlannerQaEscalationResolution
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid QaEscalationCaseId { get; set; }
    public Guid QaReviewReportVersionId { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string? ExceptionRationale { get; set; }
    public bool? ExceptionAcknowledged { get; set; }
    public string? AcknowledgedBlockerCodesJson { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerQaEscalationCase QaEscalationCase { get; set; } = null!;
    public WeddingPlannerQaReviewReportVersion QaReviewReportVersion { get; set; } = null!;
}
