namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable audit record for one labelled operator review decision.
/// The reviewer is a TEST operator label, not an authentication product.
/// </summary>
public class MatchReviewDecision
{
    public Guid Id { get; set; }
    public Guid BlissMatchId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid? MatchEvaluationRunId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ReviewerLabel { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string ResultingMatchStatus { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public string Status { get; set; } = "COMPLETED";
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string InputSnapshot { get; set; } = string.Empty;

    public BlissMatch BlissMatch { get; set; } = null!;
    public Creator Creator { get; set; } = null!;
    public MatchEvaluationRun? MatchEvaluationRun { get; set; }
}
