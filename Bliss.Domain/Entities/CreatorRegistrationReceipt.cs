namespace Bliss.Domain.Entities;

/// <summary>Append-only record of a creator's registration and consent choices.</summary>
public sealed class CreatorRegistrationReceipt
{
    public Guid Id { get; set; }
    public Guid RequestKey { get; set; }
    public string Subject { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
    public string RequestSha256 { get; set; } = string.Empty;
    public string PrivacyNoticeVersion { get; set; } = string.Empty;
    public string TermsVersion { get; set; } = string.Empty;
    public bool ProcessingConsent { get; set; }
    public bool TermsAccepted { get; set; }
    public bool MarketingOptIn { get; set; }
    public DateTime CreatedAt { get; set; }
    public Creator Creator { get; set; } = null!;
}
