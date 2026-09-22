namespace Bliss.Domain.Entities;

/// <summary>
/// Durable Concept Workshop job orchestration row. Terminal SUCCEEDED/FAILED jobs are not retried;
/// replay returns the existing row without AI calls.
/// </summary>
public class WeddingPlannerWorkshopJob
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Objective { get; set; } = string.Empty;
    public string CampaignGoal { get; set; } = string.Empty;
    public string AudienceFocus { get; set; } = string.Empty;
    public string ChannelFormat { get; set; } = string.Empty;
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public string DeliverablesJson { get; set; } = string.Empty;
    public string Cta { get; set; } = string.Empty;
    public string ConstraintsJson { get; set; } = "[]";
    public string InputJson { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }
    public string? StrategyStageOutputJson { get; set; }
    public string? CreativeStageOutputJson { get; set; }
    public string? ProductionStageOutputJson { get; set; }
    public Guid? StrategyAgentRunId { get; set; }
    public Guid? CreativeAgentRunId { get; set; }
    public Guid? ProductionAgentRunId { get; set; }
    public Guid? OutputConceptPackageVersionId { get; set; }
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
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public WeddingPlannerAgentRun? StrategyAgentRun { get; set; }
    public WeddingPlannerAgentRun? CreativeAgentRun { get; set; }
    public WeddingPlannerAgentRun? ProductionAgentRun { get; set; }
    public WeddingPlannerConceptPackageVersion? OutputConceptPackageVersion { get; set; }
    public ICollection<WeddingPlannerConceptRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerConceptRoleContribution>();
}
