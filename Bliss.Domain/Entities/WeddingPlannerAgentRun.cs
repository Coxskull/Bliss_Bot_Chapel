namespace Bliss.Domain.Entities;

/// <summary>
/// Append-oriented execution receipt for a Wedding Planner AI worker invocation.
/// Terminal runs (SUCCEEDED/FAILED) are not reopened; replay uses SourceSystem+IdempotencyKey.
/// </summary>
public class WeddingPlannerAgentRun
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid? SessionId { get; set; }
    public string LogicalRole { get; set; } = string.Empty;
    public string WorkerKey { get; set; } = string.Empty;
    public string PromptPackVersion { get; set; } = string.Empty;
    public string? ProviderKey { get; set; }
    public string? ModelId { get; set; }
    public string? AdapterVersion { get; set; }
    public Guid? TriggerMessageId { get; set; }
    public Guid? OutputMessageId { get; set; }
    public Guid? OutputBrandDnaVersionId { get; set; }
    public string? WorkerProfileVersion { get; set; }
    public string? AssignedRolesJson { get; set; }
    public Guid? OutputResearchReportVersionId { get; set; }
    public Guid? OutputConceptPackageVersionId { get; set; }
    public Guid? OutputCreativePackageVersionId { get; set; }
    public Guid? OutputQaReviewReportVersionId { get; set; }
    public Guid? OutputMeasurementLearningReportVersionId { get; set; }
    public string? RequestId { get; set; }
    public string? ProviderRequestId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = "RUNNING";
    public string? Outcome { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public decimal? EstimatedCostUsd { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerPlanningSession? Session { get; set; }
    public WeddingPlannerConversationMessage? TriggerMessage { get; set; }
    public WeddingPlannerConversationMessage? OutputMessage { get; set; }
    public WeddingPlannerBrandDnaVersion? OutputBrandDnaVersion { get; set; }
    public WeddingPlannerResearchReportVersion? OutputResearchReportVersion { get; set; }
    public WeddingPlannerConceptPackageVersion? OutputConceptPackageVersion { get; set; }
    public WeddingPlannerCreativePackageVersion? OutputCreativePackageVersion { get; set; }
    public WeddingPlannerQaReviewReportVersion? OutputQaReviewReportVersion { get; set; }
    public WeddingPlannerMeasurementLearningReportVersion? OutputMeasurementLearningReportVersion { get; set; }
}
