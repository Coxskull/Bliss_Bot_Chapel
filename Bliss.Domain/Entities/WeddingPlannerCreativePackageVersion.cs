namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable creative-package.v1 snapshot. DocumentJson/Summary are never edited after insert.
/// Status/pointer metadata may change via human decisions. Selected variant is never stored here.
/// </summary>
public class WeddingPlannerCreativePackageVersion
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int VersionNumber { get; set; }
    public string SchemaVersion { get; set; } = "creative-package.v1";
    public string DocumentJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Guid ProducingCreativeProductionJobId { get; set; }
    public Guid ProducingAgentRunId { get; set; }
    public Guid ApprovedConceptPackageVersionId { get; set; }
    public string SelectedConceptId { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }
    public string JobKind { get; set; } = string.Empty;
    public Guid? ParentCreativePackageVersionId { get; set; }
    public string Status { get; set; } = "PROPOSED";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerCreativeProductionJob ProducingCreativeProductionJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
    public WeddingPlannerConceptPackageVersion ApprovedConceptPackageVersion { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion? ParentCreativePackageVersion { get; set; }
    public ICollection<WeddingPlannerCreativeRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerCreativeRoleContribution>();
    public ICollection<WeddingPlannerCreativeAsset> Assets { get; set; } =
        new List<WeddingPlannerCreativeAsset>();
    public ICollection<WeddingPlannerCreativePackageDecision> Decisions { get; set; } =
        new List<WeddingPlannerCreativePackageDecision>();
}
