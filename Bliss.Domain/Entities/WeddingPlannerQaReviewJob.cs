namespace Bliss.Domain.Entities;

/// <summary>
/// Durable QA review job orchestration row. Terminal SUCCEEDED/FAILED jobs are not retried;
/// replay returns the existing row without rules/AI calls. Steward is never an agent run.
/// </summary>
public class WeddingPlannerQaReviewJob
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string ReviewObjective { get; set; } = string.Empty;
    public string FocusAreasJson { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string InputJson { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public Guid ApprovedCreativePackageVersionId { get; set; }
    public string CreativePackageDocumentSha256 { get; set; } = string.Empty;
    public Guid CreativePackageDecisionId { get; set; }
    public string SelectedVariantId { get; set; } = string.Empty;
    public Guid SelectedCreativeAssetId { get; set; }
    public string SelectedCreativeAssetSha256 { get; set; } = string.Empty;
    public string SelectedCreativeAssetContentType { get; set; } = string.Empty;
    public int SelectedCreativeAssetByteSize { get; set; }
    public int SelectedCreativeAssetWidth { get; set; }
    public int SelectedCreativeAssetHeight { get; set; }
    public string SelectedConceptId { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }
    public string? RulesFindingsJson { get; set; }
    public string? ChaperoneReviewStageOutputJson { get; set; }
    public string? QaInspectionStageOutputJson { get; set; }
    public Guid? ChaperoneReviewAgentRunId { get; set; }
    public Guid? QaInspectionAgentRunId { get; set; }
    public Guid? OutputQaReviewReportVersionId { get; set; }
    public string Status { get; set; } = "RUNNING";
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion ApprovedCreativePackageVersion { get; set; } = null!;
    public WeddingPlannerCreativePackageDecision CreativePackageDecision { get; set; } = null!;
    public WeddingPlannerCreativeAsset SelectedCreativeAsset { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public WeddingPlannerAgentRun? ChaperoneReviewAgentRun { get; set; }
    public WeddingPlannerAgentRun? QaInspectionAgentRun { get; set; }
    public WeddingPlannerQaReviewReportVersion? OutputQaReviewReportVersion { get; set; }
    public ICollection<WeddingPlannerQaRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerQaRoleContribution>();
}
