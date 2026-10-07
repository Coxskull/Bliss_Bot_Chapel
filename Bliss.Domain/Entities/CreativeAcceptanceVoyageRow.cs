namespace Bliss.Domain.Entities;

public sealed class CreativeAcceptanceVoyageRow
{
    public Guid Id { get; set; }
    public string VoyageKey { get; set; } = string.Empty;
    public string AdvertiserName { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Niche { get; set; } = string.Empty;
    public string InventoryProductId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ReportJson { get; set; } = string.Empty;
    public int ModelCalls { get; set; }
    public bool CampaignReady { get; set; }
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
