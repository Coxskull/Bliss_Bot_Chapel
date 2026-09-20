namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable audit record for one accepted, operator-controlled match formation.
/// </summary>
public class MatchFormationRun
{
    public Guid Id { get; set; }
    public Guid BlissMatchId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid AdvertiserOpportunityId { get; set; }
    public Guid RuleVersionId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = "COMPLETED";
    public string Outcome { get; set; } = "CREATED";
    public bool EvaluateOnCreate { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string InputSnapshot { get; set; } = string.Empty;

    public BlissMatch BlissMatch { get; set; } = null!;
    public Creator Creator { get; set; } = null!;
    public AdvertiserOpportunity AdvertiserOpportunity { get; set; } = null!;
    public RuleVersion RuleVersion { get; set; } = null!;
}
