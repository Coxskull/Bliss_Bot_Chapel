namespace Bliss.Domain.Entities;

/// <summary>A snapshotted participant allocation within a compensation illustration.</summary>
public class CompensationIllustrationLine
{
    public Guid Id { get; set; }
    public Guid CompensationIllustrationId { get; set; }
    public Guid CompensationRuleAllocationId { get; set; }
    public string ParticipantRole { get; set; } = string.Empty;
    public string ParticipantLabel { get; set; } = string.Empty;
    public decimal Percentage { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }

    public CompensationIllustration CompensationIllustration { get; set; } = null!;
    public CompensationRuleAllocation CompensationRuleAllocation { get; set; } = null!;
}
