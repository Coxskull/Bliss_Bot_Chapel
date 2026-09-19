namespace Bliss.Domain.Entities;

public class AdvertiserProgram
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ExternalProgramId { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public Advertiser Advertiser { get; set; } = null!;
    public ICollection<AdvertiserOpportunity> Opportunities { get; set; } = new List<AdvertiserOpportunity>();
    public ICollection<ProgramAccess> ProgramAccesses { get; set; } = new List<ProgramAccess>();
}
