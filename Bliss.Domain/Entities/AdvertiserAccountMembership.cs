namespace Bliss.Domain.Entities;

/// <summary>Issuer subject to the single advertiser tenant created at registration.</summary>
public sealed class AdvertiserAccountMembership
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public Guid AdvertiserId { get; set; }
    public string Status { get; set; } = "PENDING_REVIEW";
    public DateTime CreatedAt { get; set; }
    public Advertiser Advertiser { get; set; } = null!;
}
