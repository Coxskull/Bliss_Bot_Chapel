namespace Bliss.Domain.Entities;

/// <summary>Issuer subject bound to one creator record created through self-registration.</summary>
public sealed class CreatorAccountMembership
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
    public string SubmittedChannelUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING_REVIEW";
    public DateTime CreatedAt { get; set; }
    public Creator Creator { get; set; } = null!;
}
