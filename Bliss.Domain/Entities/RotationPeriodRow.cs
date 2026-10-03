namespace Bliss.Domain.Entities;

/// <summary>
/// One later rotation period. Open slots stay open. No revenue is stored.
/// </summary>
public class RotationPeriodRow
{
    public Guid Id { get; set; }
    public string PeriodKey { get; set; } = string.Empty;
    public int TheoreticalSlots { get; set; }
    public int PlacedAdvertisers { get; set; }
    public int OpenSlots { get; set; }
    public int SlotCount { get; set; }
    public bool CreatorApproved { get; set; }
    public string RevenueLine { get; set; } = string.Empty;
    public bool CensusClaimed { get; set; }
    public bool SlotsChanged { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
