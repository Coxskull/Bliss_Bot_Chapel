namespace Bliss.Domain.Entities;

/// <summary>An immutable participant-allocation policy. It is not a settlement instruction.</summary>
public class CompensationRuleVersion
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DocumentJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime EffectiveAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<CompensationRuleAllocation> Allocations { get; set; } =
        new List<CompensationRuleAllocation>();
    public ICollection<CompensationIllustration> Illustrations { get; set; } =
        new List<CompensationIllustration>();
}
