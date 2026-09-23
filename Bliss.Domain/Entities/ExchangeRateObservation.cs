namespace Bliss.Domain.Entities;

/// <summary>A dated, sourced FX observation. It is never silently overwritten.</summary>
public class ExchangeRateObservation
{
    public Guid Id { get; set; }
    public Guid? ResearchSourceId { get; set; }
    public string BaseCurrencyCode { get; set; } = string.Empty;
    public string QuoteCurrencyCode { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime ObservedAt { get; set; }
    public DateTime RetrievedAt { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public string VerificationStatus { get; set; } = "UNKNOWN";
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public ResearchSource? ResearchSource { get; set; }
}
