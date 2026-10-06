namespace Bliss.Domain.Entities;

public sealed class RealEstateProductRow
{
    public string ProductId { get; set; } = string.Empty;
    public string Version { get; set; } = "V1";
    public string Tier { get; set; } = string.Empty;
    public string Exclusivity { get; set; } = string.Empty;
    public int MaximumAdvertisers { get; set; }
    public string SlotIds { get; set; } = string.Empty;
    public string ShowcaseId { get; set; } = string.Empty;
    public string Lifecycle { get; set; } = "DRAFT";
    public string OccupancyStatus { get; set; } = "UNRECORDED";
    public string DeviceStatus { get; set; } = "NEEDS_REVIEW";
    public string PlatformStatus { get; set; } = "NEEDS_REVIEW";
    public string Notice { get; set; } = string.Empty;
}
