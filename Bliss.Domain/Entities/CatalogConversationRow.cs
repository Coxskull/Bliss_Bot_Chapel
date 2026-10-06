namespace Bliss.Domain.Entities;

public sealed class CatalogConversationRow
{
    public Guid Id { get; set; }
    public string Intent { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Reply { get; set; } = string.Empty;
    public string ProductIds { get; set; } = string.Empty;
    public string ShowcaseIds { get; set; } = string.Empty;
    public bool ShowcaseDisplayed { get; set; }
    public bool HumanEscalation { get; set; }
    public bool InventedProduct { get; set; }
    public bool InventedPrice { get; set; }
    public int ModelCalls { get; set; }
    public bool CampaignReady { get; set; }
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
