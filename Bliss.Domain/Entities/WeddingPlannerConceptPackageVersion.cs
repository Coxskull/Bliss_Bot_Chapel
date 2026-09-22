namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable concept-package.v1 snapshot. DocumentJson/Summary are never edited after insert.
/// Status/pointer metadata may change via human decisions. Selected concept is never stored here.
/// </summary>
public class WeddingPlannerConceptPackageVersion
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int VersionNumber { get; set; }
    public string SchemaVersion { get; set; } = "concept-package.v1";
    public string DocumentJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Guid ProducingWorkshopJobId { get; set; }
    public Guid ProducingAgentRunId { get; set; }
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }
    public string ChannelFormat { get; set; } = string.Empty;
    public int CanvasWidth { get; set; }
    public int CanvasHeight { get; set; }
    public string Status { get; set; } = "PROPOSED";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerWorkshopJob ProducingWorkshopJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public ICollection<WeddingPlannerConceptRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerConceptRoleContribution>();
    public ICollection<WeddingPlannerConceptPackageDecision> Decisions { get; set; } =
        new List<WeddingPlannerConceptPackageDecision>();
}
