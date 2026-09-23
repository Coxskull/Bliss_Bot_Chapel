namespace Bliss.Domain.Entities;

/// <summary>One configurable percentage row in an immutable compensation policy.</summary>
public class CompensationRuleAllocation
{
    public Guid Id { get; set; }
    public Guid CompensationRuleVersionId { get; set; }
    public string ParticipantRole { get; set; } = string.Empty;
    public string ParticipantLabel { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public int SortOrder { get; set; }

    public CompensationRuleVersion CompensationRuleVersion { get; set; } = null!;
}
