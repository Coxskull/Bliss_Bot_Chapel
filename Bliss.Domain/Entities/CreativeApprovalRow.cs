namespace Bliss.Domain.Entities;

/// <summary>
/// One human creative decision inside an open Wedding Planner workspace.
/// Campaign ready is not granted. No price is stored.
/// </summary>
public class CreativeApprovalRow
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AdvertiserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public bool CampaignReady { get; set; }
    public bool MatchWritten { get; set; }
    public bool PriceInvented { get; set; }
    public int ModelCalls { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }

    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
}
