namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable human APPROVE/REJECT decision for a Color Intelligence profile version.
/// Approval alone may set the workspace current-approved color profile pointer.
/// </summary>
public class WeddingPlannerColorProfileDecision
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ColorProfileVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ColorProfileVersion { get; set; } = null!;
}
