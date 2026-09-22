namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable human APPROVE/REJECT decision for a Creative Package.
/// SelectedVariantId is required on APPROVE and forbidden on REJECT; never mutates the package document.
/// </summary>
public class WeddingPlannerCreativePackageDecision
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid CreativePackageVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? SelectedVariantId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion CreativePackageVersion { get; set; } = null!;
}
