using Bliss.Domain.WeddingPlanner;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Configuration for Wedding Planner AI workers.
/// Bound from the <c>WeddingPlannerAi</c> configuration section.
/// Secrets (ApiKey) must not be logged or persisted to agent-run or audit rows.
/// </summary>
public sealed class WeddingPlannerAiOptions
{
    public const string SectionName = "WeddingPlannerAi";

    /// <summary>
    /// <see cref="WeddingPlannerAiProviderKinds.Local"/> or
    /// <see cref="WeddingPlannerAiProviderKinds.OpenAiCompatible"/>.
    /// Defaults to Local for Development/CI safety.
    /// </summary>
    public string Provider { get; set; } = WeddingPlannerAiProviderKinds.Local;

    /// <summary>
    /// Base URL of an OpenAI-compatible chat-completions API (e.g. https://api.example.com/v1).
    /// Required when Provider is OpenAiCompatible.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Bearer API key for the HTTP provider. Never log or persist this value.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Model identifier sent to the chat-completions endpoint.
    /// </summary>
    public string? ModelId { get; set; }

    /// <summary>
    /// Optional override for the worker identity stored on agent runs.
    /// Defaults to <see cref="WeddingPlannerWorkers.OpenAiCompatibleV1"/> for the HTTP adapter
    /// and <see cref="WeddingPlannerWorkers.LocalDeterministicV1"/> for the local provider.
    /// </summary>
    public string? WorkerKey { get; set; }

    /// <summary>
    /// HTTP request timeout in seconds for the OpenAI-compatible provider.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Estimated USD price per one million prompt/input tokens.
    /// </summary>
    public decimal InputPricePerMillionTokens { get; set; }

    /// <summary>
    /// Estimated USD price per one million completion/output tokens.
    /// </summary>
    public decimal OutputPricePerMillionTokens { get; set; }

    /// <summary>
    /// When true, Local provider is rejected even outside Production (startup and workshop job create).
    /// </summary>
    public bool RequireRemoteAiProvider { get; set; }
}
