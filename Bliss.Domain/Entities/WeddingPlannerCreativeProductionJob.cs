namespace Bliss.Domain.Entities;

/// <summary>
/// Durable Creative Production job orchestration row for INITIAL and REVISION.
/// Terminal SUCCEEDED/FAILED jobs are not retried; replay returns the existing row without AI/asset calls.
/// </summary>
public class WeddingPlannerCreativeProductionJob
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string JobKind { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string FormatsJson { get; set; } = string.Empty;
    public int RequestedVariantCount { get; set; }
    public Guid? RevisionParentCreativePackageVersionId { get; set; }
    public string? RevisionNotes { get; set; }
    public string InputJson { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public Guid ApprovedConceptPackageVersionId { get; set; }
    public string SelectedConceptId { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }
    public string? CreativeDirectionStageOutputJson { get; set; }
    public string? StrategyAdaptationStageOutputJson { get; set; }
    public string? VisualSystemStageOutputJson { get; set; }
    public string? ImageDirectionStageOutputJson { get; set; }
    public string? CopySystemStageOutputJson { get; set; }
    public string? VariantProductionStageOutputJson { get; set; }
    public Guid? CreativeDirectionAgentRunId { get; set; }
    public Guid? StrategyAdaptationAgentRunId { get; set; }
    public Guid? VisualSystemAgentRunId { get; set; }
    public Guid? ImageDirectionAgentRunId { get; set; }
    public Guid? CopySystemAgentRunId { get; set; }
    public Guid? VariantProductionAgentRunId { get; set; }
    public string? AssetProviderKey { get; set; }
    public string? AssetProviderAdapterVersion { get; set; }
    public string? AssetProviderRequestId { get; set; }
    public string? AssetProviderReceiptJson { get; set; }
    public decimal? AssetProviderEstimatedCostUsd { get; set; }
    public Guid? OutputCreativePackageVersionId { get; set; }
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
    public WeddingPlannerConceptPackageVersion ApprovedConceptPackageVersion { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion? RevisionParentCreativePackageVersion { get; set; }
    public WeddingPlannerAgentRun? CreativeDirectionAgentRun { get; set; }
    public WeddingPlannerAgentRun? StrategyAdaptationAgentRun { get; set; }
    public WeddingPlannerAgentRun? VisualSystemAgentRun { get; set; }
    public WeddingPlannerAgentRun? ImageDirectionAgentRun { get; set; }
    public WeddingPlannerAgentRun? CopySystemAgentRun { get; set; }
    public WeddingPlannerAgentRun? VariantProductionAgentRun { get; set; }
    public WeddingPlannerCreativePackageVersion? OutputCreativePackageVersion { get; set; }
    public ICollection<WeddingPlannerCreativeRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerCreativeRoleContribution>();
    public ICollection<WeddingPlannerCreativeAsset> Assets { get; set; } =
        new List<WeddingPlannerCreativeAsset>();
}
