namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable durable evidence for one of the eight Curator logical roles on a report.
/// Exactly eight rows per successful report; never edited in place.
/// </summary>
public class WeddingPlannerResearchRoleContribution
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid ResearchReportVersionId { get; set; }
    public Guid ResearchJobId { get; set; }
    public string LogicalRole { get; set; } = string.Empty;
    public Guid ProducingAgentRunId { get; set; }
    public string ContributionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ResearchReportVersion { get; set; } = null!;
    public WeddingPlannerResearchJob ResearchJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
}
