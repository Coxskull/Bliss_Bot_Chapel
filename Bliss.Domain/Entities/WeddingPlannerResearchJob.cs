namespace Bliss.Domain.Entities;

/// <summary>
/// Durable Curator research job orchestration row. Terminal SUCCEEDED/FAILED jobs are not retried;
/// replay returns the existing row without provider or AI calls.
/// </summary>
public class WeddingPlannerResearchJob
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Objective { get; set; } = string.Empty;
    public string QuestionsJson { get; set; } = string.Empty;
    public string Geography { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string AllowedDomainsJson { get; set; } = "[]";
    public string InputJson { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public Guid? ApprovedColorProfileVersionId { get; set; }
    public string? ResearchProviderKey { get; set; }
    public string? ResearchAdapterVersion { get; set; }
    public string? ResearchProviderRequestId { get; set; }
    public string? ResearchWorkerKey { get; set; }
    public decimal? ResearchEstimatedCostUsd { get; set; }
    public string? SourceCatalogJson { get; set; }
    public string? ResearchStageOutputJson { get; set; }
    public string? EvidenceStageOutputJson { get; set; }
    public string? SynthesisRiskStageOutputJson { get; set; }
    public Guid? ResearchAgentRunId { get; set; }
    public Guid? EvidenceAgentRunId { get; set; }
    public Guid? SynthesisRiskAgentRunId { get; set; }
    public Guid? OutputResearchReportVersionId { get; set; }
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
    public WeddingPlannerColorProfileVersion? ApprovedColorProfileVersion { get; set; }
    public WeddingPlannerAgentRun? ResearchAgentRun { get; set; }
    public WeddingPlannerAgentRun? EvidenceAgentRun { get; set; }
    public WeddingPlannerAgentRun? SynthesisRiskAgentRun { get; set; }
    public WeddingPlannerResearchReportVersion? OutputResearchReportVersion { get; set; }
    public ICollection<WeddingPlannerResearchRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerResearchRoleContribution>();
}
