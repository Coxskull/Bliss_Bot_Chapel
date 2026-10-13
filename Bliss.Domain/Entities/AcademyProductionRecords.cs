using System.Text.Json.Serialization;

namespace Bliss.Domain.Entities;

public sealed class AcademyProductionBrief
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid BrandDnaId { get; set; }
    public string Family { get; set; } = "";
    public string Headline { get; set; } = "";
    public string Cta { get; set; } = "";
    public string ReferenceId { get; set; } = "";
    public string ReferenceSha256 { get; set; } = "";
    public string ReferenceSnapshotJson { get; set; } = "";
    public string BrandSnapshotJson { get; set; } = "";
    public string ProductionBriefJson { get; set; } = "";
    public string RecipeSha256 { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class AcademyProductionCandidate
{
    public Guid Id { get; set; }
    public Guid BriefId { get; set; }
    public int Version { get; set; }
    public int ExpectedVersion { get; set; }
    public string Label { get; set; } = "";
    public string Notes { get; set; } = "";
    public string ImageSha256 { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    [JsonIgnore] public byte[] ImageBytes { get; set; } = [];
    public string IdempotencyKey { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class AcademyProductionReview
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public int Version { get; set; }
    public int ExpectedVersion { get; set; }
    public string Decision { get; set; } = "";
    public string RequestSha256 { get; set; } = "";
    public string Customization { get; set; } = "";
    public string QualityParity { get; set; } = "";
    public string Originality { get; set; } = "";
    public string GeographicAuthenticity { get; set; } = "";
    public bool CriticalDefect { get; set; }
    public string Rationale { get; set; } = "";
    public string Preserve { get; set; } = "";
    public string Repair { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
