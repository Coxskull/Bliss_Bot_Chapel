using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Strict validators/canonicalizers for Creative Production briefs, six stage outputs,
/// PNG-backed creative-package.v1 merge, and revision pin rules.
/// Never generates image bytes, fetches URLs, or executes markup.
/// Phase 5 concept roles remain pinned provenance and are never re-authored here.
/// </summary>
public static class WeddingPlannerCreativeDepartmentValidation
{
    public const int MaxObjectiveLength = 4000;
    public const int MaxRevisionNotesLength = 4000;
    public const int MinFormats = 1;
    public const int MaxFormats = 4;
    public const int MinVariants = 1;
    public const int MaxVariants = 4;
    public const int MaxContributionSummaryLength = 2000;
    public const int MaxCopyFieldLength = 1000;
    public const int MaxImagePromptLength = 2000;
    public const int MaxNegativeConstraintLength = 500;
    public const int MaxNegativeConstraints = 12;
    public const int MaxFactualClaimStatementLength = 2000;
    public const int MaxFactualClaimsPerVariant = 8;
    public const int MaxSourceIdsPerClaim = 10;
    public const int MaxSourceIdLength = 64;
    public const int MaxPaletteRoleRefs = 8;
    public const int MaxNotesLength = 2000;
    public const int MaxNorthStarLength = 2000;
    public const int MaxPrincipleLength = 500;
    public const int MaxPrinciples = 12;
    public const int MaxAtmosphereLength = 2000;
    public const int MaxGeometryLanguageLength = 2000;
    public const int MaxTypographyRoleLength = 64;
    public const int MaxMarkerLength = 128;
    public const int MaxVariantIdLength = 32;

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
    private static readonly HashSet<string> JobKindSet = new(WeddingPlannerCreativeProductionJobKinds.All, StringComparer.Ordinal);

    private static readonly HashSet<string> ForbiddenKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "css", "svg", "script", "src", "url", "href", "base64",
        "imageBytes", "image_bytes", "imageData", "image_data",
        "matchId", "matchingId", "blissMatchId", "matchIds", "matchingIds",
        "campaignId", "campaignIds",
        "placementId", "placementIds",
        "inventoryId", "inventoryIds", "adInventorySlotId",
        "campaignReady", "qaApproved", "blissReady", "legalCleared", "readiness",
        "imageProvider", "assetProvider", "imageTool", "assetUpload",
        "providerToolConfiguration", "assetBinary",
        "aiProvider", "aiEndpoint", "aiApiKey", "assetEndpoint", "assetApiKey",
        "conceptOverride", "alternateSelectedConceptId", "overrideSelectedConceptId",
        "overrideConceptPackageVersionId", "conceptPackageOverride"
    };

    private static readonly HashSet<string> ForbiddenProvenanceSourceIdKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "approvedBrandDnaVersionId",
        "approvedColorProfileVersionId",
        "approvedResearchReportVersionId",
        "approvedConceptPackageVersionId",
        "creativeProductionJobId",
        "creativePackageVersionId",
        "brandDnaVersionId",
        "colorProfileVersionId",
        "researchReportVersionId",
        "conceptPackageVersionId",
        "jobId",
        "packageId",
        "workshopJobId"
    };

    public static (int Width, int Height) CanvasForFormat(string format) =>
        format switch
        {
            WeddingPlannerChannelFormats.StaticSocialSquare => (1080, 1080),
            WeddingPlannerChannelFormats.StaticSocialStory => (1080, 1920),
            WeddingPlannerChannelFormats.StaticDisplayBanner => (1200, 628),
            WeddingPlannerChannelFormats.EmailHero => (1200, 600),
            _ => throw new InvalidOperationException($"Unknown format '{format}'.")
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

    public static CanonicalCreativeProductionBrief CanonicalizeBrief(
        string jobKind,
        string objective,
        IReadOnlyList<string> formats,
        int requestedVariantCount,
        Guid? revisionParentCreativePackageVersionId,
        string? revisionNotes,
        Guid approvedConceptPackageVersionId,
        string selectedConceptId,
        Guid approvedBrandDnaVersionId,
        int approvedBrandDnaVersionNumber,
        Guid approvedColorProfileVersionId,
        int approvedColorProfileVersionNumber,
        Guid approvedResearchReportVersionId,
        int approvedResearchReportVersionNumber,
        int? clientCanvasWidth,
        int? clientCanvasHeight,
        string aiProviderKind,
        string aiWorkerKey,
        string assetProviderKind,
        string assetWorkerKey)
    {
        var canonicalKind = Required(jobKind, "JobKind", 32).ToUpperInvariant();
        if (!JobKindSet.Contains(canonicalKind))
        {
            throw new InvalidOperationException("JobKind must be INITIAL or REVISION.");
        }

        var canonicalObjective = Required(objective, "Objective", MaxObjectiveLength);
        if (formats is null || formats.Count < MinFormats || formats.Count > MaxFormats)
        {
            throw new InvalidOperationException(
                $"Formats must contain between {MinFormats} and {MaxFormats} unique Phase 5 format values.");
        }

        var canonicalFormats = new List<string>(formats.Count);
        var formatSeen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var format in formats)
        {
            var f = Required(format, "Formats item", 64);
            if (!ChannelFormatSet.Contains(f))
            {
                throw new InvalidOperationException(
                    $"Unknown format '{f}'. Formats must be drawn from Phase 5's four channel formats.");
            }

            if (!formatSeen.Add(f))
            {
                throw new InvalidOperationException($"Duplicate format '{f}' is not allowed.");
            }

            canonicalFormats.Add(f);
        }

        if (requestedVariantCount < MinVariants
            || requestedVariantCount > MaxVariants
            || requestedVariantCount < canonicalFormats.Count)
        {
            throw new InvalidOperationException(
                $"RequestedVariantCount must be an integer 1–4 and ≥ Formats.Count ({canonicalFormats.Count}).");
        }

        string? canonicalRevisionNotes = null;
        Guid? canonicalParentId = null;
        if (canonicalKind == WeddingPlannerCreativeProductionJobKinds.Initial)
        {
            if (revisionParentCreativePackageVersionId is not null)
            {
                throw new InvalidOperationException("INITIAL forbids RevisionParentCreativePackageVersionId.");
            }

            if (!string.IsNullOrWhiteSpace(revisionNotes))
            {
                throw new InvalidOperationException("INITIAL forbids RevisionNotes.");
            }
        }
        else
        {
            if (revisionParentCreativePackageVersionId is null || revisionParentCreativePackageVersionId == Guid.Empty)
            {
                throw new InvalidOperationException("REVISION requires RevisionParentCreativePackageVersionId.");
            }

            canonicalParentId = revisionParentCreativePackageVersionId;
            canonicalRevisionNotes = Required(revisionNotes, "RevisionNotes", MaxRevisionNotesLength);
        }

        if (clientCanvasWidth is not null || clientCanvasHeight is not null)
        {
            // Clients must not supply alternate canvas; if any supplied they must match every
            // requested format's derived canvas — which is impossible when formats differ, so reject.
            throw new InvalidOperationException(
                "Client canvas width/height overrides are not allowed; canvas is server-derived per format.");
        }

        var canonicalSelectedConceptId = Required(selectedConceptId, "SelectedConceptId", 32);
        if (!ConceptIdSet.Contains(canonicalSelectedConceptId))
        {
            throw new InvalidOperationException("SelectedConceptId must be concept_1, concept_2, or concept_3.");
        }

        var payload = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CreativeProductionBriefV1,
            ["jobKind"] = canonicalKind,
            ["objective"] = canonicalObjective,
            ["formats"] = ToJsonArray(canonicalFormats),
            ["requestedVariantCount"] = requestedVariantCount,
            ["approvedConceptPackageVersionId"] = approvedConceptPackageVersionId.ToString("D"),
            ["selectedConceptId"] = canonicalSelectedConceptId,
            ["approvedBrandDnaVersionId"] = approvedBrandDnaVersionId.ToString("D"),
            ["approvedBrandDnaVersionNumber"] = approvedBrandDnaVersionNumber,
            ["approvedColorProfileVersionId"] = approvedColorProfileVersionId.ToString("D"),
            ["approvedColorProfileVersionNumber"] = approvedColorProfileVersionNumber,
            ["approvedResearchReportVersionId"] = approvedResearchReportVersionId.ToString("D"),
            ["approvedResearchReportVersionNumber"] = approvedResearchReportVersionNumber,
            ["aiProviderKind"] = Required(aiProviderKind, "aiProviderKind", 64),
            ["aiWorkerKey"] = Required(aiWorkerKey, "aiWorkerKey", 64),
            ["assetProviderKind"] = Required(assetProviderKind, "assetProviderKind", 64),
            ["assetWorkerKey"] = Required(assetWorkerKey, "assetWorkerKey", 64),
            ["creativeAssetProviderVersion"] = WeddingPlannerCreativeDepartmentContractVersions.CreativeAssetProviderV1,
            ["creativeDepartmentOrchestrationVersion"] =
                WeddingPlannerCreativeDepartmentContractVersions.CreativeDepartmentOrchestrationV1
        };

        if (canonicalParentId is Guid parentId)
        {
            payload["revisionParentCreativePackageVersionId"] = parentId.ToString("D");
            payload["revisionNotes"] = canonicalRevisionNotes;
        }

        RejectForbiddenBriefFields(payload);
        var ordered = OrderObjectKeys(payload);
        var json = ordered.ToJsonString(NodeWriteOptions);
        var sha = Sha256Hex(json);

        return new CanonicalCreativeProductionBrief(
            JobKind: canonicalKind,
            Objective: canonicalObjective,
            Formats: canonicalFormats,
            RequestedVariantCount: requestedVariantCount,
            RevisionParentCreativePackageVersionId: canonicalParentId,
            RevisionNotes: canonicalRevisionNotes,
            ApprovedConceptPackageVersionId: approvedConceptPackageVersionId,
            SelectedConceptId: canonicalSelectedConceptId,
            ApprovedBrandDnaVersionId: approvedBrandDnaVersionId,
            ApprovedBrandDnaVersionNumber: approvedBrandDnaVersionNumber,
            ApprovedColorProfileVersionId: approvedColorProfileVersionId,
            ApprovedColorProfileVersionNumber: approvedColorProfileVersionNumber,
            ApprovedResearchReportVersionId: approvedResearchReportVersionId,
            ApprovedResearchReportVersionNumber: approvedResearchReportVersionNumber,
            InputJson: json,
            InputSha256: sha);
    }

    public static IReadOnlySet<string> ExtractPaletteRoleNames(string colorProfileDocumentJson) =>
        WeddingPlannerConceptWorkshopValidation.ExtractPaletteRoleNames(colorProfileDocumentJson);

    public static IReadOnlySet<string> ExtractResearchSourceIds(string researchReportDocumentJson) =>
        WeddingPlannerConceptWorkshopValidation.ExtractResearchSourceIds(researchReportDocumentJson);

    public static SelectedConceptSnapshot ExtractSelectedConceptSnapshot(
        string conceptPackageDocumentJson,
        string selectedConceptId)
    {
        var root = ParseObject(conceptPackageDocumentJson, "Concept package");
        if (root["concepts"] is not JsonArray concepts)
        {
            throw new InvalidOperationException("Pinned concept package must contain concepts.");
        }

        JsonObject? match = null;
        foreach (var item in concepts)
        {
            if (item is not JsonObject obj)
            {
                continue;
            }

            var id = obj["id"]?.GetValue<string>();
            if (string.Equals(id, selectedConceptId, StringComparison.Ordinal))
            {
                match = obj;
                break;
            }
        }

        if (match is null)
        {
            throw new InvalidOperationException(
                $"SelectedConceptId '{selectedConceptId}' is not present in the pinned concept package.");
        }

        var name = Required(match["name"]?.GetValue<string>(), "selectedConcept.name", 200);
        var rationale = Required(match["rationale"]?.GetValue<string>(), "selectedConcept.rationale", 2000);
        var visualDirection = Required(match["visualDirection"]?.GetValue<string>(), "selectedConcept.visualDirection", 2000);
        var paletteRoleRefs = ParseStringList(match["paletteRoleRefs"], "paletteRoleRefs", MaxPaletteRoleRefs, 64);
        if (match["copy"] is not JsonObject copyObj)
        {
            throw new InvalidOperationException("Selected concept copy is required.");
        }

        AssertOnlyKnownProperties(copyObj, "kind", "headline", "body", "cta");
        var kind = Required(copyObj["kind"]?.GetValue<string>(), "copy.kind", 64);
        if (!string.Equals(kind, WeddingPlannerCopyKinds.CreativeNonFactual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Selected concept copy.kind must be {WeddingPlannerCopyKinds.CreativeNonFactual}.");
        }

        var copy = new CanonicalCreativeCopy(
            kind,
            Required(copyObj["headline"]?.GetValue<string>(), "copy.headline", MaxCopyFieldLength),
            Required(copyObj["body"]?.GetValue<string>(), "copy.body", MaxCopyFieldLength),
            Required(copyObj["cta"]?.GetValue<string>(), "copy.cta", MaxCopyFieldLength));

        var claims = new List<CanonicalCreativeFactualClaim>();
        if (match["factualClaims"] is JsonArray claimsArr)
        {
            foreach (var claimNode in claimsArr)
            {
                if (claimNode is not JsonObject claimObj)
                {
                    throw new InvalidOperationException("Each factual claim must be an object.");
                }

                AssertOnlyKnownProperties(claimObj, "statement", "sourceIds");
                var statement = Required(claimObj["statement"]?.GetValue<string>(), "statement", MaxFactualClaimStatementLength);
                var sourceIds = ParseStringList(claimObj["sourceIds"], "sourceIds", MaxSourceIdsPerClaim, MaxSourceIdLength);
                claims.Add(new CanonicalCreativeFactualClaim(statement, sourceIds));
            }
        }

        return new SelectedConceptSnapshot(
            selectedConceptId,
            name,
            rationale,
            visualDirection,
            paletteRoleRefs,
            copy,
            claims);
    }

    public static IReadOnlySet<string> ExtractVariantIdsFromPackage(string documentJson)
    {
        var root = ParseObject(documentJson, "Creative package");
        if (root["variants"] is not JsonArray variants)
        {
            throw new InvalidOperationException("Creative package variants must be an array.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in variants)
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

    public static CanonicalCreativeStageOutput CanonicalizeCreativeDirectionOutput(
        string? stageJson,
        string selectedConceptId,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Creative direction stage output",
            WeddingPlannerSchemaVersions.CreativeDirectionWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1,
            selectedConceptId,
            requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 2)
        {
            throw new InvalidOperationException("Creative direction stage must contain exactly two contributions.");
        }

        CanonicalCreativeContribution? director = null;
        CanonicalCreativeContribution? campaign = null;
        foreach (var contribution in contributions)
        {
            var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
            var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
            if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.CreativeDirector, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "direction");
                if (contribution["direction"] is not JsonObject direction)
                {
                    throw new InvalidOperationException("CREATIVE_DIRECTOR direction is required.");
                }

                AssertOnlyKnownProperties(direction, "northStar", "principles");
                var northStar = Required(direction["northStar"]?.GetValue<string>(), "direction.northStar", MaxNorthStarLength);
                var principles = ParseStringList(direction["principles"], "direction.principles", MaxPrinciples, MaxPrincipleLength);
                director = new CanonicalCreativeContribution(
                    role, summary,
                    Direction: new CanonicalCreativeDirection(northStar, principles));
            }
            else if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.CampaignStrategist, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "framing");
                if (contribution["framing"] is not JsonObject framing)
                {
                    throw new InvalidOperationException("CAMPAIGN_STRATEGIST framing is required.");
                }

                AssertOnlyKnownProperties(framing, "objectiveEcho", "formatPlanNotes");
                campaign = new CanonicalCreativeContribution(
                    role, summary,
                    Framing: new CanonicalCampaignFraming(
                        Required(framing["objectiveEcho"]?.GetValue<string>(), "framing.objectiveEcho", MaxNotesLength),
                        Required(framing["formatPlanNotes"]?.GetValue<string>(), "framing.formatPlanNotes", MaxNotesLength)));
            }
            else
            {
                throw new InvalidOperationException($"Creative direction stage contains unexpected logicalRole '{role}'.");
            }
        }

        if (director is null || campaign is null)
        {
            throw new InvalidOperationException(
                "Creative direction stage must include exactly CREATIVE_DIRECTOR and CAMPAIGN_STRATEGIST.");
        }

        return BuildStageOutput(
            root,
            WeddingPlannerSchemaVersions.CreativeDirectionWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1,
            selectedConceptId,
            requireSyntheticMarker,
            [director, campaign],
            c => c.LogicalRole switch
            {
                WeddingPlannerCreativeDepartmentLogicalRoles.CreativeDirector => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["direction"] = new JsonObject
                    {
                        ["northStar"] = c.Direction!.NorthStar,
                        ["principles"] = ToJsonArray(c.Direction.Principles)
                    }
                },
                _ => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["framing"] = new JsonObject
                    {
                        ["objectiveEcho"] = c.Framing!.ObjectiveEcho,
                        ["formatPlanNotes"] = c.Framing.FormatPlanNotes
                    }
                }
            });
    }

    public static CanonicalCreativeStageOutput CanonicalizeStrategyAdaptationOutput(
        string? stageJson,
        string selectedConceptId,
        IReadOnlyList<string> briefFormats,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Strategy adaptation stage output",
            WeddingPlannerSchemaVersions.StrategyAdaptationWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1,
            selectedConceptId,
            requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 3)
        {
            throw new InvalidOperationException("Strategy adaptation stage must contain exactly three contributions.");
        }

        CanonicalCreativeContribution? audience = null;
        CanonicalCreativeContribution? offer = null;
        CanonicalCreativeContribution? channel = null;
        var briefFormatSet = new HashSet<string>(briefFormats, StringComparer.Ordinal);

        foreach (var contribution in contributions)
        {
            var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
            var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
            if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.AudienceStrategist, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "audienceAdaptation");
                if (contribution["audienceAdaptation"] is not JsonObject adapt)
                {
                    throw new InvalidOperationException("AUDIENCE_STRATEGIST audienceAdaptation is required.");
                }

                AssertOnlyKnownProperties(adapt, "notes");
                audience = new CanonicalCreativeContribution(
                    role, summary,
                    AudienceNotes: Required(adapt["notes"]?.GetValue<string>(), "audienceAdaptation.notes", MaxNotesLength));
            }
            else if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.OfferStrategist, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "offerFraming");
                if (contribution["offerFraming"] is not JsonObject offerObj)
                {
                    throw new InvalidOperationException("OFFER_STRATEGIST offerFraming is required.");
                }

                AssertOnlyKnownProperties(offerObj, "notes");
                offer = new CanonicalCreativeContribution(
                    role, summary,
                    OfferNotes: Required(offerObj["notes"]?.GetValue<string>(), "offerFraming.notes", MaxNotesLength));
            }
            else if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.ChannelStrategist, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "channelAdaptation");
                if (contribution["channelAdaptation"] is not JsonObject channelObj)
                {
                    throw new InvalidOperationException("CHANNEL_STRATEGIST channelAdaptation is required.");
                }

                AssertOnlyKnownProperties(channelObj, "formats");
                var formats = ParseStringList(channelObj["formats"], "channelAdaptation.formats", MaxFormats, 64);
                foreach (var f in formats)
                {
                    if (!briefFormatSet.Contains(f))
                    {
                        throw new InvalidOperationException(
                            $"Channel adaptation format '{f}' is not present in the brief.");
                    }
                }

                channel = new CanonicalCreativeContribution(role, summary, ChannelFormats: formats);
            }
            else
            {
                throw new InvalidOperationException($"Strategy adaptation stage contains unexpected logicalRole '{role}'.");
            }
        }

        if (audience is null || offer is null || channel is null)
        {
            throw new InvalidOperationException(
                "Strategy adaptation stage must include AUDIENCE_STRATEGIST, OFFER_STRATEGIST, and CHANNEL_STRATEGIST.");
        }

        return BuildStageOutput(
            root,
            WeddingPlannerSchemaVersions.StrategyAdaptationWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1,
            selectedConceptId,
            requireSyntheticMarker,
            [audience, offer, channel],
            c => c.LogicalRole switch
            {
                WeddingPlannerCreativeDepartmentLogicalRoles.AudienceStrategist => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["audienceAdaptation"] = new JsonObject { ["notes"] = c.AudienceNotes }
                },
                WeddingPlannerCreativeDepartmentLogicalRoles.OfferStrategist => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["offerFraming"] = new JsonObject { ["notes"] = c.OfferNotes }
                },
                _ => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["channelAdaptation"] = new JsonObject { ["formats"] = ToJsonArray(c.ChannelFormats!) }
                }
            });
    }

    public static CanonicalCreativeStageOutput CanonicalizeVisualSystemOutput(
        string? stageJson,
        string selectedConceptId,
        IReadOnlySet<string> knownPaletteRoles,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Visual system stage output",
            WeddingPlannerSchemaVersions.VisualSystemWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1,
            selectedConceptId,
            requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 3)
        {
            throw new InvalidOperationException("Visual system stage must contain exactly three contributions.");
        }

        CanonicalCreativeContribution? visual = null;
        CanonicalCreativeContribution? layout = null;
        CanonicalCreativeContribution? typography = null;

        foreach (var contribution in contributions)
        {
            var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
            var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
            if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.VisualDesigner, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "visualSystem");
                if (contribution["visualSystem"] is not JsonObject vs)
                {
                    throw new InvalidOperationException("VISUAL_DESIGNER visualSystem is required.");
                }

                AssertOnlyKnownProperties(vs, "paletteRoleRefs", "atmosphere");
                var refs = ParsePaletteRoleRefs(vs["paletteRoleRefs"], knownPaletteRoles);
                visual = new CanonicalCreativeContribution(
                    role, summary,
                    VisualSystem: new CanonicalVisualSystem(
                        refs,
                        Required(vs["atmosphere"]?.GetValue<string>(), "visualSystem.atmosphere", MaxAtmosphereLength)));
            }
            else if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.LayoutDesigner, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "layoutSystem");
                if (contribution["layoutSystem"] is not JsonObject ls)
                {
                    throw new InvalidOperationException("LAYOUT_DESIGNER layoutSystem is required.");
                }

                AssertOnlyKnownProperties(ls, "geometryLanguage");
                layout = new CanonicalCreativeContribution(
                    role, summary,
                    LayoutGeometry: Required(ls["geometryLanguage"]?.GetValue<string>(), "layoutSystem.geometryLanguage", MaxGeometryLanguageLength));
            }
            else if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.TypographyDesigner, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "typographySystem");
                if (contribution["typographySystem"] is not JsonObject ts)
                {
                    throw new InvalidOperationException("TYPOGRAPHY_DESIGNER typographySystem is required.");
                }

                AssertOnlyKnownProperties(ts, "headlineRole", "bodyRole", "ctaRole");
                typography = new CanonicalCreativeContribution(
                    role, summary,
                    Typography: new CanonicalTypographySystem(
                        Required(ts["headlineRole"]?.GetValue<string>(), "typographySystem.headlineRole", MaxTypographyRoleLength),
                        Required(ts["bodyRole"]?.GetValue<string>(), "typographySystem.bodyRole", MaxTypographyRoleLength),
                        Required(ts["ctaRole"]?.GetValue<string>(), "typographySystem.ctaRole", MaxTypographyRoleLength)));
            }
            else
            {
                throw new InvalidOperationException($"Visual system stage contains unexpected logicalRole '{role}'.");
            }
        }

        if (visual is null || layout is null || typography is null)
        {
            throw new InvalidOperationException(
                "Visual system stage must include VISUAL_DESIGNER, LAYOUT_DESIGNER, and TYPOGRAPHY_DESIGNER.");
        }

        return BuildStageOutput(
            root,
            WeddingPlannerSchemaVersions.VisualSystemWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1,
            selectedConceptId,
            requireSyntheticMarker,
            [visual, layout, typography],
            c => c.LogicalRole switch
            {
                WeddingPlannerCreativeDepartmentLogicalRoles.VisualDesigner => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["visualSystem"] = new JsonObject
                    {
                        ["paletteRoleRefs"] = ToJsonArray(c.VisualSystem!.PaletteRoleRefs),
                        ["atmosphere"] = c.VisualSystem.Atmosphere
                    }
                },
                WeddingPlannerCreativeDepartmentLogicalRoles.LayoutDesigner => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["layoutSystem"] = new JsonObject { ["geometryLanguage"] = c.LayoutGeometry }
                },
                _ => new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["typographySystem"] = new JsonObject
                    {
                        ["headlineRole"] = c.Typography!.HeadlineRole,
                        ["bodyRole"] = c.Typography.BodyRole,
                        ["ctaRole"] = c.Typography.CtaRole
                    }
                }
            });
    }

    public static CanonicalCreativeStageOutput CanonicalizeImageDirectionOutput(
        string? stageJson,
        string selectedConceptId,
        int requestedVariantCount,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Image direction stage output",
            WeddingPlannerSchemaVersions.ImageDirectionWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1,
            selectedConceptId,
            requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 1)
        {
            throw new InvalidOperationException("Image direction stage must contain exactly one contribution.");
        }

        var contribution = contributions[0];
        AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "imagePrompts");
        var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.ImagePromptDesigner, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Image direction stage contribution must be IMAGE_PROMPT_DESIGNER.");
        }

        var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
        var expectedIds = WeddingPlannerVariantIds.ForCount(requestedVariantCount);
        var prompts = ParseImagePrompts(contribution["imagePrompts"], expectedIds);
        var canonical = new CanonicalCreativeContribution(role, summary, ImagePrompts: prompts);

        return BuildStageOutput(
            root,
            WeddingPlannerSchemaVersions.ImageDirectionWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1,
            selectedConceptId,
            requireSyntheticMarker,
            [canonical],
            c => new JsonObject
            {
                ["logicalRole"] = c.LogicalRole,
                ["summary"] = c.Summary,
                ["imagePrompts"] = new JsonArray(c.ImagePrompts!.Select(p => (JsonNode)new JsonObject
                {
                    ["variantId"] = p.VariantId,
                    ["prompt"] = p.Prompt,
                    ["negativeConstraints"] = ToJsonArray(p.NegativeConstraints)
                }).ToArray())
            });
    }

    public static CanonicalCreativeStageOutput CanonicalizeCopySystemOutput(
        string? stageJson,
        string selectedConceptId,
        int requestedVariantCount,
        IReadOnlyList<CanonicalCreativeFactualClaim> preservedClaims,
        IReadOnlySet<string> forbiddenSourceIds,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Copy system stage output",
            WeddingPlannerSchemaVersions.CopySystemWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1,
            selectedConceptId,
            requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 3)
        {
            throw new InvalidOperationException("Copy system stage must contain exactly three contributions.");
        }

        var expectedIds = WeddingPlannerVariantIds.ForCount(requestedVariantCount);
        CanonicalCreativeContribution? headlines = null;
        CanonicalCreativeContribution? bodies = null;
        CanonicalCreativeContribution? ctas = null;

        foreach (var contribution in contributions)
        {
            var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
            var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
            if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "headlines");
                headlines = new CanonicalCreativeContribution(
                    role, summary,
                    Headlines: ParseVariantTexts(contribution["headlines"], expectedIds, "headlines", allowClaims: false, preservedClaims, forbiddenSourceIds));
            }
            else if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "bodies");
                bodies = new CanonicalCreativeContribution(
                    role, summary,
                    Bodies: ParseVariantTexts(contribution["bodies"], expectedIds, "bodies", allowClaims: true, preservedClaims, forbiddenSourceIds));
            }
            else if (string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.CtaSpecialist, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "ctas");
                ctas = new CanonicalCreativeContribution(
                    role, summary,
                    Ctas: ParseVariantTexts(contribution["ctas"], expectedIds, "ctas", allowClaims: false, preservedClaims, forbiddenSourceIds));
            }
            else
            {
                throw new InvalidOperationException($"Copy system stage contains unexpected logicalRole '{role}'.");
            }
        }

        if (headlines is null || bodies is null || ctas is null)
        {
            throw new InvalidOperationException(
                "Copy system stage must include HEADLINE_SPECIALIST, BODY_COPY_SPECIALIST, and CTA_SPECIALIST.");
        }

        return BuildStageOutput(
            root,
            WeddingPlannerSchemaVersions.CopySystemWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1,
            selectedConceptId,
            requireSyntheticMarker,
            [headlines, bodies, ctas],
            c =>
            {
                if (c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist)
                {
                    return new JsonObject
                    {
                        ["logicalRole"] = c.LogicalRole,
                        ["summary"] = c.Summary,
                        ["headlines"] = ToVariantTextArray(c.Headlines!, includeClaims: false)
                    };
                }

                if (c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist)
                {
                    return new JsonObject
                    {
                        ["logicalRole"] = c.LogicalRole,
                        ["summary"] = c.Summary,
                        ["bodies"] = ToVariantTextArray(c.Bodies!, includeClaims: true)
                    };
                }

                return new JsonObject
                {
                    ["logicalRole"] = c.LogicalRole,
                    ["summary"] = c.Summary,
                    ["ctas"] = ToVariantTextArray(c.Ctas!, includeClaims: false)
                };
            });
    }

    public static CanonicalCreativeStageOutput CanonicalizeVariantProductionOutput(
        string? stageJson,
        string selectedConceptId,
        CanonicalCreativeProductionBrief brief,
        CanonicalCreativeStageOutput imageDirection,
        CanonicalCreativeStageOutput copySystem,
        IReadOnlySet<string> knownPaletteRoles,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Variant production stage output",
            WeddingPlannerSchemaVersions.VariantProductionWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1,
            selectedConceptId,
            requireSyntheticMarker);

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 1)
        {
            throw new InvalidOperationException("Variant production stage must contain exactly one contribution.");
        }

        var contribution = contributions[0];
        AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "variants");
        var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!string.Equals(role, WeddingPlannerCreativeDepartmentLogicalRoles.VariantProducer, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Variant production stage contribution must be VARIANT_PRODUCER.");
        }

        var summary = Required(contribution["summary"]?.GetValue<string>(), "contribution.summary", MaxContributionSummaryLength);
        var expectedIds = WeddingPlannerVariantIds.ForCount(brief.RequestedVariantCount);
        var imagePromptIds = imageDirection.Contributions
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.ImagePromptDesigner)
            .ImagePrompts!
            .Select(p => p.VariantId)
            .ToHashSet(StringComparer.Ordinal);
        var headlineIds = copySystem.Contributions
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist)
            .Headlines!
            .Select(h => h.VariantId)
            .ToHashSet(StringComparer.Ordinal);
        var bodyIds = copySystem.Contributions
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist)
            .Bodies!
            .Select(b => b.VariantId)
            .ToHashSet(StringComparer.Ordinal);
        var ctaIds = copySystem.Contributions
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.CtaSpecialist)
            .Ctas!
            .Select(c => c.VariantId)
            .ToHashSet(StringComparer.Ordinal);

        var variants = ParseVariants(
            contribution["variants"],
            expectedIds,
            brief.Formats,
            knownPaletteRoles,
            imagePromptIds,
            headlineIds,
            bodyIds,
            ctaIds);

        var canonical = new CanonicalCreativeContribution(role, summary, Variants: variants);
        return BuildStageOutput(
            root,
            WeddingPlannerSchemaVersions.VariantProductionWorkerOutputV1,
            WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1,
            selectedConceptId,
            requireSyntheticMarker,
            [canonical],
            c => new JsonObject
            {
                ["logicalRole"] = c.LogicalRole,
                ["summary"] = c.Summary,
                ["variants"] = new JsonArray(c.Variants!.Select(v => (JsonNode)new JsonObject
                {
                    ["id"] = v.Id,
                    ["format"] = v.Format,
                    ["canvas"] = new JsonObject { ["width"] = v.Width, ["height"] = v.Height },
                    ["refs"] = new JsonObject
                    {
                        ["direction"] = v.Refs.Direction,
                        ["visualSystem"] = v.Refs.VisualSystem,
                        ["layout"] = v.Refs.Layout,
                        ["typography"] = v.Refs.Typography,
                        ["imagePrompt"] = v.Refs.ImagePrompt,
                        ["headline"] = v.Refs.Headline,
                        ["body"] = v.Refs.Body,
                        ["cta"] = v.Refs.Cta
                    },
                    ["paletteRoleRefs"] = ToJsonArray(v.PaletteRoleRefs)
                }).ToArray())
            });
    }

    public static CanonicalCreativePackagePlan MergePackagePlan(
        CanonicalCreativeProductionBrief brief,
        SelectedConceptSnapshot selectedConcept,
        IReadOnlyList<CanonicalCreativeStageOutput> stages,
        Guid creativeProductionJobId,
        bool requireSyntheticMarker)
    {
        if (stages.Count != 6)
        {
            throw new InvalidOperationException("Merge requires exactly six stage outputs.");
        }

        var variantProduction = stages[^1].Contributions
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.VariantProducer);
        var imagePrompts = stages
            .SelectMany(s => s.Contributions)
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.ImagePromptDesigner)
            .ImagePrompts!;
        var headlines = stages
            .SelectMany(s => s.Contributions)
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist)
            .Headlines!;
        var bodies = stages
            .SelectMany(s => s.Contributions)
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist)
            .Bodies!;
        var ctas = stages
            .SelectMany(s => s.Contributions)
            .Single(c => c.LogicalRole == WeddingPlannerCreativeDepartmentLogicalRoles.CtaSpecialist)
            .Ctas!;

        var expectedIds = WeddingPlannerVariantIds.ForCount(brief.RequestedVariantCount);
        if (variantProduction.Variants is null || variantProduction.Variants.Count != expectedIds.Count)
        {
            throw new InvalidOperationException($"Merged package requires exactly {expectedIds.Count} variants.");
        }

        var usedFormats = new HashSet<string>(StringComparer.Ordinal);
        var plannedVariants = new List<CanonicalCreativePlannedVariant>(expectedIds.Count);
        foreach (var expectedId in expectedIds)
        {
            var variant = variantProduction.Variants.Single(v => v.Id == expectedId);
            usedFormats.Add(variant.Format);
            var prompt = imagePrompts.Single(p => p.VariantId == expectedId);
            var headline = headlines.Single(h => h.VariantId == expectedId);
            var body = bodies.Single(b => b.VariantId == expectedId);
            var cta = ctas.Single(c => c.VariantId == expectedId);
            plannedVariants.Add(new CanonicalCreativePlannedVariant(
                variant.Id,
                variant.Format,
                variant.Width,
                variant.Height,
                variant.PaletteRoleRefs,
                new CanonicalCreativeCopy(
                    WeddingPlannerCopyKinds.CreativeNonFactual,
                    headline.Text,
                    body.Text,
                    cta.Text),
                body.FactualClaims,
                prompt.Prompt,
                Asset: null));
        }

        foreach (var format in brief.Formats)
        {
            if (!usedFormats.Contains(format))
            {
                throw new InvalidOperationException($"Merged package must use brief format '{format}' at least once.");
            }
        }

        var contributionSummaries = WeddingPlannerCreativeDepartmentLogicalRoles.AllInOrder
            .Select(role =>
            {
                var contribution = stages.SelectMany(s => s.Contributions).Single(c => c.LogicalRole == role);
                return new CanonicalCreativeContributionSummary(role, contribution.Summary);
            })
            .ToList();

        if (contributionSummaries.Count != 13)
        {
            throw new InvalidOperationException("Merged package requires exactly 13 contributions.");
        }

        return new CanonicalCreativePackagePlan(
            brief,
            selectedConcept,
            plannedVariants,
            contributionSummaries,
            stages.SelectMany(s => s.Contributions).ToList(),
            creativeProductionJobId,
            requireSyntheticMarker);
    }

    public static CanonicalCreativePackage FinalizePackageDocument(
        CanonicalCreativePackagePlan plan,
        IReadOnlyList<CanonicalCreativeAssetRef> assets)
    {
        if (assets.Count != plan.Variants.Count)
        {
            throw new InvalidOperationException("Package finalization requires exactly one asset per variant.");
        }

        if (assets.Count > WeddingPlannerPngValidator.MaxAssetsPerPackage)
        {
            throw new InvalidOperationException(
                $"Package cannot exceed {WeddingPlannerPngValidator.MaxAssetsPerPackage} assets.");
        }

        var assetByVariant = assets.ToDictionary(a => a.VariantId, StringComparer.Ordinal);
        var finalizedVariants = new List<CanonicalCreativePlannedVariant>(plan.Variants.Count);
        foreach (var variant in plan.Variants)
        {
            if (!assetByVariant.TryGetValue(variant.Id, out var asset))
            {
                throw new InvalidOperationException($"Missing asset for variant '{variant.Id}'.");
            }

            if (asset.Width != variant.Width || asset.Height != variant.Height)
            {
                throw new InvalidOperationException($"Asset dimensions mismatch for variant '{variant.Id}'.");
            }

            if (!string.Equals(asset.ContentType, WeddingPlannerCreativeAssetContentTypes.ImagePng, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Creative assets must be image/png.");
            }

            finalizedVariants.Add(variant with { Asset = asset });
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CreativePackageV1,
            ["disclaimer"] = WeddingPlannerCreativePackageDisclaimer.Text,
            ["provenance"] = new JsonObject
            {
                ["approvedConceptPackageVersionId"] = plan.Brief.ApprovedConceptPackageVersionId.ToString("D"),
                ["selectedConceptId"] = plan.Brief.SelectedConceptId,
                ["approvedBrandDnaVersionId"] = plan.Brief.ApprovedBrandDnaVersionId.ToString("D"),
                ["approvedBrandDnaVersionNumber"] = plan.Brief.ApprovedBrandDnaVersionNumber,
                ["approvedColorProfileVersionId"] = plan.Brief.ApprovedColorProfileVersionId.ToString("D"),
                ["approvedColorProfileVersionNumber"] = plan.Brief.ApprovedColorProfileVersionNumber,
                ["approvedResearchReportVersionId"] = plan.Brief.ApprovedResearchReportVersionId.ToString("D"),
                ["approvedResearchReportVersionNumber"] = plan.Brief.ApprovedResearchReportVersionNumber,
                ["creativeProductionJobId"] = plan.CreativeProductionJobId.ToString("D"),
                ["jobKind"] = plan.Brief.JobKind,
                ["parentCreativePackageVersionId"] = plan.Brief.RevisionParentCreativePackageVersionId is Guid parent
                    ? parent.ToString("D")
                    : null
            },
            ["selectedConceptSnapshot"] = new JsonObject
            {
                ["id"] = plan.SelectedConcept.Id,
                ["name"] = plan.SelectedConcept.Name,
                ["rationale"] = plan.SelectedConcept.Rationale,
                ["visualDirection"] = plan.SelectedConcept.VisualDirection,
                ["paletteRoleRefs"] = ToJsonArray(plan.SelectedConcept.PaletteRoleRefs),
                ["copy"] = new JsonObject
                {
                    ["kind"] = plan.SelectedConcept.Copy.Kind,
                    ["headline"] = plan.SelectedConcept.Copy.Headline,
                    ["body"] = plan.SelectedConcept.Copy.Body,
                    ["cta"] = plan.SelectedConcept.Copy.Cta
                },
                ["factualClaims"] = new JsonArray(plan.SelectedConcept.FactualClaims.Select(ToClaimNode).ToArray())
            },
            ["brief"] = new JsonObject
            {
                ["objective"] = plan.Brief.Objective,
                ["formats"] = ToJsonArray(plan.Brief.Formats),
                ["requestedVariantCount"] = plan.Brief.RequestedVariantCount,
                ["revisionNotes"] = plan.Brief.RevisionNotes
            },
            ["variants"] = new JsonArray(finalizedVariants.Select(v => (JsonNode)new JsonObject
            {
                ["id"] = v.Id,
                ["format"] = v.Format,
                ["canvas"] = new JsonObject { ["width"] = v.Width, ["height"] = v.Height },
                ["paletteRoleRefs"] = ToJsonArray(v.PaletteRoleRefs),
                ["copy"] = new JsonObject
                {
                    ["kind"] = v.Copy.Kind,
                    ["headline"] = v.Copy.Headline,
                    ["body"] = v.Copy.Body,
                    ["cta"] = v.Copy.Cta
                },
                ["factualClaims"] = new JsonArray(v.FactualClaims.Select(ToClaimNode).ToArray()),
                ["imagePrompt"] = v.ImagePrompt,
                ["asset"] = new JsonObject
                {
                    ["creativeAssetId"] = v.Asset!.CreativeAssetId.ToString("D"),
                    ["contentType"] = v.Asset.ContentType,
                    ["byteSize"] = v.Asset.ByteSize,
                    ["sha256"] = v.Asset.Sha256,
                    ["width"] = v.Asset.Width,
                    ["height"] = v.Asset.Height
                }
            }).ToArray()),
            ["contributions"] = new JsonArray(plan.ContributionSummaries.Select(c => (JsonNode)new JsonObject
            {
                ["logicalRole"] = c.LogicalRole,
                ["summary"] = c.Summary
            }).ToArray())
        };

        if (plan.RequireSyntheticMarker)
        {
            document["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage;
        }

        RejectForbiddenKeysEverywhere(document);
        AssertNoEmbeddedAssetBytesOrUrls(document);

        var json = document.ToJsonString(NodeWriteOptions);
        if (!json.Contains(WeddingPlannerCreativePackageDisclaimer.Text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Merged creative package must include the exact draft-creative disclaimer.");
        }

        if (plan.RequireSyntheticMarker
            && !json.Contains(WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Local creative packages must include the SYNTHETIC DEVELOPMENT CREATIVE PACKAGE marker.");
        }

        if (plan.ContributionSummaries.Count != 13)
        {
            throw new InvalidOperationException("Merged creative package requires exactly 13 contributions.");
        }

        var summary = Truncate(
            $"{WeddingPlannerSchemaVersions.CreativePackageV1}: {plan.Brief.JobKind} — {plan.Brief.SelectedConceptId}, {finalizedVariants.Count} variants, 13 contributions, formats [{string.Join(", ", plan.Brief.Formats)}].",
            2000);

        return new CanonicalCreativePackage(json, summary, finalizedVariants, plan.ContributionSummaries, plan.RoleContributions);
    }

    public static string SerializeAssignedRolesJson(IReadOnlyList<string> roles) =>
        JsonSerializer.Serialize(roles, CanonicalJsonOptions);

    public static string SerializeContributionJson(CanonicalCreativeContribution contribution)
    {
        var node = contribution.LogicalRole switch
        {
            WeddingPlannerCreativeDepartmentLogicalRoles.CreativeDirector => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["direction"] = new JsonObject
                {
                    ["northStar"] = contribution.Direction!.NorthStar,
                    ["principles"] = ToJsonArray(contribution.Direction.Principles)
                }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.CampaignStrategist => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["framing"] = new JsonObject
                {
                    ["objectiveEcho"] = contribution.Framing!.ObjectiveEcho,
                    ["formatPlanNotes"] = contribution.Framing.FormatPlanNotes
                }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.AudienceStrategist => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["audienceAdaptation"] = new JsonObject { ["notes"] = contribution.AudienceNotes }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.OfferStrategist => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["offerFraming"] = new JsonObject { ["notes"] = contribution.OfferNotes }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.ChannelStrategist => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["channelAdaptation"] = new JsonObject { ["formats"] = ToJsonArray(contribution.ChannelFormats!) }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.VisualDesigner => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["visualSystem"] = new JsonObject
                {
                    ["paletteRoleRefs"] = ToJsonArray(contribution.VisualSystem!.PaletteRoleRefs),
                    ["atmosphere"] = contribution.VisualSystem.Atmosphere
                }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.LayoutDesigner => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["layoutSystem"] = new JsonObject { ["geometryLanguage"] = contribution.LayoutGeometry }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.TypographyDesigner => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["typographySystem"] = new JsonObject
                {
                    ["headlineRole"] = contribution.Typography!.HeadlineRole,
                    ["bodyRole"] = contribution.Typography.BodyRole,
                    ["ctaRole"] = contribution.Typography.CtaRole
                }
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.ImagePromptDesigner => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["imagePrompts"] = new JsonArray(contribution.ImagePrompts!.Select(p => (JsonNode)new JsonObject
                {
                    ["variantId"] = p.VariantId,
                    ["prompt"] = p.Prompt,
                    ["negativeConstraints"] = ToJsonArray(p.NegativeConstraints)
                }).ToArray())
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["headlines"] = ToVariantTextArray(contribution.Headlines!, includeClaims: false)
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["bodies"] = ToVariantTextArray(contribution.Bodies!, includeClaims: true)
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.CtaSpecialist => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["ctas"] = ToVariantTextArray(contribution.Ctas!, includeClaims: false)
            },
            WeddingPlannerCreativeDepartmentLogicalRoles.VariantProducer => new JsonObject
            {
                ["logicalRole"] = contribution.LogicalRole,
                ["summary"] = contribution.Summary,
                ["variants"] = new JsonArray(contribution.Variants!.Select(v => (JsonNode)new JsonObject
                {
                    ["id"] = v.Id,
                    ["format"] = v.Format,
                    ["canvas"] = new JsonObject { ["width"] = v.Width, ["height"] = v.Height },
                    ["refs"] = new JsonObject
                    {
                        ["direction"] = v.Refs.Direction,
                        ["visualSystem"] = v.Refs.VisualSystem,
                        ["layout"] = v.Refs.Layout,
                        ["typography"] = v.Refs.Typography,
                        ["imagePrompt"] = v.Refs.ImagePrompt,
                        ["headline"] = v.Refs.Headline,
                        ["body"] = v.Refs.Body,
                        ["cta"] = v.Refs.Cta
                    },
                    ["paletteRoleRefs"] = ToJsonArray(v.PaletteRoleRefs)
                }).ToArray())
            },
            _ => throw new InvalidOperationException($"Unknown creative contribution role '{contribution.LogicalRole}'.")
        };

        RejectForbiddenKeysEverywhere(node);
        return node.ToJsonString(NodeWriteOptions);
    }

    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string BuildRenderSpecJson(CanonicalCreativePlannedVariant variant, CanonicalCreativeProductionBrief brief) =>
        new JsonObject
        {
            ["variantId"] = variant.Id,
            ["format"] = variant.Format,
            ["canvas"] = new JsonObject { ["width"] = variant.Width, ["height"] = variant.Height },
            ["copy"] = new JsonObject
            {
                ["kind"] = variant.Copy.Kind,
                ["headline"] = variant.Copy.Headline,
                ["body"] = variant.Copy.Body,
                ["cta"] = variant.Copy.Cta
            },
            ["paletteRoleRefs"] = ToJsonArray(variant.PaletteRoleRefs),
            ["imagePrompt"] = variant.ImagePrompt,
            ["selectedConceptId"] = brief.SelectedConceptId,
            ["objective"] = brief.Objective
        }.ToJsonString(NodeWriteOptions);

    private static JsonObject ParseStageRoot(
        string? stageJson,
        string label,
        string schemaVersion,
        string workerProfile,
        string selectedConceptId,
        bool requireSyntheticMarker)
    {
        var root = ParseObject(stageJson, label);
        RejectForbiddenKeysEverywhere(root);
        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "marker", "selectedConceptId", "contributions");
        AssertSchema(root, schemaVersion);
        AssertWorkerProfile(root, workerProfile);
        AssertMarker(root, requireSyntheticMarker);
        var conceptId = Required(root["selectedConceptId"]?.GetValue<string>(), "selectedConceptId", 32);
        if (!string.Equals(conceptId, selectedConceptId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stage selectedConceptId must equal pinned '{selectedConceptId}'.");
        }

        return root;
    }

    private static CanonicalCreativeStageOutput BuildStageOutput(
        JsonObject root,
        string schemaVersion,
        string workerProfile,
        string selectedConceptId,
        bool requireSyntheticMarker,
        IReadOnlyList<CanonicalCreativeContribution> contributions,
        Func<CanonicalCreativeContribution, JsonObject> toNode)
    {
        var ordered = new JsonObject
        {
            ["schemaVersion"] = schemaVersion,
            ["workerProfileVersion"] = workerProfile,
            ["selectedConceptId"] = selectedConceptId,
            ["contributions"] = new JsonArray(contributions.Select(c => (JsonNode)toNode(c)).ToArray())
        };
        if (requireSyntheticMarker || root["marker"] is not null)
        {
            ordered["marker"] = WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage;
        }

        return new CanonicalCreativeStageOutput(
            ordered.ToJsonString(NodeWriteOptions),
            workerProfile,
            selectedConceptId,
            contributions);
    }

    private static List<CanonicalCreativeImagePrompt> ParseImagePrompts(
        JsonNode? node,
        IReadOnlyList<string> expectedIds)
    {
        if (node is not JsonArray arr || arr.Count != expectedIds.Count)
        {
            throw new InvalidOperationException(
                $"imagePrompts must contain exactly {expectedIds.Count} items.");
        }

        var prompts = new List<CanonicalCreativeImagePrompt>(expectedIds.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException("Each imagePrompt must be an object.");
            }

            AssertOnlyKnownProperties(obj, "variantId", "prompt", "negativeConstraints");
            var variantId = Required(obj["variantId"]?.GetValue<string>(), "variantId", MaxVariantIdLength);
            if (!seen.Add(variantId))
            {
                throw new InvalidOperationException($"Duplicate imagePrompt variantId '{variantId}'.");
            }

            prompts.Add(new CanonicalCreativeImagePrompt(
                variantId,
                Required(obj["prompt"]?.GetValue<string>(), "prompt", MaxImagePromptLength),
                ParseStringList(obj["negativeConstraints"], "negativeConstraints", MaxNegativeConstraints, MaxNegativeConstraintLength)));
        }

        AssertExactVariantIds(expectedIds, seen);
        return expectedIds.Select(id => prompts.Single(p => p.VariantId == id)).ToList();
    }

    private static List<CanonicalCreativeVariantText> ParseVariantTexts(
        JsonNode? node,
        IReadOnlyList<string> expectedIds,
        string label,
        bool allowClaims,
        IReadOnlyList<CanonicalCreativeFactualClaim> preservedClaims,
        IReadOnlySet<string> forbiddenSourceIds)
    {
        if (node is not JsonArray arr || arr.Count != expectedIds.Count)
        {
            throw new InvalidOperationException($"{label} must contain exactly {expectedIds.Count} items.");
        }

        var items = new List<CanonicalCreativeVariantText>(expectedIds.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException($"Each {label} item must be an object.");
            }

            if (allowClaims)
            {
                AssertOnlyKnownProperties(obj, "variantId", "text", "factualClaims");
            }
            else
            {
                AssertOnlyKnownProperties(obj, "variantId", "text");
            }

            var variantId = Required(obj["variantId"]?.GetValue<string>(), "variantId", MaxVariantIdLength);
            if (!seen.Add(variantId))
            {
                throw new InvalidOperationException($"Duplicate {label} variantId '{variantId}'.");
            }

            var text = Required(obj["text"]?.GetValue<string>(), "text", MaxCopyFieldLength);
            var claims = new List<CanonicalCreativeFactualClaim>();
            if (allowClaims && obj["factualClaims"] is JsonArray claimsArr)
            {
                if (claimsArr.Count > MaxFactualClaimsPerVariant)
                {
                    throw new InvalidOperationException(
                        $"factualClaims cannot exceed {MaxFactualClaimsPerVariant} items.");
                }

                foreach (var claimNode in claimsArr)
                {
                    claims.Add(ParsePreservedClaim(claimNode, preservedClaims, forbiddenSourceIds));
                }
            }
            else if (!allowClaims && obj["factualClaims"] is not null)
            {
                throw new InvalidOperationException($"{label} must not include factualClaims.");
            }

            items.Add(new CanonicalCreativeVariantText(variantId, text, claims));
        }

        AssertExactVariantIds(expectedIds, seen);
        return expectedIds.Select(id => items.Single(i => i.VariantId == id)).ToList();
    }

    private static List<CanonicalCreativeVariantSpec> ParseVariants(
        JsonNode? node,
        IReadOnlyList<string> expectedIds,
        IReadOnlyList<string> briefFormats,
        IReadOnlySet<string> knownPaletteRoles,
        IReadOnlySet<string> imagePromptIds,
        IReadOnlySet<string> headlineIds,
        IReadOnlySet<string> bodyIds,
        IReadOnlySet<string> ctaIds)
    {
        if (node is not JsonArray arr || arr.Count != expectedIds.Count)
        {
            throw new InvalidOperationException($"variants must contain exactly {expectedIds.Count} items.");
        }

        var briefFormatSet = new HashSet<string>(briefFormats, StringComparer.Ordinal);
        var variants = new List<CanonicalCreativeVariantSpec>(expectedIds.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var usedFormats = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in arr)
        {
            if (item is not JsonObject obj)
            {
                throw new InvalidOperationException("Each variant must be an object.");
            }

            AssertOnlyKnownProperties(obj, "id", "format", "canvas", "refs", "paletteRoleRefs");
            var id = Required(obj["id"]?.GetValue<string>(), "variant.id", MaxVariantIdLength);
            if (!seen.Add(id))
            {
                throw new InvalidOperationException($"Duplicate variant id '{id}'.");
            }

            var format = Required(obj["format"]?.GetValue<string>(), "variant.format", 64);
            if (!briefFormatSet.Contains(format))
            {
                throw new InvalidOperationException($"Variant format '{format}' is not in the brief.");
            }

            usedFormats.Add(format);
            var (width, height) = CanvasForFormat(format);
            if (obj["canvas"] is not JsonObject canvas)
            {
                throw new InvalidOperationException("variant.canvas is required.");
            }

            AssertOnlyKnownProperties(canvas, "width", "height");
            var cw = canvas["width"]?.GetValue<int?>()
                ?? throw new InvalidOperationException("variant.canvas.width is required.");
            var ch = canvas["height"]?.GetValue<int?>()
                ?? throw new InvalidOperationException("variant.canvas.height is required.");
            if (cw != width || ch != height)
            {
                throw new InvalidOperationException(
                    $"Variant '{id}' canvas must be exactly {width}x{height} for format {format}.");
            }

            if (obj["refs"] is not JsonObject refs)
            {
                throw new InvalidOperationException("variant.refs is required.");
            }

            AssertOnlyKnownProperties(
                refs,
                "direction", "visualSystem", "layout", "typography", "imagePrompt", "headline", "body", "cta");

            var refsCanonical = new CanonicalCreativeVariantRefs(
                Required(refs["direction"]?.GetValue<string>(), "refs.direction", 128),
                Required(refs["visualSystem"]?.GetValue<string>(), "refs.visualSystem", 128),
                Required(refs["layout"]?.GetValue<string>(), "refs.layout", 128),
                Required(refs["typography"]?.GetValue<string>(), "refs.typography", 128),
                Required(refs["imagePrompt"]?.GetValue<string>(), "refs.imagePrompt", 128),
                Required(refs["headline"]?.GetValue<string>(), "refs.headline", 128),
                Required(refs["body"]?.GetValue<string>(), "refs.body", 128),
                Required(refs["cta"]?.GetValue<string>(), "refs.cta", 128));

            AssertRef(refsCanonical.Direction, "contributions.CREATIVE_DIRECTOR");
            AssertRef(refsCanonical.VisualSystem, "contributions.VISUAL_DESIGNER");
            AssertRef(refsCanonical.Layout, "contributions.LAYOUT_DESIGNER");
            AssertRef(refsCanonical.Typography, "contributions.TYPOGRAPHY_DESIGNER");
            AssertRef(refsCanonical.ImagePrompt, $"imagePrompts.{id}");
            AssertRef(refsCanonical.Headline, $"headlines.{id}");
            AssertRef(refsCanonical.Body, $"bodies.{id}");
            AssertRef(refsCanonical.Cta, $"ctas.{id}");

            if (!imagePromptIds.Contains(id) || !headlineIds.Contains(id) || !bodyIds.Contains(id) || !ctaIds.Contains(id))
            {
                throw new InvalidOperationException($"Variant '{id}' has dangling prior-stage refs.");
            }

            var paletteRefs = ParsePaletteRoleRefs(obj["paletteRoleRefs"], knownPaletteRoles);
            variants.Add(new CanonicalCreativeVariantSpec(id, format, width, height, refsCanonical, paletteRefs));
        }

        AssertExactVariantIds(expectedIds, seen);
        foreach (var format in briefFormats)
        {
            if (!usedFormats.Contains(format))
            {
                throw new InvalidOperationException($"Each brief format must be used at least once; missing '{format}'.");
            }
        }

        return expectedIds.Select(id => variants.Single(v => v.Id == id)).ToList();
    }

    private static void AssertRef(string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Variant ref must be '{expected}' (got '{actual}').");
        }
    }

    private static CanonicalCreativeFactualClaim ParsePreservedClaim(
        JsonNode? node,
        IReadOnlyList<CanonicalCreativeFactualClaim> preservedClaims,
        IReadOnlySet<string> forbiddenSourceIds)
    {
        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException("Each factual claim must be an object.");
        }

        AssertOnlyKnownProperties(obj, "statement", "sourceIds");
        var statement = Required(obj["statement"]?.GetValue<string>(), "statement", MaxFactualClaimStatementLength);
        var sourceIds = ParseStringList(obj["sourceIds"], "sourceIds", MaxSourceIdsPerClaim, MaxSourceIdLength);
        foreach (var sourceId in sourceIds)
        {
            if (forbiddenSourceIds.Contains(sourceId) || ForbiddenProvenanceSourceIdKeys.Contains(sourceId))
            {
                throw new InvalidOperationException(
                    $"factualClaims sourceId '{sourceId}' cannot be a Brand DNA/color/provenance id.");
            }
        }

        var match = preservedClaims.FirstOrDefault(c =>
            string.Equals(c.Statement, statement, StringComparison.Ordinal)
            && c.SourceIds.Count == sourceIds.Count
            && c.SourceIds.OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(sourceIds.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal));

        if (match is null)
        {
            throw new InvalidOperationException(
                "factualClaims may only preserve exact statement + sourceIds from the pinned selected Phase 5 concept.");
        }

        return new CanonicalCreativeFactualClaim(statement, sourceIds);
    }

    private static List<string> ParsePaletteRoleRefs(JsonNode? node, IReadOnlySet<string> knownPaletteRoles)
    {
        var roles = ParseStringList(node, "paletteRoleRefs", MaxPaletteRoleRefs, 64);
        if (roles.Count == 0)
        {
            throw new InvalidOperationException("paletteRoleRefs must contain at least one role.");
        }

        foreach (var role in roles)
        {
            if (!knownPaletteRoles.Contains(role))
            {
                throw new InvalidOperationException($"Unknown paletteRoleRef '{role}'.");
            }
        }

        return roles;
    }

    private static List<string> ParseStringList(JsonNode? node, string label, int maxItems, int maxItemLength)
    {
        if (node is null)
        {
            return [];
        }

        if (node is not JsonArray arr)
        {
            throw new InvalidOperationException($"{label} must be an array.");
        }

        if (arr.Count > maxItems)
        {
            throw new InvalidOperationException($"{label} cannot exceed {maxItems} items.");
        }

        var items = new List<string>(arr.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            var value = Required(item?.GetValue<string>(), label + " item", maxItemLength);
            if (!seen.Add(value))
            {
                throw new InvalidOperationException($"Duplicate {label} item '{value}'.");
            }

            items.Add(value);
        }

        return items;
    }

    private static void AssertExactVariantIds(IReadOnlyList<string> expected, IReadOnlySet<string> actual)
    {
        if (actual.Count != expected.Count || expected.Any(id => !actual.Contains(id)))
        {
            throw new InvalidOperationException(
                $"Variant ids must be exactly {string.Join(", ", expected)}.");
        }
    }

    private static JsonArray ToVariantTextArray(IReadOnlyList<CanonicalCreativeVariantText> items, bool includeClaims)
    {
        return new JsonArray(items.Select(i =>
        {
            var obj = new JsonObject
            {
                ["variantId"] = i.VariantId,
                ["text"] = i.Text
            };
            if (includeClaims)
            {
                obj["factualClaims"] = new JsonArray(i.FactualClaims.Select(ToClaimNode).ToArray());
            }

            return (JsonNode)obj;
        }).ToArray());
    }

    private static JsonObject ToClaimNode(CanonicalCreativeFactualClaim claim) =>
        new()
        {
            ["statement"] = claim.Statement,
            ["sourceIds"] = ToJsonArray(claim.SourceIds)
        };

    private static void AssertNoEmbeddedAssetBytesOrUrls(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (property.Key is "bytes" or "pngBytes" or "imageBytes" or "dataUrl" or "base64"
                    || property.Key.Contains("url", StringComparison.OrdinalIgnoreCase))
                {
                    if (property.Key is not ("creativeAssetId"))
                    {
                        // allow only id refs under asset objects — reject any url-like keys
                        if (property.Key.Contains("url", StringComparison.OrdinalIgnoreCase)
                            || property.Key is "bytes" or "pngBytes" or "imageBytes" or "dataUrl" or "base64")
                        {
                            throw new InvalidOperationException(
                                $"Creative package must not embed '{property.Key}'; asset ids only.");
                        }
                    }
                }

                AssertNoEmbeddedAssetBytesOrUrls(property.Value!);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                {
                    AssertNoEmbeddedAssetBytesOrUrls(item);
                }
            }
        }
    }

    private static List<JsonObject> ParseContributionsArray(JsonObject root)
    {
        if (root["contributions"] is not JsonArray arr)
        {
            throw new InvalidOperationException("contributions must be an array.");
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

    private static JsonObject ParseObject(string? json, string label)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj)
            {
                throw new InvalidOperationException($"{label} must be a JSON object.");
            }

            return obj;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{label} is invalid JSON.", ex);
        }
    }

    private static void AssertSchema(JsonObject root, string expected)
    {
        var actual = Required(root["schemaVersion"]?.GetValue<string>(), "schemaVersion", 64);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"schemaVersion must be {expected}.");
        }
    }

    private static void AssertWorkerProfile(JsonObject root, string expected)
    {
        var actual = Required(root["workerProfileVersion"]?.GetValue<string>(), "workerProfileVersion", 64);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
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

        var marker = root["marker"]?.GetValue<string>();
        if (!string.Equals(
                marker,
                WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Local creative stage outputs must include marker SYNTHETIC DEVELOPMENT CREATIVE PACKAGE.");
        }
    }

    private static void AssertOnlyKnownProperties(JsonObject obj, params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in obj)
        {
            if (!allowedSet.Contains(property.Key))
            {
                throw new InvalidOperationException($"Unknown field '{property.Key}' is forbidden.");
            }
        }
    }

    private static void RejectForbiddenKeysEverywhere(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (ForbiddenKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key))
                {
                    throw new InvalidOperationException($"Forbidden field '{property.Key}' is not allowed.");
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
        var k = key.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return k.Contains("campaignready", StringComparison.Ordinal)
            || k.Contains("qaapproved", StringComparison.Ordinal)
            || k.Contains("blissready", StringComparison.Ordinal)
            || k.Contains("legalcleared", StringComparison.Ordinal)
            || k.Contains("matchid", StringComparison.Ordinal)
            || k.Contains("placementid", StringComparison.Ordinal)
            || k.Contains("inventoryid", StringComparison.Ordinal)
            || k.Contains("imagebytes", StringComparison.Ordinal)
            || k.Contains("base64", StringComparison.Ordinal)
            || (k.Contains("selectedconcept", StringComparison.Ordinal) && k.Contains("override", StringComparison.Ordinal));
    }

    private static JsonObject OrderObjectKeys(JsonObject obj)
    {
        var ordered = new JsonObject();
        foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            ordered[property.Key] = property.Value is JsonObject child
                ? OrderObjectKeys(child)
                : property.Value?.DeepClone();
        }

        return ordered;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values) =>
        new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());

    private static string Required(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} exceeds max length {maxLength}.");
        }

        return trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record CanonicalCreativeProductionBrief(
    string JobKind,
    string Objective,
    IReadOnlyList<string> Formats,
    int RequestedVariantCount,
    Guid? RevisionParentCreativePackageVersionId,
    string? RevisionNotes,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string InputJson,
    string InputSha256);

public sealed record SelectedConceptSnapshot(
    string Id,
    string Name,
    string Rationale,
    string VisualDirection,
    IReadOnlyList<string> PaletteRoleRefs,
    CanonicalCreativeCopy Copy,
    IReadOnlyList<CanonicalCreativeFactualClaim> FactualClaims);

public sealed record CanonicalCreativeCopy(string Kind, string Headline, string Body, string Cta);

public sealed record CanonicalCreativeFactualClaim(string Statement, IReadOnlyList<string> SourceIds);

public sealed record CanonicalCreativeDirection(string NorthStar, IReadOnlyList<string> Principles);

public sealed record CanonicalCampaignFraming(string ObjectiveEcho, string FormatPlanNotes);

public sealed record CanonicalVisualSystem(IReadOnlyList<string> PaletteRoleRefs, string Atmosphere);

public sealed record CanonicalTypographySystem(string HeadlineRole, string BodyRole, string CtaRole);

public sealed record CanonicalCreativeImagePrompt(
    string VariantId,
    string Prompt,
    IReadOnlyList<string> NegativeConstraints);

public sealed record CanonicalCreativeVariantText(
    string VariantId,
    string Text,
    IReadOnlyList<CanonicalCreativeFactualClaim> FactualClaims);

public sealed record CanonicalCreativeVariantRefs(
    string Direction,
    string VisualSystem,
    string Layout,
    string Typography,
    string ImagePrompt,
    string Headline,
    string Body,
    string Cta);

public sealed record CanonicalCreativeVariantSpec(
    string Id,
    string Format,
    int Width,
    int Height,
    CanonicalCreativeVariantRefs Refs,
    IReadOnlyList<string> PaletteRoleRefs);

public sealed record CanonicalCreativeContribution(
    string LogicalRole,
    string Summary,
    CanonicalCreativeDirection? Direction = null,
    CanonicalCampaignFraming? Framing = null,
    string? AudienceNotes = null,
    string? OfferNotes = null,
    IReadOnlyList<string>? ChannelFormats = null,
    CanonicalVisualSystem? VisualSystem = null,
    string? LayoutGeometry = null,
    CanonicalTypographySystem? Typography = null,
    IReadOnlyList<CanonicalCreativeImagePrompt>? ImagePrompts = null,
    IReadOnlyList<CanonicalCreativeVariantText>? Headlines = null,
    IReadOnlyList<CanonicalCreativeVariantText>? Bodies = null,
    IReadOnlyList<CanonicalCreativeVariantText>? Ctas = null,
    IReadOnlyList<CanonicalCreativeVariantSpec>? Variants = null);

public sealed record CanonicalCreativeStageOutput(
    string CanonicalJson,
    string WorkerProfileVersion,
    string SelectedConceptId,
    IReadOnlyList<CanonicalCreativeContribution> Contributions);

public sealed record CanonicalCreativeContributionSummary(string LogicalRole, string Summary);

public sealed record CanonicalCreativeAssetRef(
    string VariantId,
    Guid CreativeAssetId,
    string ContentType,
    int ByteSize,
    string Sha256,
    int Width,
    int Height);

public sealed record CanonicalCreativePlannedVariant(
    string Id,
    string Format,
    int Width,
    int Height,
    IReadOnlyList<string> PaletteRoleRefs,
    CanonicalCreativeCopy Copy,
    IReadOnlyList<CanonicalCreativeFactualClaim> FactualClaims,
    string ImagePrompt,
    CanonicalCreativeAssetRef? Asset);

public sealed record CanonicalCreativePackagePlan(
    CanonicalCreativeProductionBrief Brief,
    SelectedConceptSnapshot SelectedConcept,
    IReadOnlyList<CanonicalCreativePlannedVariant> Variants,
    IReadOnlyList<CanonicalCreativeContributionSummary> ContributionSummaries,
    IReadOnlyList<CanonicalCreativeContribution> RoleContributions,
    Guid CreativeProductionJobId,
    bool RequireSyntheticMarker);

public sealed record CanonicalCreativePackage(
    string DocumentJson,
    string Summary,
    IReadOnlyList<CanonicalCreativePlannedVariant> Variants,
    IReadOnlyList<CanonicalCreativeContributionSummary> ContributionSummaries,
    IReadOnlyList<CanonicalCreativeContribution> RoleContributions);
