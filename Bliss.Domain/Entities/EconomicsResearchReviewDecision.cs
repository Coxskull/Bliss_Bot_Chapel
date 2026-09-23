namespace Bliss.Domain.Entities;

/// <summary>A human gate decision on one staged research candidate.</summary>
public class EconomicsResearchReviewDecision
{
    public Guid Id { get; set; }
    public Guid EconomicsResearchCandidateId { get; set; }
    public Guid? MarketBenchmarkObservationId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string ReviewerLabel { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public EconomicsResearchCandidate EconomicsResearchCandidate { get; set; } = null!;
    public MarketBenchmarkObservation? MarketBenchmarkObservation { get; set; }
}
