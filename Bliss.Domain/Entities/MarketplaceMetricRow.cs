namespace Bliss.Domain.Entities;

public sealed class MarketplaceMetricRow
{
    public Guid Id { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public int AdvertiserCount { get; set; }
    public int CreatorCount { get; set; }
    public int SlotCount { get; set; }
    public int RevenueRowCount { get; set; }
    public string Pressure { get; set; } = string.Empty;
    public string RevenueLine { get; set; } = string.Empty;
    public bool CensusClaimed { get; set; }
    public bool RevenueRecorded { get; set; }
    public bool SlotsChanged { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
