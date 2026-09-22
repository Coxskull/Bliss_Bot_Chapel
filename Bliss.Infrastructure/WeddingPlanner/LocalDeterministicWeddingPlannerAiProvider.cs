using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
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
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
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

        if (IsCuratorStage(request))
        {
            return Task.FromResult(CompleteCuratorStage(request));
        }

        throw new InvalidOperationException(
            $"Unsupported Wedding Planner provider request for role '{request.LogicalRole}' / pack '{request.PromptPackVersion}' / format '{request.ResponseFormat}'.");
    }

    private static bool IsCuratorStage(WeddingPlannerAiCompletionRequest request)
    {
        if (!string.Equals(request.ResponseFormat, WeddingPlannerResponseFormats.Json, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CuratorResearch, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.CuratorResearchV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCuratorWorkerProfiles.ResearchV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CuratorEvidence, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.CuratorEvidenceV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCuratorWorkerProfiles.EvidenceV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CuratorSynthesisRisk, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.CuratorSynthesisRiskV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1, StringComparison.Ordinal));
    }

    private static WeddingPlannerAiCompletionResult CompleteCuratorStage(WeddingPlannerAiCompletionRequest request)
    {
        var profile = request.WorkerProfileVersion
            ?? throw new InvalidOperationException("Curator stage requires WorkerProfileVersion.");
        var assigned = request.AssignedRoles?.ToList()
            ?? WeddingPlannerCuratorWorkerProfiles.AssignedRoles(profile).ToList();
        var expected = WeddingPlannerCuratorWorkerProfiles.AssignedRoles(profile);
        if (assigned.Count != expected.Count || !expected.All(assigned.Contains))
        {
            throw new InvalidOperationException("Curator AssignedRoles must match the worker profile mapping.");
        }

        var context = string.Join("\n", request.Messages.Select(x => x.Body));
        var sourceIds = ExtractSourceIds(context);
        if (sourceIds.Count == 0)
        {
            sourceIds = ["src_1"];
        }

        var primary = sourceIds[0];
        var contributions = new JsonArray();
        foreach (var role in expected)
        {
            contributions.Add(BuildSyntheticContribution(role, profile, primary, sourceIds));
        }

        // Synthesis stage also embeds a synthesis block hint in the RESEARCH_SYNTHESIZER summary
        // so the orchestrator can build executive summary deterministically from stage output + context.
        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CuratorWorkerOutputV1,
            ["workerProfileVersion"] = profile,
            ["contributions"] = contributions
        };

        var json = document.ToJsonString(NodeWriteOptions);
        return BuildResult(json, request);
    }

    private static JsonObject BuildSyntheticContribution(
        string role,
        string profile,
        string primarySourceId,
        IReadOnlyList<string> sourceIds)
    {
        var second = sourceIds.Count > 1 ? sourceIds[1] : primarySourceId;
        return role switch
        {
            WeddingPlannerCuratorLogicalRoles.MarketLandscapeResearcher => Contribution(
                role,
                "SYNTHETIC market landscape summary from fixture catalog.",
                Finding(WeddingPlannerFindingTypes.Fact, "SYNTHETIC: fixture market demand signal observed in catalog.", 0.72, primarySourceId)),
            WeddingPlannerCuratorLogicalRoles.AudienceContextResearcher => Contribution(
                role,
                "SYNTHETIC audience context summary from fixture catalog.",
                Finding(WeddingPlannerFindingTypes.Inference, "SYNTHETIC: audience preference inferred from fixture sources.", 0.64, second)),
            WeddingPlannerCuratorLogicalRoles.CompetitorSignalsResearcher => Contribution(
                role,
                "SYNTHETIC competitor signals summary from fixture catalog.",
                Finding(WeddingPlannerFindingTypes.Fact, "SYNTHETIC: competitor messaging pattern noted in fixtures.", 0.61, primarySourceId)),
            WeddingPlannerCuratorLogicalRoles.ChannelFormatResearcher => Contribution(
                role,
                "SYNTHETIC channel/format summary from fixture catalog.",
                Finding(WeddingPlannerFindingTypes.Inference, "SYNTHETIC: short-form video appears relevant in fixtures.", 0.58, second)),
            WeddingPlannerCuratorLogicalRoles.EvidenceAnalyst => Contribution(
                role,
                "SYNTHETIC evidence analysis of fixture catalog consistency.",
                Finding(WeddingPlannerFindingTypes.Fact, "SYNTHETIC: cited fixture sources are internally consistent.", 0.7, primarySourceId)),
            WeddingPlannerCuratorLogicalRoles.SourceVerifier => Contribution(
                role,
                "SYNTHETIC source metadata verification (no live URL fetch).",
                Finding(WeddingPlannerFindingTypes.Fact, "SYNTHETIC: source metadata verified for schema and allowlist rules only.", 0.8, primarySourceId),
                Finding(WeddingPlannerFindingTypes.Gap, "SYNTHETIC: live URL content was not fetched or certified.", 0.9)),
            WeddingPlannerCuratorLogicalRoles.ClaimsRiskReviewer => Contribution(
                role,
                "SYNTHETIC claims/risk review of fixture findings.",
                Finding(WeddingPlannerFindingTypes.Risk, "SYNTHETIC: overclaiming live facts from fixtures is a risk.", 0.75, primarySourceId)),
            WeddingPlannerCuratorLogicalRoles.ResearchSynthesizer => Contribution(
                role,
                "SYNTHETIC executive synthesis of fixture Curator stages. Open questions remain about live validation.",
                Finding(WeddingPlannerFindingTypes.Inference, "SYNTHETIC: fixture findings support a provisional planning brief only.", 0.66, primarySourceId),
                Finding(WeddingPlannerFindingTypes.Gap, "SYNTHETIC: production remote sources were not used in this local run.", 0.85)),
            _ => throw new InvalidOperationException($"Unsupported Curator role '{role}' for profile '{profile}'.")
        };
    }

    private static JsonObject Contribution(string role, string summary, params JsonObject[] findings) =>
        new()
        {
            ["logicalRole"] = role,
            ["summary"] = summary,
            ["findings"] = new JsonArray(findings.Cast<JsonNode>().ToArray())
        };

    private static JsonObject Finding(string type, string statement, double confidence, params string[] citationIds)
    {
        var obj = new JsonObject
        {
            ["type"] = type,
            ["statement"] = statement,
            ["confidence"] = confidence,
            ["citationSourceIds"] = new JsonArray(citationIds.Select(id => (JsonNode)id!).ToArray())
        };
        return obj;
    }

    private static List<string> ExtractSourceIds(string context)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        // Match catalog source ids like "src_1" appearing in JSON context.
        for (var i = 0; i < context.Length - 4; i++)
        {
            if (context.AsSpan(i).StartsWith("\"id\":\"src_", StringComparison.Ordinal)
                || context.AsSpan(i).StartsWith("\"id\": \"src_", StringComparison.Ordinal))
            {
                var start = context.IndexOf("src_", i, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }

                var end = start;
                while (end < context.Length && (char.IsAsciiLetterOrDigit(context[end]) || context[end] == '_'))
                {
                    end++;
                }

                var id = context[start..end];
                if (seen.Add(id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
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
