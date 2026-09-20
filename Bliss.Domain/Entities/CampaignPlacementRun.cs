namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable audit record for one operator-controlled placement binding.
/// This records planning intent and performs no delivery or measurement.
/// </summary>
public class CampaignPlacementRun
{
    public Guid Id { get; set; }
    public Guid CampaignPlacementId { get; set; }
    public Guid BlissMatchId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid AdvertiserOpportunityId { get; set; }
    public Guid ContentItemId { get; set; }
    public Guid AdInventorySlotId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string OperatorLabel { get; set; } = string.Empty;
    public string Status { get; set; } = "COMPLETED";
    public string Outcome { get; set; } = "PLANNED";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string InputSnapshot { get; set; } = string.Empty;

    public CampaignPlacement CampaignPlacement { get; set; } = null!;
    public BlissMatch BlissMatch { get; set; } = null!;
    public Campaign Campaign { get; set; } = null!;
    public Creator Creator { get; set; } = null!;
    public AdvertiserOpportunity AdvertiserOpportunity { get; set; } = null!;
    public ContentItem ContentItem { get; set; } = null!;
    public AdInventorySlot AdInventorySlot { get; set; } = null!;
}
