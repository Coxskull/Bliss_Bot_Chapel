namespace Bliss.Domain.Entities;

public class CampaignPlacement
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid ContentItemId { get; set; }
    public Guid AdInventorySlotId { get; set; }
    public Guid? BlissMatchId { get; set; }

    /// <summary>
    /// Placement planning status. Contracted rates, quotes, and compensation
    /// belong to the future Economics and ledger contexts, not this row.
    /// </summary>
    public string Status { get; set; } = "CREATED";
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    public Campaign Campaign { get; set; } = null!;
    public ContentItem ContentItem { get; set; } = null!;
    public AdInventorySlot AdInventorySlot { get; set; } = null!;
    public BlissMatch? BlissMatch { get; set; }
    public ICollection<HistoricalPlacementEconomics> EconomicsHistory { get; set; } =
        new List<HistoricalPlacementEconomics>();
}
