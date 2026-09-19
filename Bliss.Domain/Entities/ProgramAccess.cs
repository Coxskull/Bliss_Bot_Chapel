namespace Bliss.Domain.Entities;

public class ProgramAccess
{
    public Guid Id { get; set; }
    public Guid AdvertiserProgramId { get; set; }
    public string Status { get; set; } = "UNKNOWN";
    public DateTime? ApprovedAt { get; set; }

    public AdvertiserProgram AdvertiserProgram { get; set; } = null!;
}
