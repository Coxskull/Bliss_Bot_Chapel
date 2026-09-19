namespace Bliss.Domain.Entities;

public class RuleVersion
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<BlissMatch> BlissMatches { get; set; } = new List<BlissMatch>();
}
