namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable durable evidence for one of the four Concept Workshop logical roles on a package.
/// Exactly four rows per successful package; never edited in place.
/// </summary>
public class WeddingPlannerConceptRoleContribution
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ConceptPackageVersionId { get; set; }
    public Guid WorkshopJobId { get; set; }
    public string LogicalRole { get; set; } = string.Empty;
    public Guid ProducingAgentRunId { get; set; }
    public string ContributionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerConceptPackageVersion ConceptPackageVersion { get; set; } = null!;
    public WeddingPlannerWorkshopJob WorkshopJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
}
