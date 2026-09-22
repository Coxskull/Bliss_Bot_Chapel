namespace Bliss.Domain.Entities;

/// <summary>
/// Append-only Wedding Planner ownership and mutation history.
/// </summary>
public class WeddingPlannerAuditEvent
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? MessageId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string ActorLabel { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string? Detail { get; set; }
    public DateTime OccurredAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace? Workspace { get; set; }
    public WeddingPlannerPlanningSession? Session { get; set; }
    public WeddingPlannerConversationMessage? Message { get; set; }
}
