namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable historical record of one deterministic rule evaluation.
/// Live eligibility/score rows on the match may be replaced; runs are append-only.
/// This is not an intelligence engine.
/// </summary>
public class MatchEvaluationRun
{
    public Guid Id { get; set; }
    public Guid BlissMatchId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid RuleVersionId { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? InputSnapshot { get; set; }
    public string? OutputSnapshot { get; set; }
    public string? MatchStatus { get; set; }
    public decimal? OverallScore { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public string Status { get; set; } = "CREATED";

    public BlissMatch BlissMatch { get; set; } = null!;
    public Creator Creator { get; set; } = null!;
    public RuleVersion RuleVersion { get; set; } = null!;
}
