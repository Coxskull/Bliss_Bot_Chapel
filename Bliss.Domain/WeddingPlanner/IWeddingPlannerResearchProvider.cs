namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Vendor-neutral source acquisition for The Curator. Returns a bounded source catalog only.
/// Does not complete AI stages and is not customer memory. The app never fetches returned URLs.
/// </summary>
public interface IWeddingPlannerResearchProvider
{
    /// <summary>
    /// Stable worker/adapter identity known before the call so RUNNING jobs can record it
    /// even if acquisition fails.
    /// </summary>
    string WorkerKey { get; }

    Task<WeddingPlannerResearchAcquisitionResult> AcquireSourcesAsync(
        WeddingPlannerResearchAcquisitionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WeddingPlannerResearchAcquisitionRequest(
    string Topic,
    string Objective,
    IReadOnlyList<string> Questions,
    string Geography,
    string Language,
    IReadOnlyList<string> AllowedDomains,
    Guid ApprovedBrandDnaVersionId,
    Guid? ApprovedColorProfileVersionId,
    string? BrandDnaSummaryFraming);

public sealed record WeddingPlannerResearchAcquisitionResult(
    string SourceCatalogJson,
    string ProviderKey,
    string AdapterVersion,
    string ProviderRequestId,
    string WorkerKey,
    decimal EstimatedCostUsd);
