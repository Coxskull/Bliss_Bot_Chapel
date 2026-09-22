namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable qa-review-report.v1 snapshot. DocumentJson/Summary are never edited after insert.
/// Status/pointer metadata may change via human decisions/resolutions.
/// </summary>
public class WeddingPlannerQaReviewReportVersion
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int VersionNumber { get; set; }
    public string SchemaVersion { get; set; } = "qa-review-report.v1";
    public string DocumentJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Guid ProducingQaReviewJobId { get; set; }
    public Guid ProducingAgentRunId { get; set; }
    public Guid ApprovedCreativePackageVersionId { get; set; }
    public string CreativePackageDocumentSha256 { get; set; } = string.Empty;
    public Guid CreativePackageDecisionId { get; set; }
    public string SelectedVariantId { get; set; } = string.Empty;
    public Guid SelectedCreativeAssetId { get; set; }
    public string SelectedCreativeAssetSha256 { get; set; } = string.Empty;
    public string SelectedConceptId { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }
    public string Status { get; set; } = "PROPOSED";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerQaReviewJob ProducingQaReviewJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion ApprovedCreativePackageVersion { get; set; } = null!;
    public WeddingPlannerCreativePackageDecision CreativePackageDecision { get; set; } = null!;
    public WeddingPlannerCreativeAsset SelectedCreativeAsset { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public ICollection<WeddingPlannerQaRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerQaRoleContribution>();
    public ICollection<WeddingPlannerQaReviewDecision> Decisions { get; set; } =
        new List<WeddingPlannerQaReviewDecision>();
    public ICollection<WeddingPlannerQaEscalationCase> EscalationCases { get; set; } =
        new List<WeddingPlannerQaEscalationCase>();
}
