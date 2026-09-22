namespace Bliss.Domain.Entities;

/// <summary>
/// Durable Wedding Planner workspace owned by one advertiser.
/// Phase 1 allows a single primary workspace per advertiser.
/// </summary>
public class WeddingPlannerWorkspace
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public bool IsPrimary { get; set; } = true;
    public string Status { get; set; } = "ACTIVE";
    public Guid? CurrentApprovedBrandDnaVersionId { get; set; }
    public Guid? CurrentApprovedColorProfileVersionId { get; set; }
    public Guid? CurrentApprovedResearchReportVersionId { get; set; }
    public Guid? CurrentApprovedConceptPackageVersionId { get; set; }
    public Guid? CurrentApprovedCreativePackageVersionId { get; set; }
    public Guid? CurrentAcceptedQaReviewReportVersionId { get; set; }
    public Guid? CurrentCampaignReadinessHandshakeVersionId { get; set; }
    public Guid? CurrentAcceptedMeasurementLearningReportVersionId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion? CurrentApprovedBrandDnaVersion { get; set; }
    public WeddingPlannerColorProfileVersion? CurrentApprovedColorProfileVersion { get; set; }
    public WeddingPlannerResearchReportVersion? CurrentApprovedResearchReportVersion { get; set; }
    public WeddingPlannerConceptPackageVersion? CurrentApprovedConceptPackageVersion { get; set; }
    public WeddingPlannerCreativePackageVersion? CurrentApprovedCreativePackageVersion { get; set; }
    public WeddingPlannerQaReviewReportVersion? CurrentAcceptedQaReviewReportVersion { get; set; }
    public WeddingPlannerCampaignReadinessHandshakeVersion? CurrentCampaignReadinessHandshakeVersion { get; set; }
    public WeddingPlannerMeasurementLearningReportVersion? CurrentAcceptedMeasurementLearningReportVersion { get; set; }
    public ICollection<WeddingPlannerPlanningSession> Sessions { get; set; } = new List<WeddingPlannerPlanningSession>();
    public ICollection<WeddingPlannerAuditEvent> AuditEvents { get; set; } = new List<WeddingPlannerAuditEvent>();
    public ICollection<WeddingPlannerAgentRun> AgentRuns { get; set; } = new List<WeddingPlannerAgentRun>();
    public ICollection<WeddingPlannerBrandDnaVersion> BrandDnaVersions { get; set; } = new List<WeddingPlannerBrandDnaVersion>();
    public ICollection<WeddingPlannerBrandDnaDecision> BrandDnaDecisions { get; set; } = new List<WeddingPlannerBrandDnaDecision>();
    public ICollection<WeddingPlannerColorProfileVersion> ColorProfileVersions { get; set; } = new List<WeddingPlannerColorProfileVersion>();
    public ICollection<WeddingPlannerColorProfileDecision> ColorProfileDecisions { get; set; } = new List<WeddingPlannerColorProfileDecision>();
    public ICollection<WeddingPlannerResearchJob> ResearchJobs { get; set; } = new List<WeddingPlannerResearchJob>();
    public ICollection<WeddingPlannerResearchReportVersion> ResearchReportVersions { get; set; } = new List<WeddingPlannerResearchReportVersion>();
    public ICollection<WeddingPlannerResearchRoleContribution> ResearchRoleContributions { get; set; } = new List<WeddingPlannerResearchRoleContribution>();
    public ICollection<WeddingPlannerResearchReportDecision> ResearchReportDecisions { get; set; } = new List<WeddingPlannerResearchReportDecision>();
    public ICollection<WeddingPlannerWorkshopJob> WorkshopJobs { get; set; } = new List<WeddingPlannerWorkshopJob>();
    public ICollection<WeddingPlannerConceptPackageVersion> ConceptPackageVersions { get; set; } = new List<WeddingPlannerConceptPackageVersion>();
    public ICollection<WeddingPlannerConceptRoleContribution> ConceptRoleContributions { get; set; } = new List<WeddingPlannerConceptRoleContribution>();
    public ICollection<WeddingPlannerConceptPackageDecision> ConceptPackageDecisions { get; set; } = new List<WeddingPlannerConceptPackageDecision>();
    public ICollection<WeddingPlannerCreativeProductionJob> CreativeProductionJobs { get; set; } = new List<WeddingPlannerCreativeProductionJob>();
    public ICollection<WeddingPlannerCreativePackageVersion> CreativePackageVersions { get; set; } = new List<WeddingPlannerCreativePackageVersion>();
    public ICollection<WeddingPlannerCreativeRoleContribution> CreativeRoleContributions { get; set; } = new List<WeddingPlannerCreativeRoleContribution>();
    public ICollection<WeddingPlannerCreativeAsset> CreativeAssets { get; set; } = new List<WeddingPlannerCreativeAsset>();
    public ICollection<WeddingPlannerCreativePackageDecision> CreativePackageDecisions { get; set; } = new List<WeddingPlannerCreativePackageDecision>();
    public ICollection<WeddingPlannerQaReviewJob> QaReviewJobs { get; set; } = new List<WeddingPlannerQaReviewJob>();
    public ICollection<WeddingPlannerQaReviewReportVersion> QaReviewReportVersions { get; set; } = new List<WeddingPlannerQaReviewReportVersion>();
    public ICollection<WeddingPlannerQaRoleContribution> QaRoleContributions { get; set; } = new List<WeddingPlannerQaRoleContribution>();
    public ICollection<WeddingPlannerQaReviewDecision> QaReviewDecisions { get; set; } = new List<WeddingPlannerQaReviewDecision>();
    public ICollection<WeddingPlannerQaEscalationCase> QaEscalationCases { get; set; } = new List<WeddingPlannerQaEscalationCase>();
    public ICollection<WeddingPlannerQaEscalationResolution> QaEscalationResolutions { get; set; } = new List<WeddingPlannerQaEscalationResolution>();
    public ICollection<WeddingPlannerCampaignReadinessHandshakeVersion> CampaignReadinessHandshakeVersions { get; set; } =
        new List<WeddingPlannerCampaignReadinessHandshakeVersion>();
    public ICollection<WeddingPlannerCampaignReadinessDecision> CampaignReadinessDecisions { get; set; } =
        new List<WeddingPlannerCampaignReadinessDecision>();
    public ICollection<WeddingPlannerMeasurementLearningJob> MeasurementLearningJobs { get; set; } =
        new List<WeddingPlannerMeasurementLearningJob>();
    public ICollection<WeddingPlannerMeasurementLearningReportVersion> MeasurementLearningReportVersions { get; set; } =
        new List<WeddingPlannerMeasurementLearningReportVersion>();
    public ICollection<WeddingPlannerMeasurementLearningRoleContribution> MeasurementLearningRoleContributions { get; set; } =
        new List<WeddingPlannerMeasurementLearningRoleContribution>();
    public ICollection<WeddingPlannerMeasurementLearningDecision> MeasurementLearningDecisions { get; set; } =
        new List<WeddingPlannerMeasurementLearningDecision>();
}
