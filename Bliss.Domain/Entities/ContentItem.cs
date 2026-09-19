namespace Bliss.Domain.Entities;

public class ContentItem
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ExternalContentId { get; set; }
    public string? Url { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Creator Creator { get; set; } = null!;
    public ICollection<AdInventorySlot> AdInventorySlots { get; set; } = new List<AdInventorySlot>();
    public ICollection<CampaignPlacement> CampaignPlacements { get; set; } = new List<CampaignPlacement>();
}
