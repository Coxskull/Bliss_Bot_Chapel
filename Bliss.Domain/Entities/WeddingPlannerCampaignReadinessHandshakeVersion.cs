namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable campaign-readiness-handshake.v1 snapshot. DocumentJson never updates after insert.
/// Status mutates only on explicit REVOKE_CAMPAIGN_READY (CAMPAIGN_READY → REVOKED).
/// Campaign-ready state lives here + workspace pointer — never on creative/QA rows.
/// </summary>
public class WeddingPlannerCampaignReadinessHandshakeVersion
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public int VersionNumber { get; set; }
    public string SchemaVersion { get; set; } = "campaign-readiness-handshake.v1";
    public string DocumentJson { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = "CAMPAIGN_READY";

    public Guid QaReviewReportVersionId { get; set; }
    public string QaReviewReportDocumentSha256 { get; set; } = string.Empty;
    public Guid QaAcceptDecisionId { get; set; }

    public Guid ApprovedCreativePackageVersionId { get; set; }
    public string CreativePackageDocumentSha256 { get; set; } = string.Empty;
    public Guid CreativePackageDecisionId { get; set; }
    public string SelectedVariantId { get; set; } = string.Empty;
    public Guid SelectedCreativeAssetId { get; set; }
    public string SelectedCreativeAssetSha256 { get; set; } = string.Empty;
    public int SelectedCreativeAssetByteSize { get; set; }
    public int SelectedCreativeAssetWidth { get; set; }
    public int SelectedCreativeAssetHeight { get; set; }

    public Guid ApprovedConceptPackageVersionId { get; set; }
    public string SelectedConceptId { get; set; } = string.Empty;
    public Guid ApprovedBrandDnaVersionId { get; set; }
    public int ApprovedBrandDnaVersionNumber { get; set; }
    public Guid ApprovedColorProfileVersionId { get; set; }
    public int ApprovedColorProfileVersionNumber { get; set; }
    public Guid ApprovedResearchReportVersionId { get; set; }
    public int ApprovedResearchReportVersionNumber { get; set; }

    public Guid BlissMatchId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid AdvertiserOpportunityId { get; set; }
    public Guid RuleVersionId { get; set; }
    public string MatchStatusSnapshot { get; set; } = string.Empty;
    public decimal? MatchOverallScoreSnapshot { get; set; }
    public string OpportunityStatusSnapshot { get; set; } = string.Empty;

    public Guid CampaignId { get; set; }
    public Guid ContentItemId { get; set; }
    public Guid AdInventorySlotId { get; set; }
    public Guid CampaignPlacementId { get; set; }
    public Guid CampaignPlacementRunId { get; set; }

    public string RulesFindingsJson { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public bool DisclaimerAcknowledged { get; set; }
    public bool? SyntheticMarkerAcknowledged { get; set; }

    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerQaReviewReportVersion QaReviewReportVersion { get; set; } = null!;
    public WeddingPlannerQaReviewDecision QaAcceptDecision { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion ApprovedCreativePackageVersion { get; set; } = null!;
    public WeddingPlannerCreativePackageDecision CreativePackageDecision { get; set; } = null!;
    public WeddingPlannerCreativeAsset SelectedCreativeAsset { get; set; } = null!;
    public WeddingPlannerConceptPackageVersion ApprovedConceptPackageVersion { get; set; } = null!;
    public WeddingPlannerBrandDnaVersion ApprovedBrandDnaVersion { get; set; } = null!;
    public WeddingPlannerColorProfileVersion ApprovedColorProfileVersion { get; set; } = null!;
    public WeddingPlannerResearchReportVersion ApprovedResearchReportVersion { get; set; } = null!;
    public BlissMatch BlissMatch { get; set; } = null!;
    public Creator Creator { get; set; } = null!;
    public AdvertiserOpportunity AdvertiserOpportunity { get; set; } = null!;
    public RuleVersion RuleVersion { get; set; } = null!;
    public Campaign Campaign { get; set; } = null!;
    public ContentItem ContentItem { get; set; } = null!;
    public AdInventorySlot AdInventorySlot { get; set; } = null!;
    public CampaignPlacement CampaignPlacement { get; set; } = null!;
    public CampaignPlacementRun CampaignPlacementRun { get; set; } = null!;
    public ICollection<WeddingPlannerCampaignReadinessDecision> Decisions { get; set; } =
        new List<WeddingPlannerCampaignReadinessDecision>();
}
