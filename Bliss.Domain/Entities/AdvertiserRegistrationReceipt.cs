namespace Bliss.Domain.Entities;

/// <summary>
/// Append-only proof of the consent choices that accompanied one advertiser
/// registration request. It intentionally contains no email or password.
/// </summary>
public sealed class AdvertiserRegistrationReceipt
{
    public Guid Id { get; set; }
    public Guid RequestKey { get; set; }
    public string Subject { get; set; } = string.Empty;
    public Guid AdvertiserId { get; set; }
    public string RequestSha256 { get; set; } = string.Empty;
    public string PrivacyNoticeVersion { get; set; } = string.Empty;
    public string TermsVersion { get; set; } = string.Empty;
    public bool ProcessingConsent { get; set; }
    public bool TermsAccepted { get; set; }
    public bool MarketingOptIn { get; set; }
    public DateTime CreatedAt { get; set; }
    public Advertiser Advertiser { get; set; } = null!;
}
