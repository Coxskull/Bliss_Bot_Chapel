namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable durable evidence for one of the three Phase 7 control roles on a report.
/// Exactly three rows per successful report. Steward has ContributionSource=RULES_HUMAN and
/// ProducingAgentRunId=null. Never backed by three agent runs.
/// </summary>
public class WeddingPlannerQaRoleContribution
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid QaReviewReportVersionId { get; set; }
    public Guid QaReviewJobId { get; set; }
    public string LogicalRole { get; set; } = string.Empty;
    public string ContributionSource { get; set; } = string.Empty;
    public Guid? ProducingAgentRunId { get; set; }
    public string ContributionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerQaReviewReportVersion QaReviewReportVersion { get; set; } = null!;
    public WeddingPlannerQaReviewJob QaReviewJob { get; set; } = null!;
    public WeddingPlannerAgentRun? ProducingAgentRun { get; set; }
}
