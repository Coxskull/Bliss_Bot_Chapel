using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Deterministic local Wedding Planner worker. No network and no external dependency.
/// Distinct prompt packs drive Concierge text vs Brand DNA JSON responses.
/// </summary>
public sealed class LocalDeterministicWeddingPlannerAiProvider : IWeddingPlannerAiProvider
{
    public const string ProviderKey = "local-deterministic";
    public const string ModelId = "wp-local-deterministic-v1";
    public const string AdapterVersion = "wp-adapter.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public string WorkerKey => WeddingPlannerWorkers.LocalDeterministicV1;

    public int InvokeCount { get; private set; }

    public Task<WeddingPlannerAiCompletionResult> CompleteAsync(
        WeddingPlannerAiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvokeCount++;

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.Concierge, StringComparison.Ordinal)
            && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.ConciergeV1, StringComparison.Ordinal)
            && string.Equals(request.ResponseFormat, WeddingPlannerResponseFormats.Text, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CompleteConcierge(request));
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.BrandDnaInterpreter, StringComparison.Ordinal)
            && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.BrandDnaV1, StringComparison.Ordinal)
            && string.Equals(request.ResponseFormat, WeddingPlannerResponseFormats.Json, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(CompleteBrandDna(request));
        }

        throw new InvalidOperationException(
            $"Unsupported Wedding Planner provider request for role '{request.LogicalRole}' / pack '{request.PromptPackVersion}' / format '{request.ResponseFormat}'.");
    }

    private static WeddingPlannerAiCompletionResult CompleteConcierge(WeddingPlannerAiCompletionRequest request)
    {
        var lastHuman = request.Messages
            .LastOrDefault(x =>
                x.ActorType.Equals(WeddingPlannerActorTypes.Advertiser, StringComparison.OrdinalIgnoreCase)
                || x.ActorType.Equals(WeddingPlannerActorTypes.Operator, StringComparison.OrdinalIgnoreCase))
            ?.Body?.Trim() ?? string.Empty;

        var reply = string.IsNullOrWhiteSpace(lastHuman)
            ? "I am the Wedding Planner Concierge. Share your brand goals, audience, and offers so we can shape Brand DNA together."
            : $"Thank you. I captured your planning note and can help refine Brand DNA next. You said: \"{Truncate(lastHuman, 240)}\"";

        return BuildResult(reply, request);
    }

    private static WeddingPlannerAiCompletionResult CompleteBrandDna(WeddingPlannerAiCompletionRequest request)
    {
        var joined = string.Join(" ", request.Messages.Select(x => x.Body)).Trim();
        var voice = ExtractHint(joined, "voice", "warm and professional");
        var audience = ExtractHint(joined, "audience", "engaged local customers");
        var tone = ExtractHint(joined, "tone", "clear and welcoming");
        var market = ExtractHint(joined, "market", "primary service area");
        var offer = ExtractHint(joined, "offer", "core advertised services");

        var document = new BrandDnaDocumentV1(
            SchemaVersion: WeddingPlannerSchemaVersions.BrandDnaV1,
            BrandVoice: voice,
            Audience: audience,
            OffersAndServices: new[] { offer },
            Markets: new[] { market },
            Tone: tone,
            LanguageDo: new[] { "Speak plainly", "Stay benefit-led" },
            LanguageDont: new[] { "Do not overpromise outcomes", "Do not invent compliance claims" },
            ComplianceNotes: new[] { "Human approval is required before Brand DNA is current." },
            OpenQuestions: string.IsNullOrWhiteSpace(joined)
                ? new[] { "What audience should Brand DNA prioritize?", "Which offers are in scope?" }
                : Array.Empty<string>());

        var json = JsonSerializer.Serialize(document, JsonOptions);
        return BuildResult(json, request);
    }

    private static WeddingPlannerAiCompletionResult BuildResult(string content, WeddingPlannerAiCompletionRequest request)
    {
        var promptTokens = Math.Max(1, request.Messages.Sum(x => EstimateTokens(x.Body)) + EstimateTokens(request.PromptPackVersion));
        var completionTokens = Math.Max(1, EstimateTokens(content));
        var total = promptTokens + completionTokens;
        var cappedCompletion = Math.Min(completionTokens, Math.Max(1, request.MaxOutputTokens));
        var requestId = "local-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant()[..16];
        var cost = Math.Round((promptTokens * 0.00000015m) + (cappedCompletion * 0.0000006m), 6, MidpointRounding.AwayFromZero);

        return new WeddingPlannerAiCompletionResult(
            Content: content.Length > request.MaxOutputTokens * 4
                ? content[..Math.Min(content.Length, request.MaxOutputTokens * 4)]
                : content,
            ProviderKey: ProviderKey,
            ModelId: ModelId,
            AdapterVersion: AdapterVersion,
            ProviderRequestId: requestId,
            WorkerKey: WeddingPlannerWorkers.LocalDeterministicV1,
            PromptTokens: promptTokens,
            CompletionTokens: cappedCompletion,
            TotalTokens: promptTokens + cappedCompletion,
            EstimatedCostUsd: cost);
    }

    private static int EstimateTokens(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (text.Length + 3) / 4);

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    private static string ExtractHint(string corpus, string key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(corpus))
        {
            return fallback;
        }

        var marker = key + ":";
        var index = corpus.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return fallback + " informed by planning conversation";
        }

        var start = index + marker.Length;
        var end = corpus.IndexOf('.', start);
        var slice = (end < 0 ? corpus[start..] : corpus[start..end]).Trim();
        return string.IsNullOrWhiteSpace(slice) ? fallback : Truncate(slice, 160);
    }

    private sealed record BrandDnaDocumentV1(
        string SchemaVersion,
        string BrandVoice,
        string Audience,
        IReadOnlyList<string> OffersAndServices,
        IReadOnlyList<string> Markets,
        string Tone,
        IReadOnlyList<string> LanguageDo,
        IReadOnlyList<string> LanguageDont,
        IReadOnlyList<string> ComplianceNotes,
        IReadOnlyList<string> OpenQuestions);
}
