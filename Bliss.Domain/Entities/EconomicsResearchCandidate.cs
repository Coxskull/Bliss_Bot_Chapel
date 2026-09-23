namespace Bliss.Domain.Entities;

/// <summary>Untrusted extracted research staged for explicit human review.</summary>
public class EconomicsResearchCandidate
{
    public Guid Id { get; set; }
    public Guid EconomicsResearchRunId { get; set; }
    public Guid GeographicMarketId { get; set; }
    public string? IndustryCategory { get; set; }
    public string? Platform { get; set; }
    public string? InventorySlotType { get; set; }
    public string Metric { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public decimal? RangeLow { get; set; }
    public decimal? RangeHigh { get; set; }
    public string? CurrencyCode { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public DateOnly? PublicationDate { get; set; }
    public DateTime RetrievedAt { get; set; }
    public string ConfidenceLevel { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public string ExtractionModel { get; set; } = string.Empty;
    public string RawPayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? PromotedObservationId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public EconomicsResearchRun EconomicsResearchRun { get; set; } = null!;
    public GeographicMarket GeographicMarket { get; set; } = null!;
    public MarketBenchmarkObservation? PromotedObservation { get; set; }
    public ICollection<EconomicsResearchReviewDecision> ReviewDecisions { get; set; } =
        new List<EconomicsResearchReviewDecision>();
}
