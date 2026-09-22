namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable Brand DNA version snapshot. DocumentJson and Summary are never edited after insert.
/// Status/pointer metadata may change via human decisions.
/// </summary>
public class WeddingPlannerBrandDnaVersion
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int VersionNumber { get; set; }
    public string SchemaVersion { get; set; } = "brand-dna.v1";
    public string DocumentJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Guid ProducingAgentRunId { get; set; }
    public string Status { get; set; } = "PROPOSED";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
    public ICollection<WeddingPlannerBrandDnaDecision> Decisions { get; set; } = new List<WeddingPlannerBrandDnaDecision>();
}
