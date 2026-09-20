namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable audit record for one accepted provider-neutral creator observation.
/// It stores no provider credentials and performs no external calls.
/// </summary>
public class CreatorIngestionRun
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public Guid CreatorPlatformId { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string IdentityKey { get; set; } = string.Empty;
    public string Status { get; set; } = "COMPLETED";
    public string Outcome { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string InputSnapshot { get; set; } = string.Empty;

    public Creator Creator { get; set; } = null!;
    public CreatorPlatform CreatorPlatform { get; set; } = null!;
}
