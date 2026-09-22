namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable human APPROVE/REJECT decision for a Brand DNA version.
/// Approval alone may set the workspace current-approved pointer.
/// </summary>
public class WeddingPlannerBrandDnaDecision
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid BrandDnaVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion BrandDnaVersion { get; set; } = null!;
}
