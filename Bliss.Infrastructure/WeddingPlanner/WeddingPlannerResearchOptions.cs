using Bliss.Domain.WeddingPlanner;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Configuration for Wedding Planner research source acquisition.
/// Bound from the <c>WeddingPlannerResearch</c> configuration section.
/// Secrets (ApiKey) must not be logged or persisted to jobs, runs, reports, or audit rows.
/// </summary>
public sealed class WeddingPlannerResearchOptions
{
    public const string SectionName = "WeddingPlannerResearch";

    /// <summary>
    /// <see cref="WeddingPlannerResearchProviderKinds.Local"/> or
    /// <see cref="WeddingPlannerResearchProviderKinds.RemoteHttp"/>.
    /// Defaults to Local for Development/CI safety.
    /// </summary>
    public string Provider { get; set; } = WeddingPlannerResearchProviderKinds.Local;

    /// <summary>
    /// Base URL of the remote research JSON API. Required when Provider is RemoteHttp.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Bearer API key for the HTTP provider. Never log or persist this value.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional override for the research worker identity stored on jobs.
    /// </summary>
    public string? WorkerKey { get; set; }

    /// <summary>
    /// HTTP request timeout in seconds for the RemoteHttp provider.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// When true, Local provider is rejected even outside Production (startup and job create).
    /// </summary>
    public bool RequireRemoteResearchProvider { get; set; }

    /// <summary>
    /// Flat estimated USD cost recorded for Local acquisitions (synthetic evidence only).
    /// </summary>
    public decimal LocalEstimatedCostUsd { get; set; }

    /// <summary>
    /// Flat estimated USD cost recorded for RemoteHttp acquisitions when the response omits cost.
    /// </summary>
    public decimal RemoteEstimatedCostUsd { get; set; }
}
