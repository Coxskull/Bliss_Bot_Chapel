using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bliss.Domain.WeddingPlanner;
using Microsoft.Extensions.Options;

namespace Bliss.Infrastructure.WeddingPlanner;

/// <summary>
/// Vendor-neutral HTTP adapter for OpenAI-compatible chat-completions endpoints.
/// Uses <see cref="HttpClient"/> only — no vendor SDK. Secrets are never included in thrown messages.
/// </summary>
public sealed class OpenAiCompatibleWeddingPlannerAiProvider : IWeddingPlannerAiProvider
{
    public const string ProviderKey = "openai-compatible";
    public const string AdapterVersion = "wp-openai-compatible-adapter.v1";
    public const string HttpClientName = "WeddingPlannerOpenAiCompatible";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly WeddingPlannerAiOptions _options;

    public OpenAiCompatibleWeddingPlannerAiProvider(
        HttpClient http,
        IOptions<WeddingPlannerAiOptions> options)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string WorkerKey =>
        string.IsNullOrWhiteSpace(_options.WorkerKey)
            ? WeddingPlannerWorkers.OpenAiCompatibleV1
            : _options.WorkerKey.Trim();

    public async Task<WeddingPlannerAiCompletionResult> CompleteAsync(
        WeddingPlannerAiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateConfiguration();

        var systemInstruction = ResolveSystemInstruction(request);
        var payload = BuildRequestPayload(request, systemInstruction);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = Math.Clamp(_options.TimeoutSeconds <= 0 ? 60 : _options.TimeoutSeconds, 1, 600);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey!.Trim());
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeddingPlannerAiProviderException(
                "Wedding Planner AI provider request timed out.",
                "PROVIDER_TIMEOUT");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new WeddingPlannerAiProviderException(
                "Wedding Planner AI provider transport failed.",
                "PROVIDER_TRANSPORT",
                ex);
        }

        using (response)
        {
            var rawBody = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new WeddingPlannerAiProviderException(
                    $"Wedding Planner AI provider returned HTTP {(int)response.StatusCode}.",
                    "PROVIDER_HTTP_" + (int)response.StatusCode);
            }

            ChatCompletionResponse? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(rawBody, JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new WeddingPlannerAiProviderException(
                    "Wedding Planner AI provider returned an invalid JSON payload.",
                    "PROVIDER_INVALID_PAYLOAD",
                    ex);
            }

            var content = parsed?.Choices?
                .Select(c => c.Message?.Content)
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new WeddingPlannerAiProviderException(
                    "Wedding Planner AI provider returned no completion content.",
                    "PROVIDER_EMPTY_CONTENT");
            }

            var promptTokens = Math.Max(0, parsed!.Usage?.PromptTokens ?? 0);
            var completionTokens = Math.Max(0, parsed.Usage?.CompletionTokens ?? 0);
            var totalTokens = parsed.Usage?.TotalTokens is int total && total > 0
                ? total
                : promptTokens + completionTokens;

            var estimatedCost = EstimateCost(promptTokens, completionTokens);
            var modelId = string.IsNullOrWhiteSpace(parsed.Model)
                ? (_options.ModelId ?? "unknown")
                : parsed.Model!;
            var providerRequestId = string.IsNullOrWhiteSpace(parsed.Id)
                ? Guid.NewGuid().ToString("N")
                : parsed.Id!;

            return new WeddingPlannerAiCompletionResult(
                Content: content.Trim(),
                ProviderKey: ProviderKey,
                ModelId: Truncate(modelId, 128),
                AdapterVersion: AdapterVersion,
                ProviderRequestId: Truncate(providerRequestId, 128),
                WorkerKey: WorkerKey,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                TotalTokens: totalTokens,
                EstimatedCostUsd: estimatedCost);
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new WeddingPlannerAiProviderException(
                "WeddingPlannerAi:BaseUrl is required for OpenAiCompatible provider.",
                "PROVIDER_CONFIG");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new WeddingPlannerAiProviderException(
                "WeddingPlannerAi:ApiKey is required for OpenAiCompatible provider.",
                "PROVIDER_CONFIG");
        }

        if (string.IsNullOrWhiteSpace(_options.ModelId))
        {
            throw new WeddingPlannerAiProviderException(
                "WeddingPlannerAi:ModelId is required for OpenAiCompatible provider.",
                "PROVIDER_CONFIG");
        }
    }

    private static string ResolveSystemInstruction(WeddingPlannerAiCompletionRequest request)
    {
        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.Concierge, StringComparison.Ordinal))
        {
            return
                "You are the Wedding Planner Concierge. Help the advertiser clarify brand voice, audience, " +
                "offers, markets, and tone. Ask clarifying questions when needed. You cannot approve Brand DNA, " +
                "change matching, or invent compliance claims. Stay concise and practical. Prompt pack: " +
                request.PromptPackVersion + ".";
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.BrandDnaInterpreter, StringComparison.Ordinal))
        {
            return
                "You are the Wedding Planner Brand DNA Interpreter. Emit a single JSON object for schema " +
                "brand-dna.v1 with keys: schemaVersion, brandVoice, audience, offersAndServices, markets, tone, " +
                "languageDo, languageDont, complianceNotes, openQuestions. Do not approve Brand DNA. Do not " +
                "include Color Intelligence, creative concepts, assets, or match data. Prompt pack: " +
                request.PromptPackVersion + ".";
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CuratorResearch, StringComparison.Ordinal))
        {
            return BuildCuratorInstruction(
                request,
                WeddingPlannerCuratorWorkerProfiles.ResearchV1,
                "Produce market, audience, competitor, and channel findings only from the provided source catalog and brief. Brand DNA is framing only and is never evidence.");
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CuratorEvidence, StringComparison.Ordinal))
        {
            return BuildCuratorInstruction(
                request,
                WeddingPlannerCuratorWorkerProfiles.EvidenceV1,
                "Analyze and verify source metadata/internal consistency only. Never fetch live URL content. Brand DNA is never evidence.");
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CuratorSynthesisRisk, StringComparison.Ordinal))
        {
            return BuildCuratorInstruction(
                request,
                WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1,
                "Review claim risk and synthesize prior stage contributions. Do not approve the research report. Never fetch live URL content.");
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.ConceptStrategy, StringComparison.Ordinal))
        {
            return BuildWorkshopInstruction(
                request,
                WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1,
                WeddingPlannerSchemaVersions.ConceptStrategyWorkerOutputV1,
                "Emit strategy only for exactly concept_1, concept_2, concept_3 with name and rationale. No visual direction, copy, claims, or prototype specs.");
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.ConceptCreative, StringComparison.Ordinal))
        {
            return BuildWorkshopInstruction(
                request,
                WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1,
                WeddingPlannerSchemaVersions.ConceptCreativeWorkerOutputV1,
                "Emit ART_DIRECTOR and COPYWRITER contributions for the same three concept ids. copy.kind must be CREATIVE_NON_FACTUAL. factualClaims sourceIds must come only from the pinned research report catalog. paletteRoleRefs must exist on the pinned color profile. Brand DNA and color ids are never factual sources.");
        }

        if (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.PrototypeProduction, StringComparison.Ordinal))
        {
            return BuildWorkshopInstruction(
                request,
                WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1,
                WeddingPlannerSchemaVersions.PrototypeProductionWorkerOutputV1,
                "Emit PRODUCTION_ARTIST prototypes only as prototype-spec.v1 for concept_1–concept_3. textRef may only be copy.headline, copy.body, or copy.cta. No html/css/svg/script/src/url/href/base64/image bytes. No image generation.");
        }

        throw new WeddingPlannerAiProviderException(
            $"Unsupported Wedding Planner logical role '{SanitizeForError(request.LogicalRole)}'.",
            "PROVIDER_UNSUPPORTED_ROLE");
    }

    private static string BuildWorkshopInstruction(
        WeddingPlannerAiCompletionRequest request,
        string expectedProfile,
        string schemaVersion,
        string stageRule)
    {
        var profile = string.IsNullOrWhiteSpace(request.WorkerProfileVersion)
            ? expectedProfile
            : request.WorkerProfileVersion.Trim();
        var roles = request.AssignedRoles is { Count: > 0 }
            ? request.AssignedRoles
            : WeddingPlannerConceptWorkshopWorkerProfiles.AssignedRoles(expectedProfile);
        var roleList = string.Join(", ", roles);

        return
            "You are a Wedding Planner Concept Workshop stage worker. Emit a single JSON object for schema " +
            schemaVersion + " with keys: schemaVersion, workerProfileVersion, contributions " +
            "(optional marker). Unknown fields are forbidden. Never include html, css, svg, script, src, url, href, " +
            "base64, or image bytes in any field. " +
            $"workerProfileVersion must be {profile}. contributions must include exactly these logicalRole values " +
            $"and no others: {roleList}. Concept ids must be exactly concept_1, concept_2, concept_3. " +
            stageRule +
            " You cannot approve concept packages, select winning concepts, generate images, fetch URLs, change matching, " +
            "or invent live market facts beyond pinned research source ids. Prompt pack: " + request.PromptPackVersion + ".";
    }

    private static string BuildCuratorInstruction(
        WeddingPlannerAiCompletionRequest request,
        string expectedProfile,
        string stageRule)
    {
        var profile = string.IsNullOrWhiteSpace(request.WorkerProfileVersion)
            ? expectedProfile
            : request.WorkerProfileVersion.Trim();
        var roles = request.AssignedRoles is { Count: > 0 }
            ? request.AssignedRoles
            : WeddingPlannerCuratorWorkerProfiles.AssignedRoles(expectedProfile);
        var roleList = string.Join(", ", roles);

        return
            "You are a Wedding Planner Curator stage worker. Emit a single JSON object for schema " +
            "curator-worker-output.v1 with keys: schemaVersion, workerProfileVersion, contributions. " +
            $"workerProfileVersion must be {profile}. contributions must include exactly these logicalRole values " +
            $"and no others: {roleList}. Each contribution needs summary and findings[]. Finding type is " +
            "FACT|INFERENCE|GAP|RISK; confidence is in [0,1]; FACT/INFERENCE/RISK require known citationSourceIds " +
            "from the provided catalog; GAP may be uncited. Unknown/dangling citations are forbidden. " +
            stageRule +
            " You cannot approve research reports, change matching, fetch citation URLs, or invent live facts " +
            "beyond the provided catalog. Prompt pack: " + request.PromptPackVersion + ".";
    }

    private ChatCompletionRequest BuildRequestPayload(
        WeddingPlannerAiCompletionRequest request,
        string systemInstruction)
    {
        var messages = new List<ChatMessageDto>(capacity: request.Messages.Count + 1)
        {
            new() { Role = "system", Content = systemInstruction }
        };

        foreach (var message in request.Messages)
        {
            messages.Add(new ChatMessageDto
            {
                Role = MapRole(message.ActorType),
                Content = message.Body ?? string.Empty
            });
        }

        var wantsJson = string.Equals(
            request.ResponseFormat,
            WeddingPlannerResponseFormats.Json,
            StringComparison.OrdinalIgnoreCase);

        return new ChatCompletionRequest
        {
            Model = _options.ModelId!.Trim(),
            Messages = messages,
            MaxTokens = Math.Max(1, request.MaxOutputTokens),
            ResponseFormat = wantsJson ? new ResponseFormatDto { Type = "json_object" } : null
        };
    }

    private static string MapRole(string actorType)
    {
        if (actorType.Equals(WeddingPlannerActorTypes.Planner, StringComparison.OrdinalIgnoreCase)
            || actorType.Equals("assistant", StringComparison.OrdinalIgnoreCase))
        {
            return "assistant";
        }

        if (actorType.Equals(WeddingPlannerActorTypes.System, StringComparison.OrdinalIgnoreCase)
            || actorType.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            return "system";
        }

        return "user";
    }

    private decimal EstimateCost(int promptTokens, int completionTokens)
    {
        var input = (_options.InputPricePerMillionTokens / 1_000_000m) * promptTokens;
        var output = (_options.OutputPricePerMillionTokens / 1_000_000m) * completionTokens;
        return Math.Round(input + output, 6, MidpointRounding.AwayFromZero);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string SanitizeForError(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, 64));
        foreach (var ch in value.Take(64))
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '_');
        }

        return builder.ToString();
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessageDto> Messages { get; set; } = new();

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("response_format")]
        public ResponseFormatDto? ResponseFormat { get; set; }
    }

    private sealed class ResponseFormatDto
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
    }

    private sealed class ChatMessageDto
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("choices")]
        public List<ChatChoice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public ChatUsage? Usage { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")]
        public ChatMessageContent? Message { get; set; }
    }

    private sealed class ChatMessageContent
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }

        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }
}

/// <summary>
/// Bounded, secret-safe provider failure. Messages must never include API keys or raw credential material.
/// </summary>
public sealed class WeddingPlannerAiProviderException : Exception
{
    public string ErrorCode { get; }

    public WeddingPlannerAiProviderException(string message, string errorCode, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}
