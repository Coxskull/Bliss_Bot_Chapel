namespace Bliss.Domain.Entities;

public class LaneTempoState
{
    public Guid Id { get; set; }
    public string Lane { get; set; } = string.Empty;
    public string Tempo { get; set; } = "FULL";
    public decimal? CeilingAmount { get; set; }
    public string? CeilingCurrency { get; set; }
    public string Notice { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class LaneTempoAudit
{
    public Guid Id { get; set; }
    public string Lane { get; set; } = string.Empty;
    public string Tempo { get; set; } = string.Empty;
    public decimal? CeilingAmount { get; set; }
    public string? CeilingCurrency { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Notice { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
