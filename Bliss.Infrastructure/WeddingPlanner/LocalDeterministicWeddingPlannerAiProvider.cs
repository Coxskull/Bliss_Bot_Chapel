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

        if (IsWorkshopStage(request))
        {
            return Task.FromResult(CompleteWorkshopStage(request));
        }

        if (IsCreativeDepartmentStage(request))
        {
            return Task.FromResult(CompleteCreativeDepartmentStage(request));
        }

        throw new InvalidOperationException(
            $"Unsupported Wedding Planner provider request for role '{request.LogicalRole}' / pack '{request.PromptPackVersion}' / format '{request.ResponseFormat}'.");
    }

    private static bool IsCreativeDepartmentStage(WeddingPlannerAiCompletionRequest request)
    {
        if (!string.Equals(request.ResponseFormat, WeddingPlannerResponseFormats.Json, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CreativeDirection, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.CreativeDirectionV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.StrategyAdaptation, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.StrategyAdaptationV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.VisualSystem, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.VisualSystemV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.ImageDirection, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.ImageDirectionV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.CopySystem, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.CopySystemV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.VariantProduction, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.VariantProductionV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1, StringComparison.Ordinal));
    }

    private static WeddingPlannerAiCompletionResult CompleteCreativeDepartmentStage(WeddingPlannerAiCompletionRequest request)
    {
        var profile = request.WorkerProfileVersion
            ?? throw new InvalidOperationException("Creative department stage requires WorkerProfileVersion.");
        var assigned = request.AssignedRoles?.ToList()
            ?? WeddingPlannerCreativeDepartmentWorkerProfiles.AssignedRoles(profile).ToList();
        var expected = WeddingPlannerCreativeDepartmentWorkerProfiles.AssignedRoles(profile);
        if (assigned.Count != expected.Count || !expected.All(assigned.Contains))
        {
            throw new InvalidOperationException("Creative department AssignedRoles must match the worker profile mapping.");
        }

        var context = string.Join("\n", request.Messages.Select(x => x.Body));
        var selectedConceptId = ExtractSelectedConceptId(context);
        var formats = ExtractFormats(context);
        if (formats.Count == 0)
        {
            formats = [WeddingPlannerChannelFormats.StaticSocialSquare];
        }

        var variantCount = ExtractRequestedVariantCount(context, formats.Count);
        var paletteRoles = ExtractPaletteRoles(context);
        if (paletteRoles.Count == 0)
        {
            paletteRoles = ["primary", "secondary", "accent", "background"];
        }

        var preservedClaims = ExtractPreservedClaims(context);

        var document = profile switch
        {
            WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1 =>
                BuildCreativeDirectionOutput(selectedConceptId),
            WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1 =>
                BuildStrategyAdaptationOutput(selectedConceptId, formats),
            WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1 =>
                BuildVisualSystemOutput(selectedConceptId, paletteRoles),
            WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1 =>
                BuildImageDirectionOutput(selectedConceptId, variantCount),
            WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1 =>
                BuildCopySystemOutput(selectedConceptId, variantCount, preservedClaims),
            WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1 =>
                BuildVariantProductionOutput(selectedConceptId, formats, variantCount, paletteRoles),
            _ => throw new InvalidOperationException($"Unsupported creative department profile '{profile}'.")
        };

        return BuildResult(document.ToJsonString(NodeWriteOptions), request);
    }

    private static string ExtractSelectedConceptId(string context)
    {
        foreach (var id in WeddingPlannerConceptIds.All)
        {
            if (context.Contains($"\"selectedConceptId\":\"{id}\"", StringComparison.Ordinal)
                || context.Contains($"selectedConceptId\": \"{id}\"", StringComparison.Ordinal)
                || context.Contains($"SelectedConceptId: {id}", StringComparison.Ordinal))
            {
                return id;
            }
        }

        return WeddingPlannerConceptIds.Concept1;
    }

    private static List<string> ExtractFormats(string context)
    {
        var formats = new List<string>();
        foreach (var format in WeddingPlannerChannelFormats.All)
        {
            if (context.Contains(format, StringComparison.Ordinal))
            {
                formats.Add(format);
            }
        }

        return formats;
    }

    private static int ExtractRequestedVariantCount(string context, int minFormats)
    {
        const string marker = "\"requestedVariantCount\":";
        var idx = context.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var slice = context[(idx + marker.Length)..];
            var digits = new string(slice.TakeWhile(c => char.IsDigit(c) || char.IsWhiteSpace(c)).ToArray()).Trim();
            if (int.TryParse(digits, out var n) && n is >= 1 and <= 4)
            {
                return Math.Max(n, minFormats);
            }
        }

        return Math.Clamp(Math.Max(minFormats, 2), 1, 4);
    }

    private static List<(string Statement, List<string> SourceIds)> ExtractPreservedClaims(string context)
    {
        var claims = new List<(string, List<string>)>();
        const string marker = "\"factualClaims\":";
        var idx = context.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return claims;
        }

        // Local fixtures preserve at most one well-known claim when present in context.
        if (context.Contains("Couples often research venues months ahead.", StringComparison.Ordinal))
        {
            var sourceIds = ExtractSourceIds(context);
            if (sourceIds.Count == 0)
            {
                sourceIds = ["src_1"];
            }

            claims.Add(("Couples often research venues months ahead.", sourceIds.Take(1).ToList()));
        }

        return claims;
    }

    private static JsonObject BuildCreativeDirectionOutput(string selectedConceptId) =>
        new()
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CreativeDirectionWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1,
            ["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
            ["selectedConceptId"] = selectedConceptId,
            ["contributions"] = new JsonArray(
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.CreativeDirector,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — north star for the selected concept.",
                    ["direction"] = new JsonObject
                    {
                        ["northStar"] = "Quiet confidence rendered as draft production artwork.",
                        ["principles"] = new JsonArray("generous negative space", "one focal CTA")
                    }
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.CampaignStrategist,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — campaign framing without campaign ids.",
                    ["framing"] = new JsonObject
                    {
                        ["objectiveEcho"] = "Advance the selected concept as draft creative for review.",
                        ["formatPlanNotes"] = "Cover requested formats with consistent voice."
                    }
                })
        };

    private static JsonObject BuildStrategyAdaptationOutput(string selectedConceptId, IReadOnlyList<string> formats) =>
        new()
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.StrategyAdaptationWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1,
            ["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
            ["selectedConceptId"] = selectedConceptId,
            ["contributions"] = new JsonArray(
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.AudienceStrategist,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — audience adaptation.",
                    ["audienceAdaptation"] = new JsonObject
                    {
                        ["notes"] = "Speak to couples seeking calm, confident planning guidance."
                    }
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.OfferStrategist,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — offer framing (non-factual).",
                    ["offerFraming"] = new JsonObject
                    {
                        ["notes"] = "Frame the next planning step without inventing product facts."
                    }
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.ChannelStrategist,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — channel/format adaptation.",
                    ["channelAdaptation"] = new JsonObject
                    {
                        ["formats"] = new JsonArray(formats.Select(f => (JsonNode)f).ToArray())
                    }
                })
        };

    private static JsonObject BuildVisualSystemOutput(string selectedConceptId, IReadOnlyList<string> paletteRoles)
    {
        var refs = paletteRoles.Take(3).ToList();
        if (refs.Count == 0)
        {
            refs = ["primary", "background", "accent"];
        }

        return new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.VisualSystemWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1,
            ["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
            ["selectedConceptId"] = selectedConceptId,
            ["contributions"] = new JsonArray(
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.VisualDesigner,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — visual system.",
                    ["visualSystem"] = new JsonObject
                    {
                        ["paletteRoleRefs"] = new JsonArray(refs.Select(r => (JsonNode)r).ToArray()),
                        ["atmosphere"] = "Soft natural light with restrained contrast."
                    }
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.LayoutDesigner,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — layout system.",
                    ["layoutSystem"] = new JsonObject
                    {
                        ["geometryLanguage"] = "centered stack with CTA footer band"
                    }
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.TypographyDesigner,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — typography system.",
                    ["typographySystem"] = new JsonObject
                    {
                        ["headlineRole"] = "display",
                        ["bodyRole"] = "text",
                        ["ctaRole"] = "label"
                    }
                })
        };
    }

    private static JsonObject BuildImageDirectionOutput(string selectedConceptId, int variantCount)
    {
        var prompts = new JsonArray();
        for (var i = 1; i <= variantCount; i++)
        {
            prompts.Add(new JsonObject
            {
                ["variantId"] = $"variant_{i}",
                ["prompt"] = i % 2 == 1
                    ? "Soft natural light, empty ceremony atmosphere, no text in frame."
                    : "Vertical soft gradient wash suggesting dusk reception.",
                ["negativeConstraints"] = new JsonArray("no logos", "no readable signage", "no watermarks")
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ImageDirectionWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1,
            ["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
            ["selectedConceptId"] = selectedConceptId,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.ImagePromptDesigner,
                ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — image prompts per variant.",
                ["imagePrompts"] = prompts
            })
        };
    }

    private static JsonObject BuildCopySystemOutput(
        string selectedConceptId,
        int variantCount,
        IReadOnlyList<(string Statement, List<string> SourceIds)> preservedClaims)
    {
        var headlines = new JsonArray();
        var bodies = new JsonArray();
        var ctas = new JsonArray();
        for (var i = 1; i <= variantCount; i++)
        {
            var variantId = $"variant_{i}";
            headlines.Add(new JsonObject
            {
                ["variantId"] = variantId,
                ["text"] = i == 1 ? "Your day, thoughtfully planned." : "A calm next step."
            });

            var body = new JsonObject
            {
                ["variantId"] = variantId,
                ["text"] = i == 1
                    ? "A calm invitation to explore options together."
                    : "Host with confidence and clarity."
            };
            if (i == 2 && preservedClaims.Count > 0)
            {
                var claim = preservedClaims[0];
                body["factualClaims"] = new JsonArray(new JsonObject
                {
                    ["statement"] = claim.Statement,
                    ["sourceIds"] = new JsonArray(claim.SourceIds.Select(s => (JsonNode)s).ToArray())
                });
            }
            else
            {
                body["factualClaims"] = new JsonArray();
            }

            bodies.Add(body);
            ctas.Add(new JsonObject
            {
                ["variantId"] = variantId,
                ["text"] = i == 1 ? "Start planning" : "Continue"
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CopySystemWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1,
            ["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
            ["selectedConceptId"] = selectedConceptId,
            ["contributions"] = new JsonArray(
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — headlines per variant.",
                    ["headlines"] = headlines
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — body copy per variant.",
                    ["bodies"] = bodies
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.CtaSpecialist,
                    ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — CTAs per variant.",
                    ["ctas"] = ctas
                })
        };
    }

    private static JsonObject BuildVariantProductionOutput(
        string selectedConceptId,
        IReadOnlyList<string> formats,
        int variantCount,
        IReadOnlyList<string> paletteRoles)
    {
        var refs = paletteRoles.Take(3).ToList();
        if (refs.Count == 0)
        {
            refs = ["primary", "background", "accent"];
        }

        var variants = new JsonArray();
        for (var i = 1; i <= variantCount; i++)
        {
            var format = formats[(i - 1) % formats.Count];
            // Ensure each brief format is used at least once by assigning sequentially first.
            if (i <= formats.Count)
            {
                format = formats[i - 1];
            }

            var (width, height) = WeddingPlannerCreativeDepartmentValidation.CanvasForFormat(format);
            var variantId = $"variant_{i}";
            variants.Add(new JsonObject
            {
                ["id"] = variantId,
                ["format"] = format,
                ["canvas"] = new JsonObject { ["width"] = width, ["height"] = height },
                ["refs"] = new JsonObject
                {
                    ["direction"] = "contributions.CREATIVE_DIRECTOR",
                    ["visualSystem"] = "contributions.VISUAL_DESIGNER",
                    ["layout"] = "contributions.LAYOUT_DESIGNER",
                    ["typography"] = "contributions.TYPOGRAPHY_DESIGNER",
                    ["imagePrompt"] = $"imagePrompts.{variantId}",
                    ["headline"] = $"headlines.{variantId}",
                    ["body"] = $"bodies.{variantId}",
                    ["cta"] = $"ctas.{variantId}"
                },
                ["paletteRoleRefs"] = new JsonArray(refs.Select(r => (JsonNode)r).ToArray())
            });
        }

        return new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.VariantProductionWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1,
            ["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
            ["selectedConceptId"] = selectedConceptId,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = WeddingPlannerCreativeDepartmentLogicalRoles.VariantProducer,
                ["summary"] = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE — production specs for requested variants.",
                ["variants"] = variants
            })
        };
    }

    private static bool IsWorkshopStage(WeddingPlannerAiCompletionRequest request)
    {
        if (!string.Equals(request.ResponseFormat, WeddingPlannerResponseFormats.Json, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.ConceptStrategy, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.ConceptStrategyV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.ConceptCreative, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.ConceptCreativeV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1, StringComparison.Ordinal))
            || (string.Equals(request.LogicalRole, WeddingPlannerAgentRoles.PrototypeProduction, StringComparison.Ordinal)
                && string.Equals(request.PromptPackVersion, WeddingPlannerPromptPacks.PrototypeProductionV1, StringComparison.Ordinal)
                && string.Equals(request.WorkerProfileVersion, WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1, StringComparison.Ordinal));
    }

    private static WeddingPlannerAiCompletionResult CompleteWorkshopStage(WeddingPlannerAiCompletionRequest request)
    {
        var profile = request.WorkerProfileVersion
            ?? throw new InvalidOperationException("Workshop stage requires WorkerProfileVersion.");
        var assigned = request.AssignedRoles?.ToList()
            ?? WeddingPlannerConceptWorkshopWorkerProfiles.AssignedRoles(profile).ToList();
        var expected = WeddingPlannerConceptWorkshopWorkerProfiles.AssignedRoles(profile);
        if (assigned.Count != expected.Count || !expected.All(assigned.Contains))
        {
            throw new InvalidOperationException("Workshop AssignedRoles must match the worker profile mapping.");
        }

        var context = string.Join("\n", request.Messages.Select(x => x.Body));
        var channelFormat = ExtractChannelFormat(context);
        var (width, height) = WeddingPlannerConceptWorkshopValidation.CanvasForChannelFormat(channelFormat);
        var paletteRoles = ExtractPaletteRoles(context);
        if (paletteRoles.Count == 0)
        {
            paletteRoles = ["primary", "secondary", "accent", "background", "neutral800"];
        }

        var sourceIds = ExtractSourceIds(context);
        if (sourceIds.Count == 0)
        {
            sourceIds = ["src_1"];
        }

        var document = profile switch
        {
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1 => BuildStrategyOutput(),
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1 => BuildCreativeOutput(paletteRoles, sourceIds),
            WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1 => BuildProductionOutput(channelFormat, width, height, paletteRoles),
            _ => throw new InvalidOperationException($"Unsupported workshop profile '{profile}'.")
        };

        return BuildResult(document.ToJsonString(NodeWriteOptions), request);
    }

    private static JsonObject BuildStrategyOutput() =>
        new()
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ConceptStrategyWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1,
            ["marker"] = WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = WeddingPlannerConceptWorkshopLogicalRoles.BrandStrategist,
                ["summary"] = "SYNTHETIC DEVELOPMENT PROTOTYPE — three concept directions aligned to the brief.",
                ["concepts"] = new JsonArray(
                    StrategyConcept(WeddingPlannerConceptIds.Concept1, "Quiet Confidence", "Leads with understated brand voice for the stated audience."),
                    StrategyConcept(WeddingPlannerConceptIds.Concept2, "Warm Invitation", "Centers hospitality cues without inventing product facts."),
                    StrategyConcept(WeddingPlannerConceptIds.Concept3, "Clear Next Step", "Privileges CTA clarity for the selected channel format."))
            })
        };

    private static JsonObject StrategyConcept(string id, string name, string rationale) =>
        new()
        {
            ["id"] = id,
            ["name"] = name,
            ["rationale"] = rationale
        };

    private static JsonObject BuildCreativeOutput(IReadOnlyList<string> paletteRoles, IReadOnlyList<string> sourceIds)
    {
        string Role(int index) => paletteRoles[Math.Min(index, paletteRoles.Count - 1)];
        var primarySource = sourceIds[0];

        return new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ConceptCreativeWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1,
            ["marker"] = WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype,
            ["contributions"] = new JsonArray(
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerConceptWorkshopLogicalRoles.ArtDirector,
                    ["summary"] = "SYNTHETIC DEVELOPMENT PROTOTYPE — visual direction per concept.",
                    ["concepts"] = new JsonArray(
                        ArtConcept(WeddingPlannerConceptIds.Concept1, "Centered quiet hero with generous negative space.", Role(0), Role(3), Role(2)),
                        ArtConcept(WeddingPlannerConceptIds.Concept2, "Split frame with soft secondary wash.", Role(0), Role(1), Role(3)),
                        ArtConcept(WeddingPlannerConceptIds.Concept3, "Banner-forward CTA block with logo slot.", Role(0), Role(2), Role(3)))
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerConceptWorkshopLogicalRoles.Copywriter,
                    ["summary"] = "SYNTHETIC DEVELOPMENT PROTOTYPE — creative non-factual copy per concept.",
                    ["concepts"] = new JsonArray(
                        CopyConcept(WeddingPlannerConceptIds.Concept1, "Your day, thoughtfully planned.", "A calm invitation to explore options together.", "Start planning", null),
                        CopyConcept(
                            WeddingPlannerConceptIds.Concept2,
                            "Warm welcomes begin here.",
                            "Host with confidence and clarity.",
                            "See the experience",
                            new JsonObject
                            {
                                ["statement"] = "SYNTHETIC DEVELOPMENT PROTOTYPE: couples often research venues months ahead.",
                                ["sourceIds"] = new JsonArray(primarySource)
                            }),
                        CopyConcept(WeddingPlannerConceptIds.Concept3, "One clear next step.", "Keep the path simple from glance to action.", "Continue", null))
                })
        };
    }

    private static JsonObject ArtConcept(string id, string visual, string a, string b, string c) =>
        new()
        {
            ["id"] = id,
            ["visualDirection"] = visual,
            ["paletteRoleRefs"] = new JsonArray(a, b, c)
        };

    private static JsonObject CopyConcept(string id, string headline, string body, string cta, JsonObject? claim)
    {
        var claims = new JsonArray();
        if (claim is not null)
        {
            claims.Add(claim);
        }

        return new JsonObject
        {
            ["id"] = id,
            ["copy"] = new JsonObject
            {
                ["kind"] = WeddingPlannerCopyKinds.CreativeNonFactual,
                ["headline"] = headline,
                ["body"] = body,
                ["cta"] = cta
            },
            ["factualClaims"] = claims
        };
    }

    private static JsonObject BuildProductionOutput(string channelFormat, int width, int height, IReadOnlyList<string> paletteRoles)
    {
        string Role(int index) => paletteRoles[Math.Min(index, paletteRoles.Count - 1)];
        return new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.PrototypeProductionWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1,
            ["marker"] = WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = WeddingPlannerConceptWorkshopLogicalRoles.ProductionArtist,
                ["summary"] = "SYNTHETIC DEVELOPMENT PROTOTYPE — low-fi prototype specs for three concepts.",
                ["prototypes"] = new JsonArray(
                    Prototype(WeddingPlannerConceptIds.Concept1, channelFormat, width, height, WeddingPlannerPrototypeTemplates.LofiStackV1, Role),
                    Prototype(WeddingPlannerConceptIds.Concept2, channelFormat, width, height, WeddingPlannerPrototypeTemplates.LofiSplitV1, Role),
                    Prototype(WeddingPlannerConceptIds.Concept3, channelFormat, width, height, WeddingPlannerPrototypeTemplates.LofiBannerV1, Role))
            })
        };
    }

    private static JsonObject Prototype(
        string conceptId,
        string channelFormat,
        int width,
        int height,
        string template,
        Func<int, string> role)
    {
        var heroH = Math.Max(120, (int)(height * 0.55));
        var headlineY = heroH + 40;
        var bodyY = headlineY + 100;
        var ctaY = Math.Min(height - 90, bodyY + 130);
        var logoY = height - 60;
        var textW = Math.Max(100, width - 128);

        return new JsonObject
        {
            ["conceptId"] = conceptId,
            ["spec"] = new JsonObject
            {
                ["schemaVersion"] = WeddingPlannerSchemaVersions.PrototypeSpecV1,
                ["format"] = channelFormat,
                ["canvas"] = new JsonObject { ["width"] = width, ["height"] = height },
                ["template"] = template,
                ["regions"] = new JsonArray(
                    Region("r1", WeddingPlannerPrototypeRegionTypes.Hero, 0, 0, width, heroH, null, null,
                        new JsonObject
                        {
                            ["kind"] = WeddingPlannerAssetPlaceholderKinds.HeroImage,
                            ["label"] = "Hero atmosphere placeholder"
                        }),
                    Region("r2", WeddingPlannerPrototypeRegionTypes.Headline, 64, headlineY, textW, 96,
                        WeddingPlannerPrototypeTextRefs.CopyHeadline, role(0), null),
                    Region("r3", WeddingPlannerPrototypeRegionTypes.Body, 64, bodyY, textW, 120,
                        WeddingPlannerPrototypeTextRefs.CopyBody, role(Math.Min(4, 4)), null),
                    Region("r4", WeddingPlannerPrototypeRegionTypes.Cta, 64, ctaY, Math.Min(360, textW), 72,
                        WeddingPlannerPrototypeTextRefs.CopyCta, role(2), null),
                    Region("r5", WeddingPlannerPrototypeRegionTypes.LogoSlot, Math.Max(0, width - 180), logoY, 116, 48,
                        null, null,
                        new JsonObject
                        {
                            ["kind"] = WeddingPlannerAssetPlaceholderKinds.Logo,
                            ["label"] = "Logo placeholder"
                        }))
            }
        };
    }

    private static JsonObject Region(
        string id,
        string type,
        int x,
        int y,
        int w,
        int h,
        string? textRef,
        string? paletteRoleRef,
        JsonObject? assetPlaceholder)
    {
        var obj = new JsonObject
        {
            ["id"] = id,
            ["type"] = type,
            ["bounds"] = new JsonObject { ["x"] = x, ["y"] = y, ["w"] = w, ["h"] = h }
        };
        if (textRef is not null)
        {
            obj["textRef"] = textRef;
        }

        if (paletteRoleRef is not null)
        {
            obj["paletteRoleRef"] = paletteRoleRef;
        }

        if (assetPlaceholder is not null)
        {
            obj["assetPlaceholder"] = assetPlaceholder;
        }

        return obj;
    }

    private static string ExtractChannelFormat(string context)
    {
        foreach (var format in WeddingPlannerChannelFormats.All)
        {
            if (context.Contains(format, StringComparison.Ordinal))
            {
                return format;
            }
        }

        return WeddingPlannerChannelFormats.StaticSocialSquare;
    }

    private static List<string> ExtractPaletteRoles(string context)
    {
        var roles = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        const string marker = "\"paletteRoles\":";
        var idx = context.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return roles;
        }

        var start = context.IndexOf('[', idx);
        var end = context.IndexOf(']', start + 1);
        if (start < 0 || end < 0)
        {
            return roles;
        }

        var slice = context[(start + 1)..end];
        foreach (var part in slice.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var role = part.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(role) && seen.Add(role))
            {
                roles.Add(role);
            }
        }

        return roles;
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
