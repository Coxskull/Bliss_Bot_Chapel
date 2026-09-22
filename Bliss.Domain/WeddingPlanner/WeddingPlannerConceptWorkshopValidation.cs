using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Strict validators/canonicalizers for Concept Workshop briefs, stage outputs,
/// prototype-spec.v1, and merged concept-package.v1 documents.
/// Never generates images, fetches URLs, or executes markup.
/// </summary>
public static class WeddingPlannerConceptWorkshopValidation
{
    public const int MaxObjectiveLength = 4000;
    public const int MaxCampaignGoalLength = 500;
    public const int MaxAudienceFocusLength = 1000;
    public const int MinDeliverables = 1;
    public const int MaxDeliverables = 6;
    public const int MaxDeliverableLength = 200;
    public const int MaxCtaLength = 200;
    public const int MaxConstraints = 12;
    public const int MaxConstraintLength = 500;
    public const int MaxContributionSummaryLength = 2000;
    public const int MaxConceptNameLength = 200;
    public const int MaxConceptRationaleLength = 2000;
    public const int MaxVisualDirectionLength = 2000;
    public const int MaxCopyFieldLength = 1000;
    public const int MaxFactualClaimStatementLength = 2000;
    public const int MaxFactualClaimsPerConcept = 8;
    public const int MaxSourceIdsPerClaim = 10;
    public const int MaxSourceIdLength = 64;
    public const int MaxPaletteRoleRefs = 8;
    public const int MaxRegionIdLength = 64;
    public const int MinRegions = 2;
    public const int MaxRegions = 12;
    public const int MaxAssetPlaceholderLabelLength = 200;
    public const int MaxMarkerLength = 128;

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new()
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

    private static readonly HashSet<string> ChannelFormatSet = new(WeddingPlannerChannelFormats.All, StringComparer.Ordinal);
    private static readonly HashSet<string> ConceptIdSet = new(WeddingPlannerConceptIds.All, StringComparer.Ordinal);
    private static readonly HashSet<string> TemplateSet = new(WeddingPlannerPrototypeTemplates.All, StringComparer.Ordinal);
    private static readonly HashSet<string> RegionTypeSet = new(WeddingPlannerPrototypeRegionTypes.All, StringComparer.Ordinal);
    private static readonly HashSet<string> TextRefSet = new(WeddingPlannerPrototypeTextRefs.All, StringComparer.Ordinal);
    private static readonly HashSet<string> AssetKindSet = new(WeddingPlannerAssetPlaceholderKinds.All, StringComparer.Ordinal);

    private static readonly HashSet<string> ForbiddenKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "css", "svg", "script", "src", "url", "href", "base64",
        "imageBytes", "image_bytes", "imageData", "image_data",
        "matchId", "matchingId", "blissMatchId", "matchIds", "matchingIds",
        "campaignId", "campaignIds",
        "placementId", "placementIds",
        "inventoryId", "inventoryIds", "adInventorySlotId",
        "campaignReady", "qaApproved", "blissReady", "readiness",
        "imageProvider", "assetProvider", "imageTool", "assetUpload",
        "providerToolConfiguration", "assetBinary"
    };

    private static readonly HashSet<string> ForbiddenProvenanceSourceIdKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "approvedBrandDnaVersionId",
        "approvedColorProfileVersionId",
        "approvedResearchReportVersionId",
        "workshopJobId",
        "conceptPackageVersionId",
        "brandDnaVersionId",
        "colorProfileVersionId",
        "researchReportVersionId",
        "jobId",
        "packageId"
    };

    public static (int Width, int Height) CanvasForChannelFormat(string channelFormat) =>
        channelFormat switch
        {
            WeddingPlannerChannelFormats.StaticSocialSquare => (1080, 1080),
            WeddingPlannerChannelFormats.StaticSocialStory => (1080, 1920),
            WeddingPlannerChannelFormats.StaticDisplayBanner => (1200, 628),
            WeddingPlannerChannelFormats.EmailHero => (1200, 600),
            _ => throw new InvalidOperationException($"Unknown ChannelFormat '{channelFormat}'.")
        };

    public static void RejectForbiddenBriefFields(JsonNode? node, string path = "$")
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (ForbiddenKeySet.Contains(property.Key)
                    || LooksLikeForbiddenAlias(property.Key))
                {
                    throw new InvalidOperationException(
                        $"Forbidden brief field '{property.Key}' is not allowed.");
                }

                RejectForbiddenBriefFields(property.Value, path + "." + property.Key);
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                RejectForbiddenBriefFields(arr[i], path + $"[{i}]");
            }
        }
    }

    public static CanonicalWorkshopBrief CanonicalizeBrief(
        string objective,
        string campaignGoal,
        string audienceFocus,
        string channelFormat,
        IReadOnlyList<string> deliverables,
        string cta,
        IReadOnlyList<string>? constraints,
        Guid approvedBrandDnaVersionId,
        int approvedBrandDnaVersionNumber,
        Guid approvedColorProfileVersionId,
        int approvedColorProfileVersionNumber,
        Guid approvedResearchReportVersionId,
        int approvedResearchReportVersionNumber,
        int? clientCanvasWidth,
        int? clientCanvasHeight,
        string aiProviderKind,
        string aiWorkerKey)
    {
        var canonicalObjective = Required(objective, "Objective", MaxObjectiveLength);
        var canonicalCampaignGoal = Required(campaignGoal, "CampaignGoal", MaxCampaignGoalLength);
        var canonicalAudienceFocus = Required(audienceFocus, "AudienceFocus", MaxAudienceFocusLength);
        var canonicalCta = Required(cta, "Cta", MaxCtaLength);
        var canonicalFormat = Required(channelFormat, "ChannelFormat", 64);
        if (!ChannelFormatSet.Contains(canonicalFormat))
        {
            throw new InvalidOperationException(
                $"ChannelFormat must be one of: {string.Join(", ", WeddingPlannerChannelFormats.All)}.");
        }

        if (deliverables is null || deliverables.Count < MinDeliverables || deliverables.Count > MaxDeliverables)
        {
            throw new InvalidOperationException(
                $"Deliverables must contain between {MinDeliverables} and {MaxDeliverables} non-empty items.");
        }

        var canonicalDeliverables = deliverables
            .Select(d => Required(d, "Deliverables item", MaxDeliverableLength))
            .ToList();

        var canonicalConstraints = NormalizeStringList(
            constraints, "Constraints", MaxConstraints, MaxConstraintLength);

        var (width, height) = CanvasForChannelFormat(canonicalFormat);
        if (clientCanvasWidth is int cw || clientCanvasHeight is int ch)
        {
            var suppliedW = clientCanvasWidth ?? -1;
            var suppliedH = clientCanvasHeight ?? -1;
            if (suppliedW != width || suppliedH != height)
            {
                throw new InvalidOperationException(
                    $"Canvas must be exactly {width}x{height} for ChannelFormat {canonicalFormat}; client overrides are not allowed.");
            }
        }

        var payload = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.WorkshopBriefV1,
            ["objective"] = canonicalObjective,
            ["campaignGoal"] = canonicalCampaignGoal,
            ["audienceFocus"] = canonicalAudienceFocus,
            ["channelFormat"] = canonicalFormat,
            ["deliverables"] = ToJsonArray(canonicalDeliverables),
            ["cta"] = canonicalCta,
            ["constraints"] = ToJsonArray(canonicalConstraints),
            ["approvedBrandDnaVersionId"] = approvedBrandDnaVersionId.ToString("D"),
            ["approvedColorProfileVersionId"] = approvedColorProfileVersionId.ToString("D"),
            ["approvedResearchReportVersionId"] = approvedResearchReportVersionId.ToString("D"),
            ["aiProviderKind"] = Required(aiProviderKind, "aiProviderKind", 64),
            ["aiWorkerKey"] = Required(aiWorkerKey, "aiWorkerKey", 64),
            ["conceptWorkshopOrchestrationVersion"] =
                WeddingPlannerConceptWorkshopContractVersions.ConceptWorkshopOrchestrationV1
        };

        RejectForbiddenBriefFields(payload);

        var ordered = OrderObjectKeys(payload);
        var json = ordered.ToJsonString(NodeWriteOptions);
        var sha = Sha256Hex(json);

        return new CanonicalWorkshopBrief(
            Objective: canonicalObjective,
            CampaignGoal: canonicalCampaignGoal,
            AudienceFocus: canonicalAudienceFocus,
            ChannelFormat: canonicalFormat,
            CanvasWidth: width,
            CanvasHeight: height,
            Deliverables: canonicalDeliverables,
            Cta: canonicalCta,
            Constraints: canonicalConstraints,
            ApprovedBrandDnaVersionId: approvedBrandDnaVersionId,
            ApprovedBrandDnaVersionNumber: approvedBrandDnaVersionNumber,
            ApprovedColorProfileVersionId: approvedColorProfileVersionId,
            ApprovedColorProfileVersionNumber: approvedColorProfileVersionNumber,
            ApprovedResearchReportVersionId: approvedResearchReportVersionId,
            ApprovedResearchReportVersionNumber: approvedResearchReportVersionNumber,
            InputJson: json,
            InputSha256: sha);
    }

    public static IReadOnlySet<string> ExtractPaletteRoleNames(string colorProfileDocumentJson)
    {
        if (string.IsNullOrWhiteSpace(colorProfileDocumentJson))
        {
            throw new InvalidOperationException("Pinned color profile DocumentJson is required.");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(colorProfileDocumentJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Pinned color profile DocumentJson is invalid.", ex);
        }

        if (root is not JsonObject obj || obj["palette"] is not JsonObject palette)
        {
            throw new InvalidOperationException("Pinned color profile must contain a palette object.");
        }

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in palette)
        {
            if (!string.IsNullOrWhiteSpace(property.Key))
            {
                roles.Add(property.Key);
            }
        }

        if (roles.Count == 0)
        {
            throw new InvalidOperationException("Pinned color profile palette has no roles.");
        }

        return roles;
    }

    public static IReadOnlySet<string> ExtractResearchSourceIds(string researchReportDocumentJson)
    {
        if (string.IsNullOrWhiteSpace(researchReportDocumentJson))
        {
            throw new InvalidOperationException("Pinned research report DocumentJson is required.");
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(researchReportDocumentJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Pinned research report DocumentJson is invalid.", ex);
        }

        if (root is not JsonObject obj || obj["sources"] is not JsonArray sources)
        {
            throw new InvalidOperationException("Pinned research report must contain a sources array.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in sources)
        {
            if (item is not JsonObject source)
            {
                throw new InvalidOperationException("Each research source must be an object.");
            }

            var id = Required(source["id"]?.GetValue<string>(), "sources[].id", MaxSourceIdLength);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"Pinned research report has duplicate source id '{id}'.");
            }
        }

        if (ids.Count == 0)
        {
            throw new InvalidOperationException("Pinned research report must contain at least one source id.");
        }

        return ids;
    }

    public static CanonicalWorkshopStageOutput CanonicalizeStrategyOutput(
        string? stageJson,
        bool requireSyntheticMarker)
    {
        var root = ParseObject(stageJson, "Strategy stage output");
        RejectForbiddenKeysEverywhere(root);
        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "marker", "contributions");
        AssertSchema(root, WeddingPlannerSchemaVersions.ConceptStrategyWorkerOutputV1);
        AssertWorkerProfile(root, WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1);
        AssertMarker(root, requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 1)
        {
            throw new InvalidOperationException("Strategy stage must contain exactly one contribution.");
        }

        var contribution = contributions[0];
        AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "concepts");
        var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!string.Equals(role, WeddingPlannerConceptWorkshopLogicalRoles.BrandStrategist, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Strategy stage contribution must be BRAND_STRATEGIST.");
        }

        var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
        var concepts = ParseStrategyConcepts(contribution["concepts"]);

        var ordered = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ConceptStrategyWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = role,
                ["summary"] = summary,
                ["concepts"] = new JsonArray(concepts.Select(c => (JsonNode)new JsonObject
                {
                    ["id"] = c.Id,
                    ["name"] = c.Name,
                    ["rationale"] = c.Rationale
                }).ToArray())
            })
        };
        if (requireSyntheticMarker || root["marker"] is not null)
        {
            ordered["marker"] = WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype;
        }

        var json = ordered.ToJsonString(NodeWriteOptions);
        return new CanonicalWorkshopStageOutput(
            json,
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1,
            [
                new CanonicalWorkshopContribution(
                    role,
                    summary,
                    concepts,
                    Array.Empty<CanonicalWorkshopArtDirection>(),
                    Array.Empty<CanonicalWorkshopCopyConcept>(),
                    Array.Empty<CanonicalWorkshopPrototype>())
            ],
            concepts.Select(c => c.Id).ToList());
    }

    public static CanonicalWorkshopStageOutput CanonicalizeCreativeOutput(
        string? stageJson,
        IReadOnlyList<string> expectedConceptIds,
        IReadOnlySet<string> knownSourceIds,
        IReadOnlySet<string> knownPaletteRoles,
        IReadOnlySet<string> forbiddenSourceIds,
        bool requireSyntheticMarker)
    {
        var root = ParseObject(stageJson, "Creative stage output");
        RejectForbiddenKeysEverywhere(root);
        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "marker", "contributions");
        AssertSchema(root, WeddingPlannerSchemaVersions.ConceptCreativeWorkerOutputV1);
        AssertWorkerProfile(root, WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1);
        AssertMarker(root, requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 2)
        {
            throw new InvalidOperationException("Creative stage must contain exactly two contributions.");
        }

        CanonicalWorkshopContribution? art = null;
        CanonicalWorkshopContribution? copy = null;
        foreach (var contribution in contributions)
        {
            var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
            var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
            if (string.Equals(role, WeddingPlannerConceptWorkshopLogicalRoles.ArtDirector, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "concepts");
                var directions = ParseArtDirectorConcepts(contribution["concepts"], expectedConceptIds, knownPaletteRoles);
                art = new CanonicalWorkshopContribution(
                    role, summary, Array.Empty<CanonicalWorkshopStrategyConcept>(), directions,
                    Array.Empty<CanonicalWorkshopCopyConcept>(), Array.Empty<CanonicalWorkshopPrototype>());
            }
            else if (string.Equals(role, WeddingPlannerConceptWorkshopLogicalRoles.Copywriter, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "concepts");
                var copies = ParseCopywriterConcepts(
                    contribution["concepts"], expectedConceptIds, knownSourceIds, forbiddenSourceIds);
                copy = new CanonicalWorkshopContribution(
                    role, summary, Array.Empty<CanonicalWorkshopStrategyConcept>(),
                    Array.Empty<CanonicalWorkshopArtDirection>(), copies, Array.Empty<CanonicalWorkshopPrototype>());
            }
            else
            {
                throw new InvalidOperationException(
                    $"Creative stage contains unexpected logicalRole '{role}'.");
            }
        }

        if (art is null || copy is null)
        {
            throw new InvalidOperationException(
                "Creative stage must include exactly ART_DIRECTOR and COPYWRITER contributions.");
        }

        var orderedContributions = new[] { art, copy };
        var ordered = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ConceptCreativeWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1,
            ["contributions"] = new JsonArray(
                ToArtContributionNode(art),
                ToCopyContributionNode(copy))
        };
        if (requireSyntheticMarker || root["marker"] is not null)
        {
            ordered["marker"] = WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype;
        }

        return new CanonicalWorkshopStageOutput(
            ordered.ToJsonString(NodeWriteOptions),
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1,
            orderedContributions,
            expectedConceptIds.ToList());
    }

    public static CanonicalWorkshopStageOutput CanonicalizeProductionOutput(
        string? stageJson,
        IReadOnlyList<string> expectedConceptIds,
        string channelFormat,
        int canvasWidth,
        int canvasHeight,
        IReadOnlySet<string> knownPaletteRoles,
        bool requireSyntheticMarker)
    {
        var root = ParseObject(stageJson, "Production stage output");
        RejectForbiddenKeysEverywhere(root);
        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "marker", "contributions");
        AssertSchema(root, WeddingPlannerSchemaVersions.PrototypeProductionWorkerOutputV1);
        AssertWorkerProfile(root, WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1);
        AssertMarker(root, requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 1)
        {
            throw new InvalidOperationException("Production stage must contain exactly one contribution.");
        }

        var contribution = contributions[0];
        AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "prototypes");
        var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!string.Equals(role, WeddingPlannerConceptWorkshopLogicalRoles.ProductionArtist, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Production stage contribution must be PRODUCTION_ARTIST.");
        }

        var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
        var prototypes = ParsePrototypes(
            contribution["prototypes"],
            expectedConceptIds,
            channelFormat,
            canvasWidth,
            canvasHeight,
            knownPaletteRoles);

        var ordered = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.PrototypeProductionWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = role,
                ["summary"] = summary,
                ["prototypes"] = new JsonArray(prototypes.Select(p => (JsonNode)new JsonObject
                {
                    ["conceptId"] = p.ConceptId,
                    ["spec"] = p.SpecNode.DeepClone()
                }).ToArray())
            })
        };
        if (requireSyntheticMarker || root["marker"] is not null)
        {
            ordered["marker"] = WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype;
        }

        return new CanonicalWorkshopStageOutput(
            ordered.ToJsonString(NodeWriteOptions),
            WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1,
            [
                new CanonicalWorkshopContribution(
                    role,
                    summary,
                    Array.Empty<CanonicalWorkshopStrategyConcept>(),
                    Array.Empty<CanonicalWorkshopArtDirection>(),
                    Array.Empty<CanonicalWorkshopCopyConcept>(),
                    prototypes)
            ],
            expectedConceptIds.ToList());
    }

    public static CanonicalConceptPackage MergeAndCanonicalizePackage(
        CanonicalWorkshopBrief brief,
        CanonicalWorkshopStageOutput strategy,
        CanonicalWorkshopStageOutput creative,
        CanonicalWorkshopStageOutput production,
        Guid workshopJobId,
        bool requireSyntheticMarker)
    {
        var strategist = strategy.Contributions.Single(c =>
            c.LogicalRole == WeddingPlannerConceptWorkshopLogicalRoles.BrandStrategist);
        var art = creative.Contributions.Single(c =>
            c.LogicalRole == WeddingPlannerConceptWorkshopLogicalRoles.ArtDirector);
        var copy = creative.Contributions.Single(c =>
            c.LogicalRole == WeddingPlannerConceptWorkshopLogicalRoles.Copywriter);
        var productionArtist = production.Contributions.Single(c =>
            c.LogicalRole == WeddingPlannerConceptWorkshopLogicalRoles.ProductionArtist);

        if (strategist.StrategyConcepts.Count != 3
            || art.ArtDirections.Count != 3
            || copy.CopyConcepts.Count != 3
            || productionArtist.Prototypes.Count != 3)
        {
            throw new InvalidOperationException("Merged package requires exactly three concepts across all stages.");
        }

        var concepts = new List<CanonicalMergedConcept>(3);
        foreach (var conceptId in WeddingPlannerConceptIds.All)
        {
            var strategyConcept = strategist.StrategyConcepts.Single(c => c.Id == conceptId);
            var artConcept = art.ArtDirections.Single(c => c.Id == conceptId);
            var copyConcept = copy.CopyConcepts.Single(c => c.Id == conceptId);
            var prototype = productionArtist.Prototypes.Single(c => c.ConceptId == conceptId);
            concepts.Add(new CanonicalMergedConcept(
                strategyConcept.Id,
                strategyConcept.Name,
                strategyConcept.Rationale,
                artConcept.VisualDirection,
                artConcept.PaletteRoleRefs,
                copyConcept.Copy,
                copyConcept.FactualClaims,
                prototype.SpecNode));
        }

        var contributions = new List<CanonicalWorkshopContributionSummary>
        {
            new(strategist.LogicalRole, strategist.Summary),
            new(art.LogicalRole, art.Summary),
            new(copy.LogicalRole, copy.Summary),
            new(productionArtist.LogicalRole, productionArtist.Summary)
        };

        if (contributions.Count != 4)
        {
            throw new InvalidOperationException("Merged package requires exactly four contributions.");
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ConceptPackageV1,
            ["disclaimer"] = WeddingPlannerConceptPackageDisclaimer.Text,
            ["provenance"] = new JsonObject
            {
                ["approvedBrandDnaVersionId"] = brief.ApprovedBrandDnaVersionId.ToString("D"),
                ["approvedBrandDnaVersionNumber"] = brief.ApprovedBrandDnaVersionNumber,
                ["approvedColorProfileVersionId"] = brief.ApprovedColorProfileVersionId.ToString("D"),
                ["approvedColorProfileVersionNumber"] = brief.ApprovedColorProfileVersionNumber,
                ["approvedResearchReportVersionId"] = brief.ApprovedResearchReportVersionId.ToString("D"),
                ["approvedResearchReportVersionNumber"] = brief.ApprovedResearchReportVersionNumber,
                ["workshopJobId"] = workshopJobId.ToString("D")
            },
            ["brief"] = new JsonObject
            {
                ["objective"] = brief.Objective,
                ["campaignGoal"] = brief.CampaignGoal,
                ["audienceFocus"] = brief.AudienceFocus,
                ["channelFormat"] = brief.ChannelFormat,
                ["canvas"] = new JsonObject
                {
                    ["width"] = brief.CanvasWidth,
                    ["height"] = brief.CanvasHeight
                },
                ["deliverables"] = ToJsonArray(brief.Deliverables),
                ["cta"] = brief.Cta,
                ["constraints"] = ToJsonArray(brief.Constraints)
            },
            ["concepts"] = new JsonArray(concepts.Select(ToMergedConceptNode).ToArray()),
            ["contributions"] = new JsonArray(contributions.Select(c => (JsonNode)new JsonObject
            {
                ["logicalRole"] = c.LogicalRole,
                ["summary"] = c.Summary
            }).ToArray())
        };

        if (requireSyntheticMarker)
        {
            document["marker"] = WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype;
        }

        RejectForbiddenKeysEverywhere(document);

        var json = document.ToJsonString(NodeWriteOptions);
        if (!json.Contains(WeddingPlannerConceptPackageDisclaimer.Text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Merged concept package must include the exact concept-direction disclaimer.");
        }

        if (requireSyntheticMarker
            && !json.Contains(WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Local Concept Workshop packages must include the SYNTHETIC DEVELOPMENT PROTOTYPE marker.");
        }

        var summary = Truncate(
            $"{WeddingPlannerSchemaVersions.ConceptPackageV1}: {brief.ChannelFormat} — 3 concepts, 4 contributions.",
            2000);

        return new CanonicalConceptPackage(json, summary, concepts, contributions);
    }

    public static string SerializeAssignedRolesJson(IReadOnlyList<string> roles) =>
        JsonSerializer.Serialize(roles, CanonicalJsonOptions);

    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static IReadOnlySet<string> ExtractConceptIdsFromPackage(string documentJson)
    {
        var root = ParseObject(documentJson, "Concept package");
        if (root["concepts"] is not JsonArray concepts)
        {
            throw new InvalidOperationException("Concept package concepts must be an array.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in concepts)
        {
            if (item is not JsonObject obj)
            {
                continue;
            }

            var id = obj["id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static List<CanonicalWorkshopStrategyConcept> ParseStrategyConcepts(JsonNode? node)
    {
        if (node is not JsonArray arr || arr.Count != 3)
        {
            throw new InvalidOperationException("Strategy concepts must contain exactly three items.");
        }

        var concepts = new List<CanonicalWorkshopStrategyConcept>(3);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException("Each strategy concept must be an object.");
            }

            AssertOnlyKnownProperties(obj, "id", "name", "rationale");
            var id = Required(obj["id"]?.GetValue<string>(), "concept.id", 32);
            if (!ConceptIdSet.Contains(id))
            {
                throw new InvalidOperationException($"Unknown concept id '{id}'.");
            }

            if (!seen.Add(id))
            {
                throw new InvalidOperationException($"Duplicate concept id '{id}'.");
            }

            concepts.Add(new CanonicalWorkshopStrategyConcept(
                id,
                Required(obj["name"]?.GetValue<string>(), "concept.name", MaxConceptNameLength),
                Required(obj["rationale"]?.GetValue<string>(), "concept.rationale", MaxConceptRationaleLength)));
        }

        AssertExactConceptIds(seen);
        return WeddingPlannerConceptIds.All
            .Select(id => concepts.Single(c => c.Id == id))
            .ToList();
    }

    private static List<CanonicalWorkshopArtDirection> ParseArtDirectorConcepts(
        JsonNode? node,
        IReadOnlyList<string> expectedConceptIds,
        IReadOnlySet<string> knownPaletteRoles)
    {
        if (node is not JsonArray arr || arr.Count != 3)
        {
            throw new InvalidOperationException("Art Director concepts must contain exactly three items.");
        }

        var concepts = new List<CanonicalWorkshopArtDirection>(3);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException("Each Art Director concept must be an object.");
            }

            AssertOnlyKnownProperties(obj, "id", "visualDirection", "paletteRoleRefs");
            var id = Required(obj["id"]?.GetValue<string>(), "concept.id", 32);
            if (!ConceptIdSet.Contains(id) || !seen.Add(id))
            {
                throw new InvalidOperationException($"Invalid or duplicate concept id '{id}'.");
            }

            var visual = Required(obj["visualDirection"]?.GetValue<string>(), "visualDirection", MaxVisualDirectionLength);
            if (obj["paletteRoleRefs"] is not JsonArray rolesArr || rolesArr.Count == 0 || rolesArr.Count > MaxPaletteRoleRefs)
            {
                throw new InvalidOperationException("paletteRoleRefs must contain 1–8 known palette roles.");
            }

            var roles = new List<string>(rolesArr.Count);
            var roleSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var roleNode in rolesArr)
            {
                var role = Required(roleNode?.GetValue<string>(), "paletteRoleRef", 64);
                if (!knownPaletteRoles.Contains(role))
                {
                    throw new InvalidOperationException($"Unknown paletteRoleRef '{role}'.");
                }

                if (!roleSeen.Add(role))
                {
                    throw new InvalidOperationException($"Duplicate paletteRoleRef '{role}'.");
                }

                roles.Add(role);
            }

            concepts.Add(new CanonicalWorkshopArtDirection(id, visual, roles));
        }

        AssertExactConceptIds(seen);
        AssertConceptIdsMatch(expectedConceptIds, seen);
        return WeddingPlannerConceptIds.All
            .Select(id => concepts.Single(c => c.Id == id))
            .ToList();
    }

    private static List<CanonicalWorkshopCopyConcept> ParseCopywriterConcepts(
        JsonNode? node,
        IReadOnlyList<string> expectedConceptIds,
        IReadOnlySet<string> knownSourceIds,
        IReadOnlySet<string> forbiddenSourceIds)
    {
        if (node is not JsonArray arr || arr.Count != 3)
        {
            throw new InvalidOperationException("Copywriter concepts must contain exactly three items.");
        }

        var concepts = new List<CanonicalWorkshopCopyConcept>(3);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException("Each Copywriter concept must be an object.");
            }

            AssertOnlyKnownProperties(obj, "id", "copy", "factualClaims");
            var id = Required(obj["id"]?.GetValue<string>(), "concept.id", 32);
            if (!ConceptIdSet.Contains(id) || !seen.Add(id))
            {
                throw new InvalidOperationException($"Invalid or duplicate concept id '{id}'.");
            }

            if (obj["copy"] is not JsonObject copyObj)
            {
                throw new InvalidOperationException("copy must be an object.");
            }

            AssertOnlyKnownProperties(copyObj, "kind", "headline", "body", "cta");
            var kind = Required(copyObj["kind"]?.GetValue<string>(), "copy.kind", 64);
            if (!string.Equals(kind, WeddingPlannerCopyKinds.CreativeNonFactual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"copy.kind must be {WeddingPlannerCopyKinds.CreativeNonFactual}.");
            }

            var copy = new CanonicalWorkshopCopy(
                kind,
                Required(copyObj["headline"]?.GetValue<string>(), "copy.headline", MaxCopyFieldLength),
                Required(copyObj["body"]?.GetValue<string>(), "copy.body", MaxCopyFieldLength),
                Required(copyObj["cta"]?.GetValue<string>(), "copy.cta", MaxCopyFieldLength));

            var claims = new List<CanonicalWorkshopFactualClaim>();
            if (obj["factualClaims"] is JsonArray claimsArr)
            {
                if (claimsArr.Count > MaxFactualClaimsPerConcept)
                {
                    throw new InvalidOperationException(
                        $"factualClaims cannot exceed {MaxFactualClaimsPerConcept} items.");
                }

                foreach (var claimNode in claimsArr)
                {
                    claims.Add(ParseFactualClaim(claimNode, knownSourceIds, forbiddenSourceIds));
                }
            }
            else if (obj["factualClaims"] is not null)
            {
                throw new InvalidOperationException("factualClaims must be an array when present.");
            }

            concepts.Add(new CanonicalWorkshopCopyConcept(id, copy, claims));
        }

        AssertExactConceptIds(seen);
        AssertConceptIdsMatch(expectedConceptIds, seen);
        return WeddingPlannerConceptIds.All
            .Select(id => concepts.Single(c => c.Id == id))
            .ToList();
    }

    private static CanonicalWorkshopFactualClaim ParseFactualClaim(
        JsonNode? node,
        IReadOnlySet<string> knownSourceIds,
        IReadOnlySet<string> forbiddenSourceIds)
    {
        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException("Each factualClaim must be an object.");
        }

        AssertOnlyKnownProperties(obj, "statement", "sourceIds");
        var statement = Required(obj["statement"]?.GetValue<string>(), "factualClaim.statement", MaxFactualClaimStatementLength);
        if (obj["sourceIds"] is not JsonArray sourceIds || sourceIds.Count == 0 || sourceIds.Count > MaxSourceIdsPerClaim)
        {
            throw new InvalidOperationException("factualClaim.sourceIds must contain 1–10 known research source ids.");
        }

        var ids = new List<string>(sourceIds.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sourceNode in sourceIds)
        {
            var id = Required(sourceNode?.GetValue<string>(), "sourceId", MaxSourceIdLength);
            if (forbiddenSourceIds.Contains(id)
                || Guid.TryParse(id, out _)
                || ForbiddenProvenanceSourceIdKeys.Contains(id))
            {
                throw new InvalidOperationException(
                    $"sourceId '{id}' cannot be a Brand DNA, color, research-report, job, or package provenance id.");
            }

            if (!knownSourceIds.Contains(id))
            {
                throw new InvalidOperationException($"Unknown or dangling factualClaim sourceId '{id}'.");
            }

            if (!seen.Add(id))
            {
                throw new InvalidOperationException($"Duplicate factualClaim sourceId '{id}'.");
            }

            ids.Add(id);
        }

        return new CanonicalWorkshopFactualClaim(statement, ids);
    }

    private static List<CanonicalWorkshopPrototype> ParsePrototypes(
        JsonNode? node,
        IReadOnlyList<string> expectedConceptIds,
        string channelFormat,
        int canvasWidth,
        int canvasHeight,
        IReadOnlySet<string> knownPaletteRoles)
    {
        if (node is not JsonArray arr || arr.Count != 3)
        {
            throw new InvalidOperationException("Production prototypes must contain exactly three items.");
        }

        var prototypes = new List<CanonicalWorkshopPrototype>(3);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException("Each prototype entry must be an object.");
            }

            AssertOnlyKnownProperties(obj, "conceptId", "spec");
            var conceptId = Required(obj["conceptId"]?.GetValue<string>(), "conceptId", 32);
            if (!ConceptIdSet.Contains(conceptId) || !seen.Add(conceptId))
            {
                throw new InvalidOperationException($"Invalid or duplicate prototype conceptId '{conceptId}'.");
            }

            if (obj["spec"] is not JsonObject spec)
            {
                throw new InvalidOperationException("prototype spec must be an object.");
            }

            var canonicalSpec = CanonicalizePrototypeSpec(
                spec, channelFormat, canvasWidth, canvasHeight, knownPaletteRoles);
            prototypes.Add(new CanonicalWorkshopPrototype(conceptId, canonicalSpec));
        }

        AssertExactConceptIds(seen);
        AssertConceptIdsMatch(expectedConceptIds, seen);
        return WeddingPlannerConceptIds.All
            .Select(id => prototypes.Single(p => p.ConceptId == id))
            .ToList();
    }

    public static JsonObject CanonicalizePrototypeSpec(
        JsonObject spec,
        string channelFormat,
        int canvasWidth,
        int canvasHeight,
        IReadOnlySet<string> knownPaletteRoles)
    {
        RejectForbiddenKeysEverywhere(spec);
        AssertOnlyKnownProperties(spec, "schemaVersion", "format", "canvas", "template", "regions");

        var schema = Required(spec["schemaVersion"]?.GetValue<string>(), "prototype.schemaVersion", 64);
        if (!string.Equals(schema, WeddingPlannerSchemaVersions.PrototypeSpecV1, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"prototype schemaVersion must be {WeddingPlannerSchemaVersions.PrototypeSpecV1}.");
        }

        var format = Required(spec["format"]?.GetValue<string>(), "prototype.format", 64);
        if (!string.Equals(format, channelFormat, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("prototype.format must equal the job ChannelFormat.");
        }

        if (spec["canvas"] is not JsonObject canvas)
        {
            throw new InvalidOperationException("prototype.canvas must be an object.");
        }

        AssertOnlyKnownProperties(canvas, "width", "height");
        var width = RequirePositiveInt(canvas["width"], "canvas.width");
        var height = RequirePositiveInt(canvas["height"], "canvas.height");
        if (width != canvasWidth || height != canvasHeight)
        {
            throw new InvalidOperationException(
                $"prototype.canvas must be exactly {canvasWidth}x{canvasHeight}.");
        }

        var template = Required(spec["template"]?.GetValue<string>(), "prototype.template", 64);
        if (!TemplateSet.Contains(template))
        {
            throw new InvalidOperationException(
                $"prototype.template must be one of: {string.Join(", ", WeddingPlannerPrototypeTemplates.All)}.");
        }

        if (spec["regions"] is not JsonArray regions
            || regions.Count < MinRegions
            || regions.Count > MaxRegions)
        {
            throw new InvalidOperationException(
                $"prototype.regions must contain between {MinRegions} and {MaxRegions} items.");
        }

        var regionNodes = new JsonArray();
        var regionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var regionNode in regions)
        {
            regionNodes.Add(CanonicalizeRegion(regionNode, width, height, knownPaletteRoles, regionIds));
        }

        return new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.PrototypeSpecV1,
            ["format"] = format,
            ["canvas"] = new JsonObject { ["width"] = width, ["height"] = height },
            ["template"] = template,
            ["regions"] = regionNodes
        };
    }

    private static JsonObject CanonicalizeRegion(
        JsonNode? node,
        int canvasWidth,
        int canvasHeight,
        IReadOnlySet<string> knownPaletteRoles,
        HashSet<string> regionIds)
    {
        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException("Each region must be an object.");
        }

        RejectForbiddenKeysEverywhere(obj);
        AssertOnlyKnownProperties(obj, "id", "type", "bounds", "textRef", "paletteRoleRef", "assetPlaceholder");

        var id = Required(obj["id"]?.GetValue<string>(), "region.id", MaxRegionIdLength);
        if (!regionIds.Add(id))
        {
            throw new InvalidOperationException($"Duplicate region id '{id}'.");
        }

        var type = Required(obj["type"]?.GetValue<string>(), "region.type", 64);
        if (!RegionTypeSet.Contains(type))
        {
            throw new InvalidOperationException($"Unknown region type '{type}'.");
        }

        if (obj["bounds"] is not JsonObject bounds)
        {
            throw new InvalidOperationException("region.bounds must be an object.");
        }

        AssertOnlyKnownProperties(bounds, "x", "y", "w", "h");
        var x = RequireNonNegativeInt(bounds["x"], "bounds.x");
        var y = RequireNonNegativeInt(bounds["y"], "bounds.y");
        var w = RequirePositiveInt(bounds["w"], "bounds.w");
        var h = RequirePositiveInt(bounds["h"], "bounds.h");
        if (x + w > canvasWidth || y + h > canvasHeight)
        {
            throw new InvalidOperationException(
                $"Region '{id}' bounds exceed canvas ({canvasWidth}x{canvasHeight}).");
        }

        var result = new JsonObject
        {
            ["id"] = id,
            ["type"] = type,
            ["bounds"] = new JsonObject { ["x"] = x, ["y"] = y, ["w"] = w, ["h"] = h }
        };

        if (obj["textRef"] is not null)
        {
            var textRef = Required(obj["textRef"]?.GetValue<string>(), "textRef", 64);
            if (!TextRefSet.Contains(textRef))
            {
                throw new InvalidOperationException(
                    "textRef must be copy.headline, copy.body, or copy.cta.");
            }

            result["textRef"] = textRef;
        }

        if (obj["paletteRoleRef"] is not null)
        {
            var paletteRole = Required(obj["paletteRoleRef"]?.GetValue<string>(), "paletteRoleRef", 64);
            if (!knownPaletteRoles.Contains(paletteRole))
            {
                throw new InvalidOperationException($"Unknown paletteRoleRef '{paletteRole}'.");
            }

            result["paletteRoleRef"] = paletteRole;
        }

        if (obj["assetPlaceholder"] is not null)
        {
            if (obj["assetPlaceholder"] is not JsonObject placeholder)
            {
                throw new InvalidOperationException("assetPlaceholder must be an object.");
            }

            AssertOnlyKnownProperties(placeholder, "kind", "label");
            var kind = Required(placeholder["kind"]?.GetValue<string>(), "assetPlaceholder.kind", 64);
            if (!AssetKindSet.Contains(kind))
            {
                throw new InvalidOperationException($"Unknown assetPlaceholder.kind '{kind}'.");
            }

            var label = Required(placeholder["label"]?.GetValue<string>(), "assetPlaceholder.label", MaxAssetPlaceholderLabelLength);
            if (label.Contains("://", StringComparison.Ordinal)
                || label.Contains('/', StringComparison.Ordinal)
                || label.Contains('\\', StringComparison.Ordinal)
                || label.Contains("data:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "assetPlaceholder.label must not contain URLs, paths, or data URIs.");
            }

            result["assetPlaceholder"] = new JsonObject
            {
                ["kind"] = kind,
                ["label"] = label
            };
        }

        return result;
    }

    private static JsonObject ToMergedConceptNode(CanonicalMergedConcept concept) =>
        new()
        {
            ["id"] = concept.Id,
            ["name"] = concept.Name,
            ["rationale"] = concept.Rationale,
            ["visualDirection"] = concept.VisualDirection,
            ["paletteRoleRefs"] = ToJsonArray(concept.PaletteRoleRefs),
            ["copy"] = new JsonObject
            {
                ["kind"] = concept.Copy.Kind,
                ["headline"] = concept.Copy.Headline,
                ["body"] = concept.Copy.Body,
                ["cta"] = concept.Copy.Cta
            },
            ["factualClaims"] = new JsonArray(concept.FactualClaims.Select(c => (JsonNode)new JsonObject
            {
                ["statement"] = c.Statement,
                ["sourceIds"] = ToJsonArray(c.SourceIds)
            }).ToArray()),
            ["prototype"] = concept.PrototypeSpec.DeepClone()
        };

    private static JsonObject ToArtContributionNode(CanonicalWorkshopContribution contribution) =>
        new()
        {
            ["logicalRole"] = contribution.LogicalRole,
            ["summary"] = contribution.Summary,
            ["concepts"] = new JsonArray(contribution.ArtDirections.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id,
                ["visualDirection"] = c.VisualDirection,
                ["paletteRoleRefs"] = ToJsonArray(c.PaletteRoleRefs)
            }).ToArray())
        };

    private static JsonObject ToCopyContributionNode(CanonicalWorkshopContribution contribution) =>
        new()
        {
            ["logicalRole"] = contribution.LogicalRole,
            ["summary"] = contribution.Summary,
            ["concepts"] = new JsonArray(contribution.CopyConcepts.Select(c => (JsonNode)new JsonObject
            {
                ["id"] = c.Id,
                ["copy"] = new JsonObject
                {
                    ["kind"] = c.Copy.Kind,
                    ["headline"] = c.Copy.Headline,
                    ["body"] = c.Copy.Body,
                    ["cta"] = c.Copy.Cta
                },
                ["factualClaims"] = new JsonArray(c.FactualClaims.Select(f => (JsonNode)new JsonObject
                {
                    ["statement"] = f.Statement,
                    ["sourceIds"] = ToJsonArray(f.SourceIds)
                }).ToArray())
            }).ToArray())
        };

    private static List<JsonObject> ParseContributionsArray(JsonObject root)
    {
        if (root["contributions"] is not JsonArray arr)
        {
            throw new InvalidOperationException("Stage contributions must be an array.");
        }

        var list = new List<JsonObject>(arr.Count);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException("Each contribution must be an object.");
            }

            list.Add(obj);
        }

        return list;
    }

    private static JsonObject ParseObject(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"{name} JSON is required.");
        }

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{name} JSON is invalid.", ex);
        }

        if (rootNode is not JsonObject root)
        {
            throw new InvalidOperationException($"{name} must be a JSON object.");
        }

        return root;
    }

    private static void AssertSchema(JsonObject root, string expected)
    {
        var schema = root["schemaVersion"]?.GetValue<string>();
        if (!string.Equals(schema, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"schemaVersion must be {expected}.");
        }
    }

    private static void AssertWorkerProfile(JsonObject root, string expected)
    {
        var profile = root["workerProfileVersion"]?.GetValue<string>();
        if (!string.Equals(profile, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"workerProfileVersion must be {expected}.");
        }
    }

    private static void AssertMarker(JsonObject root, bool requireSyntheticMarker)
    {
        if (!requireSyntheticMarker)
        {
            return;
        }

        var marker = root["marker"]?.GetValue<string>()?.Trim();
        if (!string.Equals(marker, WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Local Concept Workshop stage output must include marker SYNTHETIC DEVELOPMENT PROTOTYPE.");
        }
    }

    private static void AssertExactConceptIds(HashSet<string> seen)
    {
        if (seen.Count != 3 || !WeddingPlannerConceptIds.All.All(seen.Contains))
        {
            throw new InvalidOperationException(
                "Concept ids must be exactly concept_1, concept_2, and concept_3.");
        }
    }

    private static void AssertConceptIdsMatch(IReadOnlyList<string> expected, HashSet<string> seen)
    {
        if (expected.Count != seen.Count || !expected.All(seen.Contains))
        {
            throw new InvalidOperationException("Concept ids must match the strategy stage exactly.");
        }
    }

    private static void RejectForbiddenKeysEverywhere(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (ForbiddenKeySet.Contains(property.Key))
                {
                    throw new InvalidOperationException(
                        $"Forbidden key '{property.Key}' is not allowed in Concept Workshop JSON.");
                }

                RejectForbiddenKeysEverywhere(property.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                RejectForbiddenKeysEverywhere(item);
            }
        }
    }

    private static bool LooksLikeForbiddenAlias(string key)
    {
        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("matchid", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("campaignid", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("placementid", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("inventoryid", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("campaignready", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("qaapproved", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("blissready", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("imageprovider", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("assetprovider", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("imagebytes", StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertOnlyKnownProperties(JsonObject obj, params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in obj)
        {
            if (!allowedSet.Contains(property.Key))
            {
                throw new InvalidOperationException($"Unknown JSON property '{property.Key}' is not allowed.");
            }
        }
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonObject OrderObjectKeys(JsonObject source)
    {
        var ordered = new JsonObject();
        foreach (var property in source.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            ordered[property.Key] = property.Value is JsonObject child
                ? OrderObjectKeys(child)
                : property.Value is JsonArray arr
                    ? new JsonArray(arr.Select(x => x?.DeepClone()).ToArray())
                    : property.Value?.DeepClone();
        }

        return ordered;
    }

    private static IReadOnlyList<string> NormalizeStringList(
        IReadOnlyList<string>? values,
        string name,
        int maxCount,
        int maxItemLength)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        if (values.Count > maxCount)
        {
            throw new InvalidOperationException($"{name} cannot exceed {maxCount} items.");
        }

        return values.Select(v => Required(v, name, maxItemLength)).ToList();
    }

    private static int RequirePositiveInt(JsonNode? node, string name)
    {
        if (node is null || node.GetValueKind() != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"{name} must be a number.");
        }

        var value = node.GetValue<int>();
        if (value <= 0)
        {
            throw new InvalidOperationException($"{name} must be > 0.");
        }

        return value;
    }

    private static int RequireNonNegativeInt(JsonNode? node, string name)
    {
        if (node is null || node.GetValueKind() != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"{name} must be a number.");
        }

        var value = node.GetValue<int>();
        if (value < 0)
        {
            throw new InvalidOperationException($"{name} must be >= 0.");
        }

        return value;
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

public sealed record CanonicalWorkshopBrief(
    string Objective,
    string CampaignGoal,
    string AudienceFocus,
    string ChannelFormat,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<string> Deliverables,
    string Cta,
    IReadOnlyList<string> Constraints,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string InputJson,
    string InputSha256);

public sealed record CanonicalWorkshopStrategyConcept(string Id, string Name, string Rationale);

public sealed record CanonicalWorkshopArtDirection(
    string Id,
    string VisualDirection,
    IReadOnlyList<string> PaletteRoleRefs);

public sealed record CanonicalWorkshopCopy(string Kind, string Headline, string Body, string Cta);

public sealed record CanonicalWorkshopFactualClaim(string Statement, IReadOnlyList<string> SourceIds);

public sealed record CanonicalWorkshopCopyConcept(
    string Id,
    CanonicalWorkshopCopy Copy,
    IReadOnlyList<CanonicalWorkshopFactualClaim> FactualClaims);

public sealed record CanonicalWorkshopPrototype(string ConceptId, JsonObject SpecNode);

public sealed record CanonicalWorkshopContribution(
    string LogicalRole,
    string Summary,
    IReadOnlyList<CanonicalWorkshopStrategyConcept> StrategyConcepts,
    IReadOnlyList<CanonicalWorkshopArtDirection> ArtDirections,
    IReadOnlyList<CanonicalWorkshopCopyConcept> CopyConcepts,
    IReadOnlyList<CanonicalWorkshopPrototype> Prototypes);

public sealed record CanonicalWorkshopStageOutput(
    string OutputJson,
    string WorkerProfileVersion,
    IReadOnlyList<CanonicalWorkshopContribution> Contributions,
    IReadOnlyList<string> ConceptIds);

public sealed record CanonicalWorkshopContributionSummary(string LogicalRole, string Summary);

public sealed record CanonicalMergedConcept(
    string Id,
    string Name,
    string Rationale,
    string VisualDirection,
    IReadOnlyList<string> PaletteRoleRefs,
    CanonicalWorkshopCopy Copy,
    IReadOnlyList<CanonicalWorkshopFactualClaim> FactualClaims,
    JsonObject PrototypeSpec);

public sealed record CanonicalConceptPackage(
    string DocumentJson,
    string Summary,
    IReadOnlyList<CanonicalMergedConcept> Concepts,
    IReadOnlyList<CanonicalWorkshopContributionSummary> Contributions);
