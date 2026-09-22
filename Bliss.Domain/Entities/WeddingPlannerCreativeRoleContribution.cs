namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable durable evidence for one of the thirteen Creative Production logical roles on a package.
/// Exactly thirteen rows per successful package; never edited in place. Never backed by thirteen agent runs.
/// </summary>
public class WeddingPlannerCreativeRoleContribution
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid CreativePackageVersionId { get; set; }
    public Guid CreativeProductionJobId { get; set; }
    public string LogicalRole { get; set; } = string.Empty;
    public Guid ProducingAgentRunId { get; set; }
    public string ContributionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion CreativePackageVersion { get; set; } = null!;
    public WeddingPlannerCreativeProductionJob CreativeProductionJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
}
