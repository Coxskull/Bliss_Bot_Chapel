namespace Bliss.Domain.Entities;

public class DataProvenance
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public string ConfidenceLevel { get; set; } = "UNKNOWN";
    public DateTime CollectedAt { get; set; }
    public string? Notes { get; set; }
}
