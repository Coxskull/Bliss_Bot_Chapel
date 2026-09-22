namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Vendor-neutral creative asset generation contract. Returns raw PNG bytes only.
/// Implementations must not become customer memory; PostgreSQL remains authoritative.
/// Wedding Planner orchestrates this provider and is not itself an image generator.
/// </summary>
public interface IWeddingPlannerCreativeAssetProvider
{
    /// <summary>
    /// Stable worker/adapter identity known before the call so RUNNING jobs can record it
    /// even if generation fails.
    /// </summary>
    string WorkerKey { get; }

    Task<WeddingPlannerCreativeAssetGenerationResult> GeneratePngAsync(
        WeddingPlannerCreativeAssetGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WeddingPlannerCreativeAssetGenerationRequest(
    string VariantId,
    string Format,
    int Width,
    int Height,
    string RenderSpecJson,
    Guid CreativeProductionJobId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId);

public sealed record WeddingPlannerCreativeAssetGenerationResult(
    byte[] PngBytes,
    string ProviderKey,
    string AdapterVersion,
    string ProviderRequestId,
    string WorkerKey,
    string? ReceiptHeadersJson,
    decimal EstimatedCostUsd);
