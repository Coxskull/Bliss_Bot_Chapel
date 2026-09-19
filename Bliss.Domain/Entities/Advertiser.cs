namespace Bliss.Domain.Entities;

public class Advertiser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? CountryCode { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<AdvertiserProgram> Programs { get; set; } = new List<AdvertiserProgram>();
    public ICollection<NetworkAccess> NetworkAccesses { get; set; } = new List<NetworkAccess>();
}
