namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable MARK_CAMPAIGN_READY / REVOKE_CAMPAIGN_READY decision.
/// Never mutates handshake DocumentJson. Revoke mutates handshake Status only.
/// </summary>
public class WeddingPlannerCampaignReadinessDecision
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid CampaignReadinessHandshakeVersionId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerCampaignReadinessHandshakeVersion CampaignReadinessHandshakeVersion { get; set; } = null!;
}
