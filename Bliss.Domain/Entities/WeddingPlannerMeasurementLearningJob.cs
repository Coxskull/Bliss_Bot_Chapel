namespace Bliss.Domain.Entities;

/// <summary>
/// Durable measurement-learning job orchestration receipt. Terminal SUCCEEDED/FAILED jobs are not
/// retried; exact replay returns the existing row without rules/AI calls or writes.
/// Exactly two agent-run FKs on success; never three fake model calls.
/// </summary>
public class WeddingPlannerMeasurementLearningJob
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }

    public Guid CampaignReadinessHandshakeVersionId { get; set; }
    public string HandshakeStatusSnapshot { get; set; } = string.Empty;
    public bool HandshakeWasCurrentAtJobStart { get; set; }

    public Guid CampaignPlacementId { get; set; }
    public Guid CampaignPlacementRunId { get; set; }
    public Guid BlissMatchId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid ContentItemId { get; set; }
    public Guid AdInventorySlotId { get; set; }

    public Guid QaReviewReportVersionId { get; set; }
    public Guid ApprovedCreativePackageVersionId { get; set; }
    public string SelectedVariantId { get; set; } = string.Empty;
    public Guid SelectedCreativeAssetId { get; set; }
    public Guid ApprovedConceptPackageVersionId { get; set; }
    public string SelectedConceptId { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }

    public DateTime ObservationStart { get; set; }
    public DateTime ObservationEnd { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public bool AttestationAcknowledged { get; set; }
    public long Impressions { get; set; }
    public long Clicks { get; set; }
    public long Conversions { get; set; }
    public decimal Spend { get; set; }
    public decimal? Revenue { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public string InputJson { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public string? MetricsJson { get; set; }
    public string? RulesFindingsJson { get; set; }
    public string? PerformanceAnalysisStageOutputJson { get; set; }
    public string? LearningSynthesisStageOutputJson { get; set; }

    public Guid? PerformanceAnalysisAgentRunId { get; set; }
    public Guid? LearningSynthesisAgentRunId { get; set; }
    public Guid? OutputMeasurementLearningReportVersionId { get; set; }

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
    public WeddingPlannerCampaignReadinessHandshakeVersion CampaignReadinessHandshakeVersion { get; set; } = null!;
    public CampaignPlacement CampaignPlacement { get; set; } = null!;
    public CampaignPlacementRun CampaignPlacementRun { get; set; } = null!;
    public BlissMatch BlissMatch { get; set; } = null!;
    public Campaign Campaign { get; set; } = null!;
    public ContentItem ContentItem { get; set; } = null!;
    public AdInventorySlot AdInventorySlot { get; set; } = null!;
    public WeddingPlannerQaReviewReportVersion QaReviewReportVersion { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion ApprovedCreativePackageVersion { get; set; } = null!;
    public WeddingPlannerCreativeAsset SelectedCreativeAsset { get; set; } = null!;
    public WeddingPlannerConceptPackageVersion ApprovedConceptPackageVersion { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public WeddingPlannerAgentRun? PerformanceAnalysisAgentRun { get; set; }
    public WeddingPlannerAgentRun? LearningSynthesisAgentRun { get; set; }
    public WeddingPlannerMeasurementLearningReportVersion? OutputMeasurementLearningReportVersion { get; set; }
    public ICollection<WeddingPlannerMeasurementLearningRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerMeasurementLearningRoleContribution>();
}
