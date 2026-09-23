namespace Bliss.Domain.Entities;

public class AdInventorySlot
{
    public Guid Id { get; set; }
    public Guid ContentItemId { get; set; }
    public string SlotType { get; set; } = string.Empty;
    public int? StartSecond { get; set; }

    /// <summary>
    /// Length of the slot when known. Duration is one pricing variable for the
    /// future Economics engine; it is not a linear dollars-per-minute rate.
    /// </summary>
    public int? DurationSeconds { get; set; }
    public bool IsAvailable { get; set; } = true;

    public ContentItem ContentItem { get; set; } = null!;
    public ICollection<CampaignPlacement> CampaignPlacements { get; set; } = new List<CampaignPlacement>();
}
