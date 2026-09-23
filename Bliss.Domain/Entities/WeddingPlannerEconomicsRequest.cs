namespace Bliss.Domain.Entities;

/// <summary>
/// Durable, tenant-owned link between a Wedding Planner session and an
/// Economics recommendation. The Planner presents the result; it does not
/// calculate or alter it.
/// </summary>
public class WeddingPlannerEconomicsRequest
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid SessionId { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid BlissMatchId { get; set; }
    public Guid AdInventorySlotId { get; set; }
    public Guid GeographicMarketId { get; set; }
    public Guid PricingModelId { get; set; }
    public Guid RateRecommendationId { get; set; }
    public int? RequestedDurationSeconds { get; set; }
    public string? IndustryCategory { get; set; }
    public string? CampaignObjective { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerPlanningSession Session { get; set; } = null!;
    public Advertiser Advertiser { get; set; } = null!;
    public BlissMatch BlissMatch { get; set; } = null!;
    public AdInventorySlot AdInventorySlot { get; set; } = null!;
    public GeographicMarket GeographicMarket { get; set; } = null!;
    public PricingModel PricingModel { get; set; } = null!;
    public RateRecommendation RateRecommendation { get; set; } = null!;
}
