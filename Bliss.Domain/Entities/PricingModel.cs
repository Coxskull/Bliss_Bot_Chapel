namespace Bliss.Domain.Entities;

/// <summary>
/// A pricing basis vocabulary entry, not a price or calculation rule.
/// </summary>
public class PricingModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
