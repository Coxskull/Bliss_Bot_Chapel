namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable human APPROVE/REJECT decision for a Concept Workshop package.
/// SelectedConceptId is required on APPROVE and forbidden on REJECT; never mutates the package document.
/// </summary>
public class WeddingPlannerConceptPackageDecision
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ConceptPackageVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? SelectedConceptId { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerConceptPackageVersion ConceptPackageVersion { get; set; } = null!;
}
