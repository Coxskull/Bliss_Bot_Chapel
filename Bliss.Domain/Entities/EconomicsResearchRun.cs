namespace Bliss.Domain.Entities;

/// <summary>A durable bounded request for asynchronous public-market research.</summary>
public class EconomicsResearchRun
{
    public Guid Id { get; set; }
    public Guid GeographicMarketId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public string? IndustryCategory { get; set; }
    public string? Platform { get; set; }
    public string? InventorySlotType { get; set; }
    public string ResearchQuestion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public GeographicMarket GeographicMarket { get; set; } = null!;
    public ICollection<EconomicsResearchCandidate> Candidates { get; set; } =
        new List<EconomicsResearchCandidate>();
}
