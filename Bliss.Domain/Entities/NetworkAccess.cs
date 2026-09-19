namespace Bliss.Domain.Entities;

public class NetworkAccess
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid AffiliateNetworkId { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public string? ExternalAccountId { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public AffiliateNetwork AffiliateNetwork { get; set; } = null!;
}
