namespace Bliss.Domain.Entities;

/// <summary>
/// One prospect row in the Bliss database. The payload keeps the prospect
/// document. Media bytes stay on disk.
/// </summary>
public class ProspectMemory
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string ProspectState { get; set; } = string.Empty;
    public int OpportunityScore { get; set; }
    public bool Suppressed { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Clip metadata in the same database. The video file stays on disk.</summary>
public class SourceClipMemory
{
    public Guid Id { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int QuotaCredit { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A factory batch row in the same database.</summary>
public class FactoryBatchMemory
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int AiCalls { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
