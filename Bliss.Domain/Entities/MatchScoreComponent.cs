namespace Bliss.Domain.Entities;

public class MatchScoreComponent
{
    public Guid Id { get; set; }
    public Guid BlissMatchId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public decimal? Weight { get; set; }
    public string? Explanation { get; set; }

    public BlissMatch BlissMatch { get; set; } = null!;
}
