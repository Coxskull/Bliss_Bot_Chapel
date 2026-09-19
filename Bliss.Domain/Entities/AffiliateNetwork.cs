namespace Bliss.Domain.Entities;

public class AffiliateNetwork
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public ICollection<NetworkAccess> NetworkAccesses { get; set; } = new List<NetworkAccess>();
}
