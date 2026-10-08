namespace Bliss.Domain.Entities;

public sealed class EvidencePackageRow
{
    public string EvidenceId { get; set; } = string.Empty;
    public string ManifestJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ParentEvidenceId { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string DriveStatus { get; set; } = string.Empty;
}
