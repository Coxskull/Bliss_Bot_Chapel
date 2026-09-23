namespace Bliss.Domain.Economics;

/// <summary>
/// Versioned bootstrap rule payload. Values are data and must be read from the
/// persisted PricingRuleVersion used by each recommendation.
/// </summary>
public sealed class PricingRuleDocument
{
    public decimal ReachBelow10kMultiplier { get; set; }
    public decimal Reach10kTo49kMultiplier { get; set; }
    public decimal Reach50kTo99kMultiplier { get; set; }
    public decimal Reach100kPlusMultiplier { get; set; }
    public decimal EngagementBelow4PercentMultiplier { get; set; }
    public decimal Engagement4To699PercentMultiplier { get; set; }
    public decimal Engagement7PercentPlusMultiplier { get; set; }
}
