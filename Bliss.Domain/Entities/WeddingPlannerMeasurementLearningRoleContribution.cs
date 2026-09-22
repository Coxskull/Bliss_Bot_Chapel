namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable durable evidence for one of the three Phase 9 intelligence roles on a report.
/// Exactly three rows per successful report. All contributions are AI-backed and map to one of
/// exactly two producing agent runs (never three fake model calls).
/// </summary>
public class WeddingPlannerMeasurementLearningRoleContribution
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid MeasurementLearningReportVersionId { get; set; }
    public Guid MeasurementLearningJobId { get; set; }
    public string LogicalRole { get; set; } = string.Empty;
    public string ContributionSource { get; set; } = string.Empty;
    public Guid ProducingAgentRunId { get; set; }
    public string ContributionJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerMeasurementLearningReportVersion MeasurementLearningReportVersion { get; set; } = null!;
    public WeddingPlannerMeasurementLearningJob MeasurementLearningJob { get; set; } = null!;
    public WeddingPlannerAgentRun ProducingAgentRun { get; set; } = null!;
}
