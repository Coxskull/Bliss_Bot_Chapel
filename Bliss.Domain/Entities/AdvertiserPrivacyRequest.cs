namespace Bliss.Domain.Entities;

/// <summary>
/// Minimal durable acknowledgement of an authenticated applicant's privacy-rights request.
/// The case does not itself revoke consent, delete records, or prove human fulfillment.
/// No email, narrative, password, IP address, or government identifier is stored.
/// </summary>
public sealed class AdvertiserPrivacyRequest
{
    public Guid Id { get; set; }
    public Guid RequestKey { get; set; }
    public string Subject { get; set; } = string.Empty;
    public Guid AdvertiserId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = "RECEIVED";
    public DateTime CreatedAt { get; set; }
    public string? AcknowledgedBySubject { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public Advertiser Advertiser { get; set; } = null!;
}
