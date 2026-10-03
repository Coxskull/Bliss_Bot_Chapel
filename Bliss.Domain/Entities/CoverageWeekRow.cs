namespace Bliss.Domain.Entities;

public sealed class CoverageWeekRow
{
    public Guid Id { get; set; }
    public string WeekKey { get; set; } = string.Empty;
    public int QualifiedSlices { get; set; }
    public int MarketCount { get; set; }
    public string MarketLine { get; set; } = string.Empty;
    public string FuelStatus { get; set; } = string.Empty;
    public bool CensusClaimed { get; set; }
    public bool SlicesChanged { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
