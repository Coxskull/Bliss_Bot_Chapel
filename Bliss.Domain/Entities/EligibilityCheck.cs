namespace Bliss.Domain.Entities;

public class EligibilityCheck
{
    public Guid Id { get; set; }
    public Guid BlissMatchId { get; set; }
    public string CheckType { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public string? Explanation { get; set; }

    public BlissMatch BlissMatch { get; set; } = null!;
}
