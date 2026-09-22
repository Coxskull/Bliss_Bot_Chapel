namespace Bliss.Domain.Entities;

/// <summary>
/// Resume-able planning session inside a Wedding Planner workspace.
/// Messages are appended; the session record is not silently replaced.
/// </summary>
public class WeddingPlannerPlanningSession
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AdvertiserId { get; set; }
    public string Status { get; set; } = "OPEN";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public Advertiser Advertiser { get; set; } = null!;
    public ICollection<WeddingPlannerConversationMessage> Messages { get; set; } = new List<WeddingPlannerConversationMessage>();
}
