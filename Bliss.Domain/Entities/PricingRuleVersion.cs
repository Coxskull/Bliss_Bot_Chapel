namespace Bliss.Domain.Entities;

/// <summary>Immutable deterministic pricing-rule document.</summary>
public class PricingRuleVersion
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DocumentJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<RateRecommendation> Recommendations { get; set; } =
        new List<RateRecommendation>();
}
