namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable Curator research-report.v1 snapshot. DocumentJson/Summary are never edited after insert.
/// Status/pointer metadata may change via human decisions.
/// </summary>
public class WeddingPlannerResearchReportVersion
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int VersionNumber { get; set; }
    public string SchemaVersion { get; set; } = "research-report.v1";
    public string DocumentJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Guid ProducingResearchJobId { get; set; }
    public Guid ProducingAgentRunId { get; set; }
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public Guid? ApprovedColorProfileVersionId { get; set; }
    public string Status { get; set; } = "PROPOSED";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerResearchJob ProducingResearchJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion? ApprovedColorProfileVersion { get; set; }
    public ICollection<WeddingPlannerResearchRoleContribution> RoleContributions { get; set; } =
        new List<WeddingPlannerResearchRoleContribution>();
    public ICollection<WeddingPlannerResearchReportDecision> Decisions { get; set; } =
        new List<WeddingPlannerResearchReportDecision>();
}
