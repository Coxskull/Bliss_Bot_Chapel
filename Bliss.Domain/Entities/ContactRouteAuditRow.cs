namespace Bliss.Domain.Entities;

/// <summary>
/// One explicit transmission request. The row records the request.
/// Transmission stays NOT_SENT.
/// </summary>
public class ContactRouteAuditRow
{
    public Guid Id { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Authorization { get; set; } = string.Empty;
    public string Adapter { get; set; } = string.Empty;
    public string Transmission { get; set; } = "NOT_SENT";
    public string Notice { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
