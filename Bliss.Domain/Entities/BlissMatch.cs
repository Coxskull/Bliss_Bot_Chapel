namespace Bliss.Domain.Entities;

public class BlissMatch
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public Guid AdvertiserOpportunityId { get; set; }
    public Guid RuleVersionId { get; set; }
    public string Status { get; set; } = "CREATED";
    public decimal? OverallScore { get; set; }
    public decimal? ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; }

    public Creator Creator { get; set; } = null!;
    public AdvertiserOpportunity AdvertiserOpportunity { get; set; } = null!;
    public RuleVersion RuleVersion { get; set; } = null!;
    public ICollection<MatchScoreComponent> ScoreComponents { get; set; } = new List<MatchScoreComponent>();
    public ICollection<EligibilityCheck> EligibilityChecks { get; set; } = new List<EligibilityCheck>();
}
