using Bliss.Domain.WeddingPlanner;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Configuration for Wedding Planner creative asset generation.
/// Bound from the <c>WeddingPlannerCreativeAsset</c> configuration section.
/// Secrets (ApiKey) must not be logged or persisted to jobs, assets, or audit rows.
/// </summary>
public sealed class WeddingPlannerCreativeAssetOptions
{
    public const string SectionName = "WeddingPlannerCreativeAsset";

    /// <summary>
    /// <see cref="WeddingPlannerCreativeAssetProviderKinds.Local"/> or
    /// <see cref="WeddingPlannerCreativeAssetProviderKinds.RemoteHttp"/>.
    /// Defaults to Local for Development/CI safety.
    /// </summary>
    public string Provider { get; set; } = WeddingPlannerCreativeAssetProviderKinds.Local;

    /// <summary>
    /// Base URL of the remote creative-asset PNG API. Required when Provider is RemoteHttp.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Bearer API key for the HTTP provider. Never log or persist this value.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional override for the asset worker identity stored on jobs/assets.
    /// </summary>
    public string? WorkerKey { get; set; }

    /// <summary>
    /// HTTP request timeout in seconds for the RemoteHttp provider.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// When true, Local provider is rejected even outside Production (startup and job create).
    /// </summary>
    public bool RequireRemoteCreativeAssetProvider { get; set; }

    /// <summary>
    /// Flat estimated USD cost recorded for Local generations (synthetic evidence only).
    /// </summary>
    public decimal LocalEstimatedCostUsd { get; set; }

    /// <summary>
    /// Flat estimated USD cost recorded for RemoteHttp generations when the response omits cost.
    /// </summary>
    public decimal RemoteEstimatedCostUsd { get; set; }
}
