namespace Bliss.Domain.Entities;

/// <summary>
/// One handoff of a qualified advertiser to a stored creator.
/// The accepted evaluator supplied the status. This is not a win.
/// </summary>
public class MarketplaceHandoffRow
{
    public Guid Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public Guid RuleVersionId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string MatchStatus { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public bool EvaluatorInvoked { get; set; }
    public bool WinClaimed { get; set; }
    public string Notice { get; set; } = string.Empty;
    public string Delivery { get; set; } = "NOT_SENT";
    public DateTime RecordedAt { get; set; }
}
