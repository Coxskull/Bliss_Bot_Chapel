namespace Bliss.Domain.Entities;

public sealed class WeddingPlannerBrandDna
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public int Version { get; set; }
    public int ExpectedVersion { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string BrandName { get; set; } = "";
    public string Niche { get; set; } = "";
    public string Market { get; set; } = "";
    public string Language { get; set; } = "";
    public string Colors { get; set; } = "";
    public string Font { get; set; } = "";
    public string Goals { get; set; } = "";
    public string Restrictions { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string ActorLabel { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
