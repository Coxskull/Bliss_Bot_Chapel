namespace Bliss.Domain.Entities;

/// <summary>
/// Append-only conversation record. Clients may append ADVERTISER/OPERATOR/SYSTEM.
/// PLANNER messages are server-owned via orchestration only.
/// </summary>
public class WeddingPlannerConversationMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid AdvertiserId { get; set; }
    public int SequenceNumber { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public WeddingPlannerPlanningSession Session { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public Advertiser Advertiser { get; set; } = null!;
}
