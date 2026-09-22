namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Vendor-neutral completion contract for Wedding Planner AI workers.
/// Implementations must not become customer memory; PostgreSQL remains authoritative.
/// </summary>
public interface IWeddingPlannerAiProvider
{
    /// <summary>
    /// Stable worker identity persisted on agent runs. Known before a completion call
    /// so RUNNING rows can record the configured worker even if the call fails.
    /// </summary>
    string WorkerKey { get; }

    Task<WeddingPlannerAiCompletionResult> CompleteAsync(
        WeddingPlannerAiCompletionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WeddingPlannerAiMessage(
    string ActorType,
    string Body);

public sealed record WeddingPlannerAiCompletionRequest(
    string LogicalRole,
    string PromptPackVersion,
    IReadOnlyList<WeddingPlannerAiMessage> Messages,
    string ResponseFormat,
    int MaxOutputTokens,
    string? WorkerProfileVersion = null,
    IReadOnlyList<string>? AssignedRoles = null);

public sealed record WeddingPlannerAiCompletionResult(
    string Content,
    string ProviderKey,
    string ModelId,
    string AdapterVersion,
    string ProviderRequestId,
    string WorkerKey,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal EstimatedCostUsd);
