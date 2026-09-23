namespace Bliss.Domain.Entities;

public class CreatorPlatform
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string? ExternalProfileId { get; set; }
    public string? IdentityKey { get; set; }
    public string? ProfileUrl { get; set; }
    /// <summary>
    /// Platform follower count when known. Input to future Economics snapshots;
    /// not a rate and not a linear valuation multiplier.
    /// </summary>
    public int? Followers { get; set; }
    public DateTime? LastCollectedAt { get; set; }

    public Creator Creator { get; set; } = null!;
}
