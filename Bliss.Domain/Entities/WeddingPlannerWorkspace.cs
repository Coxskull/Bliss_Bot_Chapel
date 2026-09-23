namespace Bliss.Domain.Entities;

/// <summary>
/// Durable Wedding Planner workspace owned by one advertiser.
/// Phase 1 allows a single primary workspace per advertiser.
/// </summary>
public class WeddingPlannerWorkspace
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public bool IsPrimary { get; set; } = true;
    public string Status { get; set; } = "ACTIVE";
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public ICollection<WeddingPlannerPlanningSession> Sessions { get; set; } = new List<WeddingPlannerPlanningSession>();
    public ICollection<WeddingPlannerAuditEvent> AuditEvents { get; set; } = new List<WeddingPlannerAuditEvent>();
    public ICollection<WeddingPlannerEconomicsRequest> EconomicsRequests { get; set; } =
        new List<WeddingPlannerEconomicsRequest>();
}
