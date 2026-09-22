namespace Bliss.Domain.Entities;

/// <summary>
/// Immutable draft PNG asset row for one creative package variant.
/// Bytes are never updated after insert. Package DocumentJson references asset ids only.
/// </summary>
public class WeddingPlannerCreativeAsset
{
    public Guid Id { get; set; }
    public Guid AdvertiserId { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid CreativePackageVersionId { get; set; }
    public Guid CreativeProductionJobId { get; set; }
    public string VariantId { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string ContentType { get; set; } = "image/png";
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public int ByteSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string AdapterVersion { get; set; } = string.Empty;
    public string? ProviderRequestId { get; set; }
    public string? ReceiptJson { get; set; }
    public decimal? EstimatedCostUsd { get; set; }
    public DateTime CreatedAt { get; set; }

    public Advertiser Advertiser { get; set; } = null!;
    public WeddingPlannerWorkspace Workspace { get; set; } = null!;
    public WeddingPlannerCreativePackageVersion CreativePackageVersion { get; set; } = null!;
    public WeddingPlannerCreativeProductionJob CreativeProductionJob { get; set; } = null!;
}
