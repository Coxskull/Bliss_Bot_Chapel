namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable Color Intelligence profile version. DocumentJson, Summary, InputJson, and InputSha256
/// are never edited after insert. Status/pointer metadata may change via human decisions.
/// The version row itself is the execution receipt (no agent-run FK).
/// </summary>
public class WeddingPlannerColorProfileVersion
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int VersionNumber { get; set; }
    public string SchemaVersion { get; set; } = "color-profile.v1";
    public string AlgorithmVersion { get; set; } = "aci.hsl.v1";
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public string DocumentJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string InputJson { get; set; } = string.Empty;
    public string InputSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = "PROPOSED";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public ICollection<WeddingPlannerColorProfileDecision> Decisions { get; set; } = new List<WeddingPlannerColorProfileDecision>();
}
