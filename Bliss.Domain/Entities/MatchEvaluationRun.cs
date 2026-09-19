namespace Bliss.Domain.Entities;

/// <summary>
/// Optional Phase 1 architectural preparation for Phase 3 historical auditability.
/// This is not an intelligence engine and does not evaluate matches.
/// </summary>
public class MatchEvaluationRun
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public Guid RuleVersionId { get; set; }
    public string AlgorithmVersion { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? InputSnapshot { get; set; }
    public string Status { get; set; } = "CREATED";

    public Creator Creator { get; set; } = null!;
    public RuleVersion RuleVersion { get; set; } = null!;
}
