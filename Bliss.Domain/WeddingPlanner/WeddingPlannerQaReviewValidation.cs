using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Strict validators/canonicalizers for QA review briefs, two stage outputs,
/// Steward RULES_HUMAN contribution, and qa-review-report.v1 merge.
/// AI never receives image bytes. Steward is never an AI worker/run.
/// </summary>
public static class WeddingPlannerQaReviewValidation
{
    public const int MaxReviewObjectiveLength = 4000;
    public const int MaxNotesLength = 4000;
    public const int MinFocusAreas = 1;
    public const int MaxFocusAreas = 6;
    public const int MaxContributionSummaryLength = 2000;
    public const int MaxRiskNoteLength = 1000;
    public const int MaxRiskNotesPerBucket = 12;
    public const int MaxUncertainties = 12;
    public const int MaxUncertaintyLength = 1000;
    public const int MaxAlignmentNoteLength = 1000;
    public const int MaxAlignmentNotesPerBucket = 12;
    public const int HeadlineWarnThreshold = 120;
    public const int BodyWarnThreshold = 800;
    public const int CtaWarnThreshold = 40;

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

    private static readonly HashSet<string> FocusAreaSet = new(WeddingPlannerQaFocusAreas.All, StringComparer.Ordinal);
    private static readonly HashSet<string> ProposedOutcomeSet = new(WeddingPlannerQaProposedOutcomes.All, StringComparer.Ordinal);
    private static readonly HashSet<string> EscalationCategorySet = new(WeddingPlannerQaEscalationCategories.All, StringComparer.Ordinal);
    private static readonly HashSet<string> DecisionSet = new(WeddingPlannerQaReviewDecisions.All, StringComparer.Ordinal);
    private static readonly HashSet<string> ResolutionSet = new(WeddingPlannerQaEscalationResolutions.All, StringComparer.Ordinal);

    private static readonly HashSet<string> ForbiddenBriefKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "css", "svg", "script", "src", "url", "href", "base64",
        "imageBytes", "image_bytes", "imageData", "image_data", "pixels", "pixelArray",
        "matchId", "matchingId", "blissMatchId", "matchIds", "matchingIds",
        "campaignId", "campaignIds",
        "placementId", "placementIds",
        "inventoryId", "inventoryIds", "adInventorySlotId",
        "campaignReady", "qaApproved", "blissReady", "legalCleared", "qaReady", "readiness",
        "aiProvider", "aiEndpoint", "aiApiKey", "providerToolConfiguration",
        "creativePackageVersionId", "selectedVariantId", "creativeAssetId",
        "approvedCreativePackageVersionId", "selectedCreativeAssetId",
        "overrideSelectedVariantId", "packageOverride"
    };

    private static readonly HashSet<string> ForbiddenOutputKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "css", "svg", "script", "src", "url", "href", "base64",
        "imageBytes", "image_bytes", "imageData", "image_data", "pixels", "pixelArray",
        "matchId", "matchingId", "blissMatchId", "matchIds", "matchingIds",
        "campaignId", "campaignIds",
        "placementId", "placementIds",
        "inventoryId", "inventoryIds", "adInventorySlotId",
        "campaignReady", "qaApproved", "blissReady", "legalCleared", "qaReady", "readiness",
        "aiProvider", "aiEndpoint", "aiApiKey", "providerToolConfiguration"
    };

    public static void RejectForbiddenBriefFields(JsonNode? node, string path = "$")
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (ForbiddenBriefKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key))
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

    public static void RejectForbiddenDecisionFields(JsonNode? node, string path = "$")
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (ForbiddenOutputKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key))
                {
                    throw new InvalidOperationException(
                        $"Forbidden decision field '{property.Key}' is not allowed.");
                }

                RejectForbiddenDecisionFields(property.Value, path + "." + property.Key);
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                RejectForbiddenDecisionFields(arr[i], path + $"[{i}]");
            }
        }
    }

    public static CanonicalQaReviewBrief CanonicalizeBrief(
        string reviewObjective,
        IReadOnlyList<string> focusAreas,
        string? notes,
        Guid approvedCreativePackageVersionId,
        string creativePackageDocumentSha256,
        Guid creativePackageDecisionId,
        string selectedVariantId,
        Guid selectedCreativeAssetId,
        string selectedCreativeAssetSha256,
        string selectedConceptId,
        Guid approvedBrandDnaVersionId,
        int approvedBrandDnaVersionNumber,
        Guid approvedColorProfileVersionId,
        int approvedColorProfileVersionNumber,
        Guid approvedResearchReportVersionId,
        int approvedResearchReportVersionNumber,
        string aiProviderKind,
        string aiWorkerKey)
    {
        var objective = Required(reviewObjective, nameof(reviewObjective), MaxReviewObjectiveLength);
        if (focusAreas is null || focusAreas.Count is < MinFocusAreas or > MaxFocusAreas)
        {
            throw new InvalidOperationException(
                $"focusAreas must contain {MinFocusAreas}–{MaxFocusAreas} unique locked values.");
        }

        var normalizedFocus = new List<string>(focusAreas.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var area in focusAreas)
        {
            var trimmed = Required(area, "focusAreas[]", 64);
            if (!FocusAreaSet.Contains(trimmed))
            {
                throw new InvalidOperationException($"Unknown focusArea '{trimmed}'.");
            }

            if (!seen.Add(trimmed))
            {
                throw new InvalidOperationException("focusAreas must be unique.");
            }

            normalizedFocus.Add(trimmed);
        }

        string? normalizedNotes = null;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            normalizedNotes = Required(notes, nameof(notes), MaxNotesLength);
        }

        var packageSha = Required(creativePackageDocumentSha256, nameof(creativePackageDocumentSha256), 64).ToLowerInvariant();
        var assetSha = Required(selectedCreativeAssetSha256, nameof(selectedCreativeAssetSha256), 64).ToLowerInvariant();
        var variantId = Required(selectedVariantId, nameof(selectedVariantId), 32);
        var conceptId = Required(selectedConceptId, nameof(selectedConceptId), 32);

        var canonical = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.QaReviewBriefV1,
            ["reviewObjective"] = objective,
            ["focusAreas"] = ToJsonArray(normalizedFocus.OrderBy(x => x, StringComparer.Ordinal)),
            ["notes"] = normalizedNotes,
            ["approvedCreativePackageVersionId"] = approvedCreativePackageVersionId.ToString("D"),
            ["creativePackageDocumentSha256"] = packageSha,
            ["creativePackageDecisionId"] = creativePackageDecisionId.ToString("D"),
            ["selectedVariantId"] = variantId,
            ["selectedCreativeAssetId"] = selectedCreativeAssetId.ToString("D"),
            ["selectedCreativeAssetSha256"] = assetSha,
            ["selectedConceptId"] = conceptId,
            ["approvedBrandDnaVersionId"] = approvedBrandDnaVersionId.ToString("D"),
            ["approvedBrandDnaVersionNumber"] = approvedBrandDnaVersionNumber,
            ["approvedColorProfileVersionId"] = approvedColorProfileVersionId.ToString("D"),
            ["approvedColorProfileVersionNumber"] = approvedColorProfileVersionNumber,
            ["approvedResearchReportVersionId"] = approvedResearchReportVersionId.ToString("D"),
            ["approvedResearchReportVersionNumber"] = approvedResearchReportVersionNumber,
            ["qaRulesContractVersion"] = WeddingPlannerQaContractVersions.QaRulesV1,
            ["qaOrchestrationContractVersion"] = WeddingPlannerQaContractVersions.QaOrchestrationV1,
            ["aiProviderKind"] = Required(aiProviderKind, nameof(aiProviderKind), 64),
            ["aiWorkerKey"] = Required(aiWorkerKey, nameof(aiWorkerKey), 64)
        };

        var ordered = OrderObjectKeys(canonical);
        var inputJson = ordered.ToJsonString(NodeWriteOptions);
        var sha = Sha256Hex(inputJson);
        return new CanonicalQaReviewBrief(
            objective,
            normalizedFocus,
            normalizedNotes,
            approvedCreativePackageVersionId,
            packageSha,
            creativePackageDecisionId,
            variantId,
            selectedCreativeAssetId,
            assetSha,
            conceptId,
            approvedBrandDnaVersionId,
            approvedBrandDnaVersionNumber,
            approvedColorProfileVersionId,
            approvedColorProfileVersionNumber,
            approvedResearchReportVersionId,
            approvedResearchReportVersionNumber,
            inputJson,
            sha);
    }

    public static CanonicalQaChaperoneStageOutput CanonicalizeChaperoneOutput(
        string? stageJson,
        string selectedVariantId,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Chaperone review stage output",
            WeddingPlannerSchemaVersions.ChaperoneReviewWorkerOutputV1,
            WeddingPlannerQaWorkerProfiles.ChaperoneReviewV1,
            selectedVariantId,
            requireSyntheticMarker);

        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "marker", "selectedVariantId", "contributions");
        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 1)
        {
            throw new InvalidOperationException("Chaperone stage must contain exactly one contribution.");
        }

        var contribution = contributions[0];
        AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "riskNotes", "uncertainties");
        var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!string.Equals(role, WeddingPlannerQaLogicalRoles.CreativeChaperone, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Chaperone stage contribution must be CREATIVE_CHAPERONE.");
        }

        var summary = Required(contribution["summary"]?.GetValue<string>(), "summary", MaxContributionSummaryLength);
        RejectApprovalOrPixelClaims(summary);
        if (contribution["riskNotes"] is not JsonObject riskNotes)
        {
            throw new InvalidOperationException("CREATIVE_CHAPERONE riskNotes is required.");
        }

        AssertOnlyKnownProperties(riskNotes, "boundary", "provenance", "brand", "claims");
        var boundary = ParseStringList(riskNotes["boundary"], "riskNotes.boundary", MaxRiskNotesPerBucket, MaxRiskNoteLength);
        var provenance = ParseStringList(riskNotes["provenance"], "riskNotes.provenance", MaxRiskNotesPerBucket, MaxRiskNoteLength);
        var brand = ParseStringList(riskNotes["brand"], "riskNotes.brand", MaxRiskNotesPerBucket, MaxRiskNoteLength);
        var claims = ParseStringList(riskNotes["claims"], "riskNotes.claims", MaxRiskNotesPerBucket, MaxRiskNoteLength);
        foreach (var note in boundary.Concat(provenance).Concat(brand).Concat(claims))
        {
            RejectApprovalOrPixelClaims(note);
        }

        var uncertainties = ParseStringList(contribution["uncertainties"], "uncertainties", MaxUncertainties, MaxUncertaintyLength);
        foreach (var item in uncertainties)
        {
            RejectApprovalOrPixelClaims(item);
        }

        RejectForbiddenKeysEverywhere(root);
        var canonicalContribution = new CanonicalQaChaperoneContribution(
            role,
            summary,
            new CanonicalQaRiskNotes(boundary, provenance, brand, claims),
            uncertainties);
        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.ChaperoneReviewWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerQaWorkerProfiles.ChaperoneReviewV1,
            ["selectedVariantId"] = selectedVariantId,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = canonicalContribution.LogicalRole,
                ["summary"] = canonicalContribution.Summary,
                ["riskNotes"] = new JsonObject
                {
                    ["boundary"] = ToJsonArray(canonicalContribution.RiskNotes.Boundary),
                    ["provenance"] = ToJsonArray(canonicalContribution.RiskNotes.Provenance),
                    ["brand"] = ToJsonArray(canonicalContribution.RiskNotes.Brand),
                    ["claims"] = ToJsonArray(canonicalContribution.RiskNotes.Claims)
                },
                ["uncertainties"] = ToJsonArray(canonicalContribution.Uncertainties)
            })
        };
        if (requireSyntheticMarker)
        {
            document["marker"] = WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview;
        }

        var ordered = OrderObjectKeys(document);
        return new CanonicalQaChaperoneStageOutput(
            ordered.ToJsonString(NodeWriteOptions),
            WeddingPlannerQaWorkerProfiles.ChaperoneReviewV1,
            selectedVariantId,
            canonicalContribution);
    }

    public static CanonicalQaInspectionStageOutput CanonicalizeQaInspectionOutput(
        string? stageJson,
        string selectedVariantId,
        string rulesOverallSeverity,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "QA inspection stage output",
            WeddingPlannerSchemaVersions.QaInspectionWorkerOutputV1,
            WeddingPlannerQaWorkerProfiles.QaInspectionV1,
            selectedVariantId,
            requireSyntheticMarker);

        AssertOnlyKnownProperties(
            root,
            "schemaVersion", "workerProfileVersion", "marker", "selectedVariantId", "rulesOverallSeverity", "contributions");
        var echoedSeverity = Required(root["rulesOverallSeverity"]?.GetValue<string>(), "rulesOverallSeverity", 16);
        if (!string.Equals(echoedSeverity, rulesOverallSeverity, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("QA inspection rulesOverallSeverity must echo durable rules overall severity.");
        }

        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 1)
        {
            throw new InvalidOperationException("QA inspection stage must contain exactly one contribution.");
        }

        var contribution = contributions[0];
        AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "alignmentNotes", "uncertainties", "proposedOutcome");
        var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!string.Equals(role, WeddingPlannerQaLogicalRoles.QaInspector, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("QA inspection contribution must be QA_INSPECTOR.");
        }

        var summary = Required(contribution["summary"]?.GetValue<string>(), "summary", MaxContributionSummaryLength);
        if (contribution["alignmentNotes"] is not JsonObject alignmentNotes)
        {
            throw new InvalidOperationException("QA_INSPECTOR alignmentNotes is required.");
        }

        AssertOnlyKnownProperties(alignmentNotes, "format", "copy", "assetMetadata");
        var format = ParseStringList(alignmentNotes["format"], "alignmentNotes.format", MaxAlignmentNotesPerBucket, MaxAlignmentNoteLength);
        var copy = ParseStringList(alignmentNotes["copy"], "alignmentNotes.copy", MaxAlignmentNotesPerBucket, MaxAlignmentNoteLength);
        var assetMetadata = ParseStringList(alignmentNotes["assetMetadata"], "alignmentNotes.assetMetadata", MaxAlignmentNotesPerBucket, MaxAlignmentNoteLength);
        var uncertainties = ParseStringList(contribution["uncertainties"], "uncertainties", MaxUncertainties, MaxUncertaintyLength);
        var proposedOutcome = Required(contribution["proposedOutcome"]?.GetValue<string>(), "proposedOutcome", 64);
        if (!ProposedOutcomeSet.Contains(proposedOutcome))
        {
            throw new InvalidOperationException("proposedOutcome must be PASS_RECOMMENDED, RETURN_FOR_REVISION, or HUMAN_ESCALATION.");
        }

        if (string.Equals(rulesOverallSeverity, WeddingPlannerQaFindingSeverities.Block, StringComparison.Ordinal)
            && string.Equals(proposedOutcome, WeddingPlannerQaProposedOutcomes.PassRecommended, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("proposedOutcome cannot be PASS_RECOMMENDED when rules overallSeverity is BLOCK.");
        }

        RejectForbiddenKeysEverywhere(root);
        var canonicalContribution = new CanonicalQaInspectionContribution(
            role,
            summary,
            new CanonicalQaAlignmentNotes(format, copy, assetMetadata),
            uncertainties,
            proposedOutcome);
        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.QaInspectionWorkerOutputV1,
            ["workerProfileVersion"] = WeddingPlannerQaWorkerProfiles.QaInspectionV1,
            ["selectedVariantId"] = selectedVariantId,
            ["rulesOverallSeverity"] = rulesOverallSeverity,
            ["contributions"] = new JsonArray(new JsonObject
            {
                ["logicalRole"] = canonicalContribution.LogicalRole,
                ["summary"] = canonicalContribution.Summary,
                ["alignmentNotes"] = new JsonObject
                {
                    ["format"] = ToJsonArray(canonicalContribution.AlignmentNotes.Format),
                    ["copy"] = ToJsonArray(canonicalContribution.AlignmentNotes.Copy),
                    ["assetMetadata"] = ToJsonArray(canonicalContribution.AlignmentNotes.AssetMetadata)
                },
                ["uncertainties"] = ToJsonArray(canonicalContribution.Uncertainties),
                ["proposedOutcome"] = canonicalContribution.ProposedOutcome
            })
        };
        if (requireSyntheticMarker)
        {
            document["marker"] = WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview;
        }

        var ordered = OrderObjectKeys(document);
        return new CanonicalQaInspectionStageOutput(
            ordered.ToJsonString(NodeWriteOptions),
            WeddingPlannerQaWorkerProfiles.QaInspectionV1,
            selectedVariantId,
            rulesOverallSeverity,
            canonicalContribution);
    }

    public static CanonicalQaStewardContribution BuildStewardContribution(
        string rulesOverallSeverity,
        IReadOnlyList<string> blockerCodes,
        IReadOnlyList<string> warnCodes,
        string? qaProposedOutcome)
    {
        var routing = DeriveProposedRouting(rulesOverallSeverity, qaProposedOutcome);
        var summary = "Rules-first routing for human authority.";
        var payload = new JsonObject
        {
            ["logicalRole"] = WeddingPlannerQaLogicalRoles.HumanEscalationSteward,
            ["contributionSource"] = WeddingPlannerQaContributionSources.RulesHuman,
            ["producingAgentRunId"] = null,
            ["summary"] = summary,
            ["routing"] = new JsonObject
            {
                ["rulesOverallSeverity"] = rulesOverallSeverity,
                ["blockerCodes"] = ToJsonArray(blockerCodes),
                ["warnCodes"] = ToJsonArray(warnCodes),
                ["proposedRouting"] = routing,
                ["requiredHumanAuthority"] = ToJsonArray(WeddingPlannerQaRequiredHumanAuthority.All)
            }
        };

        return new CanonicalQaStewardContribution(
            WeddingPlannerQaLogicalRoles.HumanEscalationSteward,
            WeddingPlannerQaContributionSources.RulesHuman,
            summary,
            rulesOverallSeverity,
            blockerCodes.ToArray(),
            warnCodes.ToArray(),
            routing,
            OrderObjectKeys(payload).ToJsonString(NodeWriteOptions));
    }

    public static CanonicalQaReviewReport MergeReport(
        CanonicalQaReviewBrief brief,
        CanonicalQaRulesFindings rules,
        CanonicalQaChaperoneStageOutput chaperone,
        CanonicalQaInspectionStageOutput inspection,
        CanonicalQaStewardContribution steward,
        Guid chaperoneRunId,
        Guid qaInspectionRunId,
        Guid qaReviewJobId,
        SelectedVariantQaSnapshot selectedVariant,
        QaAssetMetaSnapshot assetMeta,
        bool requireSyntheticMarker)
    {
        if (!string.Equals(chaperone.SelectedVariantId, brief.SelectedVariantId, StringComparison.Ordinal)
            || !string.Equals(inspection.SelectedVariantId, brief.SelectedVariantId, StringComparison.Ordinal)
            || !string.Equals(selectedVariant.Id, brief.SelectedVariantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Merged report selectedVariantId must match job pin.");
        }

        if (string.Equals(rules.OverallSeverity, WeddingPlannerQaFindingSeverities.Block, StringComparison.Ordinal)
            && string.Equals(inspection.Contribution.ProposedOutcome, WeddingPlannerQaProposedOutcomes.PassRecommended, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Merged report cannot recommend PASS under rules BLOCK.");
        }

        if (!string.Equals(steward.ContributionSource, WeddingPlannerQaContributionSources.RulesHuman, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Steward contributionSource must be RULES_HUMAN.");
        }

        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.QaReviewReportV1,
            ["disclaimer"] = WeddingPlannerQaReviewReportDisclaimer.Text,
            ["provenance"] = new JsonObject
            {
                ["approvedCreativePackageVersionId"] = brief.ApprovedCreativePackageVersionId.ToString("D"),
                ["creativePackageDocumentSha256"] = brief.CreativePackageDocumentSha256,
                ["creativePackageDecisionId"] = brief.CreativePackageDecisionId.ToString("D"),
                ["selectedVariantId"] = brief.SelectedVariantId,
                ["selectedCreativeAssetId"] = brief.SelectedCreativeAssetId.ToString("D"),
                ["selectedCreativeAssetSha256"] = brief.SelectedCreativeAssetSha256,
                ["selectedCreativeAssetMeta"] = new JsonObject
                {
                    ["contentType"] = assetMeta.ContentType,
                    ["byteSize"] = assetMeta.ByteSize,
                    ["width"] = assetMeta.Width,
                    ["height"] = assetMeta.Height
                },
                ["selectedConceptId"] = brief.SelectedConceptId,
                ["approvedBrandDnaVersionId"] = brief.ApprovedBrandDnaVersionId.ToString("D"),
                ["approvedBrandDnaVersionNumber"] = brief.ApprovedBrandDnaVersionNumber,
                ["approvedColorProfileVersionId"] = brief.ApprovedColorProfileVersionId.ToString("D"),
                ["approvedColorProfileVersionNumber"] = brief.ApprovedColorProfileVersionNumber,
                ["approvedResearchReportVersionId"] = brief.ApprovedResearchReportVersionId.ToString("D"),
                ["approvedResearchReportVersionNumber"] = brief.ApprovedResearchReportVersionNumber,
                ["qaReviewJobId"] = qaReviewJobId.ToString("D")
            },
            ["brief"] = new JsonObject
            {
                ["reviewObjective"] = brief.ReviewObjective,
                ["focusAreas"] = ToJsonArray(brief.FocusAreas),
                ["notes"] = brief.Notes
            },
            ["rules"] = JsonNode.Parse(rules.FindingsJson)!,
            ["selectedVariantSnapshot"] = new JsonObject
            {
                ["id"] = selectedVariant.Id,
                ["format"] = selectedVariant.Format,
                ["canvas"] = new JsonObject
                {
                    ["width"] = selectedVariant.Width,
                    ["height"] = selectedVariant.Height
                },
                ["copy"] = new JsonObject
                {
                    ["kind"] = selectedVariant.CopyKind,
                    ["headline"] = selectedVariant.Headline,
                    ["body"] = selectedVariant.Body,
                    ["cta"] = selectedVariant.Cta
                },
                ["factualClaims"] = new JsonArray(selectedVariant.FactualClaims.Select(c => (JsonNode)new JsonObject
                {
                    ["statement"] = c.Statement,
                    ["sourceIds"] = ToJsonArray(c.SourceIds)
                }).ToArray()),
                ["asset"] = new JsonObject
                {
                    ["creativeAssetId"] = brief.SelectedCreativeAssetId.ToString("D"),
                    ["contentType"] = assetMeta.ContentType,
                    ["byteSize"] = assetMeta.ByteSize,
                    ["sha256"] = brief.SelectedCreativeAssetSha256,
                    ["width"] = assetMeta.Width,
                    ["height"] = assetMeta.Height
                }
            },
            ["contributions"] = new JsonArray(
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerQaLogicalRoles.CreativeChaperone,
                    ["contributionSource"] = WeddingPlannerQaContributionSources.Ai,
                    ["summary"] = chaperone.Contribution.Summary
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerQaLogicalRoles.QaInspector,
                    ["contributionSource"] = WeddingPlannerQaContributionSources.Ai,
                    ["summary"] = inspection.Contribution.Summary,
                    ["proposedOutcome"] = inspection.Contribution.ProposedOutcome
                },
                new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerQaLogicalRoles.HumanEscalationSteward,
                    ["contributionSource"] = WeddingPlannerQaContributionSources.RulesHuman,
                    ["summary"] = steward.Summary,
                    ["routing"] = new JsonObject
                    {
                        ["rulesOverallSeverity"] = steward.RulesOverallSeverity,
                        ["blockerCodes"] = ToJsonArray(steward.BlockerCodes),
                        ["warnCodes"] = ToJsonArray(steward.WarnCodes),
                        ["proposedRouting"] = steward.ProposedRouting,
                        ["requiredHumanAuthority"] = ToJsonArray(WeddingPlannerQaRequiredHumanAuthority.All)
                    }
                })
        };

        if (requireSyntheticMarker)
        {
            document["marker"] = WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview;
        }

        RejectForbiddenKeysEverywhere(document);
        if (!string.Equals(document["disclaimer"]?.GetValue<string>(), WeddingPlannerQaReviewReportDisclaimer.Text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("QA review report disclaimer must match the locked Phase 7 string.");
        }

        var contributions = document["contributions"] as JsonArray
            ?? throw new InvalidOperationException("QA review report contributions are required.");
        if (contributions.Count != 3)
        {
            throw new InvalidOperationException("QA review report must contain exactly 3 contributions.");
        }

        var ordered = OrderObjectKeys(document);
        var documentJson = ordered.ToJsonString(NodeWriteOptions);
        if (requireSyntheticMarker
            && !documentJson.Contains(WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Local QA path requires SYNTHETIC DEVELOPMENT QA REVIEW marker.");
        }

        var focusList = string.Join(",", brief.FocusAreas);
        var summary =
            $"{WeddingPlannerSchemaVersions.QaReviewReportV1}; selectedVariantId={brief.SelectedVariantId}; " +
            $"rules={rules.OverallSeverity}; contributions=3; agentRuns=2; focusAreas=[{focusList}]";

        var roleContributions = new List<CanonicalQaRoleContributionPayload>
        {
            new(
                WeddingPlannerQaLogicalRoles.CreativeChaperone,
                WeddingPlannerQaContributionSources.Ai,
                chaperoneRunId,
                SerializeContributionJson(new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerQaLogicalRoles.CreativeChaperone,
                    ["contributionSource"] = WeddingPlannerQaContributionSources.Ai,
                    ["summary"] = chaperone.Contribution.Summary,
                    ["riskNotes"] = new JsonObject
                    {
                        ["boundary"] = ToJsonArray(chaperone.Contribution.RiskNotes.Boundary),
                        ["provenance"] = ToJsonArray(chaperone.Contribution.RiskNotes.Provenance),
                        ["brand"] = ToJsonArray(chaperone.Contribution.RiskNotes.Brand),
                        ["claims"] = ToJsonArray(chaperone.Contribution.RiskNotes.Claims)
                    },
                    ["uncertainties"] = ToJsonArray(chaperone.Contribution.Uncertainties)
                })),
            new(
                WeddingPlannerQaLogicalRoles.QaInspector,
                WeddingPlannerQaContributionSources.Ai,
                qaInspectionRunId,
                SerializeContributionJson(new JsonObject
                {
                    ["logicalRole"] = WeddingPlannerQaLogicalRoles.QaInspector,
                    ["contributionSource"] = WeddingPlannerQaContributionSources.Ai,
                    ["summary"] = inspection.Contribution.Summary,
                    ["alignmentNotes"] = new JsonObject
                    {
                        ["format"] = ToJsonArray(inspection.Contribution.AlignmentNotes.Format),
                        ["copy"] = ToJsonArray(inspection.Contribution.AlignmentNotes.Copy),
                        ["assetMetadata"] = ToJsonArray(inspection.Contribution.AlignmentNotes.AssetMetadata)
                    },
                    ["uncertainties"] = ToJsonArray(inspection.Contribution.Uncertainties),
                    ["proposedOutcome"] = inspection.Contribution.ProposedOutcome
                })),
            new(
                WeddingPlannerQaLogicalRoles.HumanEscalationSteward,
                WeddingPlannerQaContributionSources.RulesHuman,
                null,
                steward.ContributionJson)
        };

        return new CanonicalQaReviewReport(documentJson, Truncate(summary, 2000), roleContributions);
    }

    public static string SerializeContributionJson(JsonObject payload) =>
        OrderObjectKeys(payload).ToJsonString(NodeWriteOptions);

    public static bool DocumentContainsSyntheticMarker(string documentJson) =>
        documentJson.Contains(WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview, StringComparison.Ordinal);

    public static IReadOnlyList<string> ExtractBlockerCodes(string rulesFindingsJson)
    {
        var root = ParseObject(rulesFindingsJson, "Rules findings");
        if (root["findings"] is not JsonArray findings)
        {
            return [];
        }

        var codes = new List<string>();
        foreach (var item in findings)
        {
            if (item is not JsonObject obj)
            {
                continue;
            }

            var severity = obj["severity"]?.GetValue<string>();
            var code = obj["code"]?.GetValue<string>();
            if (string.Equals(severity, WeddingPlannerQaFindingSeverities.Block, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(code))
            {
                codes.Add(code);
            }
        }

        return codes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    public static string ExtractRulesOverallSeverity(string rulesFindingsJson)
    {
        var root = ParseObject(rulesFindingsJson, "Rules findings");
        return Required(root["overallSeverity"]?.GetValue<string>(), "overallSeverity", 16);
    }

    public static void ValidateDecisionBody(
        string decision,
        string rationale,
        string selectedVariantId,
        string expectedSelectedVariantId,
        bool? visualReviewConfirmed,
        bool? copyReviewConfirmed,
        bool? provenanceReviewConfirmed,
        bool? syntheticMarkerAcknowledged,
        string? escalationCategory,
        bool reportHasSyntheticMarker,
        string rulesOverallSeverity,
        JsonNode? rawNode)
    {
        if (rawNode is not null)
        {
            RejectForbiddenDecisionFields(rawNode);
        }

        var normalized = Required(decision, nameof(decision), 64).ToUpperInvariant();
        if (!DecisionSet.Contains(normalized))
        {
            throw new InvalidOperationException("decision must be ACCEPT, RETURN_FOR_REVISION, or ESCALATE.");
        }

        Required(rationale, nameof(rationale), 2000);
        var echo = Required(selectedVariantId, nameof(selectedVariantId), 32);
        if (!string.Equals(echo, expectedSelectedVariantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("selectedVariantId must exactly echo the report/job pin.");
        }

        if (normalized == WeddingPlannerQaReviewDecisions.Accept)
        {
            if (string.Equals(rulesOverallSeverity, WeddingPlannerQaFindingSeverities.Block, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("ACCEPT is forbidden when rules overallSeverity is BLOCK.");
            }

            if (visualReviewConfirmed != true || copyReviewConfirmed != true || provenanceReviewConfirmed != true)
            {
                throw new InvalidOperationException(
                    "ACCEPT requires visualReviewConfirmed, copyReviewConfirmed, and provenanceReviewConfirmed to be true.");
            }

            if (reportHasSyntheticMarker && syntheticMarkerAcknowledged != true)
            {
                throw new InvalidOperationException(
                    "ACCEPT requires syntheticMarkerAcknowledged=true when the report carries SYNTHETIC DEVELOPMENT QA REVIEW.");
            }
        }
        else if (normalized == WeddingPlannerQaReviewDecisions.Escalate)
        {
            var category = Required(escalationCategory, nameof(escalationCategory), 64);
            if (!EscalationCategorySet.Contains(category))
            {
                throw new InvalidOperationException("escalationCategory is invalid.");
            }
        }
    }

    public static void ValidateResolutionBody(
        string resolution,
        string rationale,
        string? exceptionRationale,
        bool? exceptionAcknowledged,
        IReadOnlyList<string>? acknowledgedBlockerCodes,
        IReadOnlyList<string> expectedBlockerCodes,
        bool canWaive,
        JsonNode? rawNode)
    {
        if (rawNode is not null)
        {
            RejectForbiddenDecisionFields(rawNode);
        }

        var normalized = Required(resolution, nameof(resolution), 64).ToUpperInvariant();
        if (!ResolutionSet.Contains(normalized))
        {
            throw new InvalidOperationException("resolution must be RETURN_FOR_REVISION or WAIVE_AND_ACCEPT.");
        }

        Required(rationale, nameof(rationale), 2000);
        if (normalized == WeddingPlannerQaEscalationResolutions.WaiveAndAccept)
        {
            if (!canWaive)
            {
                throw new WeddingPlannerForbiddenException("WAIVE_AND_ACCEPT requires operator or admin authority.");
            }

            Required(exceptionRationale, nameof(exceptionRationale), 2000);
            if (exceptionAcknowledged != true)
            {
                throw new InvalidOperationException("WAIVE_AND_ACCEPT requires exceptionAcknowledged=true.");
            }

            var ack = acknowledgedBlockerCodes ?? Array.Empty<string>();
            var expected = expectedBlockerCodes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var actual = ack.Select(x => Required(x, "acknowledgedBlockerCodes[]", 64))
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray();
            if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "acknowledgedBlockerCodes must exactly match all BLOCK finding codes on the report.");
            }
        }
    }

    public static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256Hex(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string DeriveProposedRouting(string rulesOverallSeverity, string? qaProposedOutcome)
    {
        if (string.Equals(rulesOverallSeverity, WeddingPlannerQaFindingSeverities.Block, StringComparison.Ordinal)
            || string.Equals(qaProposedOutcome, WeddingPlannerQaProposedOutcomes.HumanEscalation, StringComparison.Ordinal))
        {
            return WeddingPlannerQaProposedRoutings.EscalationSuggested;
        }

        if (string.Equals(qaProposedOutcome, WeddingPlannerQaProposedOutcomes.ReturnForRevision, StringComparison.Ordinal)
            || string.Equals(rulesOverallSeverity, WeddingPlannerQaFindingSeverities.Warn, StringComparison.Ordinal))
        {
            return WeddingPlannerQaProposedRoutings.ReturnSuggested;
        }

        return WeddingPlannerQaProposedRoutings.HumanReview;
    }

    private static void RejectApprovalOrPixelClaims(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("approve", StringComparison.Ordinal)
            || lower.Contains("pixel-pass", StringComparison.Ordinal)
            || lower.Contains("visually certified", StringComparison.Ordinal)
            || lower.Contains("pixel certified", StringComparison.Ordinal)
            || lower.Contains("campaign-ready", StringComparison.Ordinal)
            || lower.Contains("waive", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Chaperone notes must not claim approval, waive, or pixel certification.");
        }
    }

    private static JsonObject ParseStageRoot(
        string? stageJson,
        string label,
        string expectedSchema,
        string expectedProfile,
        string selectedVariantId,
        bool requireSyntheticMarker)
    {
        var root = ParseObject(stageJson, label);
        AssertOnlyKnownProperties(
            root,
            "schemaVersion", "workerProfileVersion", "marker", "selectedVariantId", "rulesOverallSeverity", "contributions");
        var schema = Required(root["schemaVersion"]?.GetValue<string>(), "schemaVersion", 64);
        if (!string.Equals(schema, expectedSchema, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} schemaVersion must be {expectedSchema}.");
        }

        var profile = Required(root["workerProfileVersion"]?.GetValue<string>(), "workerProfileVersion", 64);
        if (!string.Equals(profile, expectedProfile, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} workerProfileVersion must be {expectedProfile}.");
        }

        var variant = Required(root["selectedVariantId"]?.GetValue<string>(), "selectedVariantId", 32);
        if (!string.Equals(variant, selectedVariantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} selectedVariantId must equal the job pin.");
        }

        if (requireSyntheticMarker)
        {
            var marker = Required(root["marker"]?.GetValue<string>(), "marker", 128);
            if (!string.Equals(marker, WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{label} requires marker '{WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview}'.");
            }
        }

        return root;
    }

    private static List<JsonObject> ParseContributionsArray(JsonObject root)
    {
        if (root["contributions"] is not JsonArray arr || arr.Count == 0)
        {
            throw new InvalidOperationException("contributions must be a non-empty array.");
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

    private static IReadOnlyList<string> ParseStringList(JsonNode? node, string label, int maxItems, int maxLength)
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

        var values = new List<string>(arr.Count);
        foreach (var item in arr)
        {
            values.Add(Required(item?.GetValue<string>(), label, maxLength));
        }

        return values;
    }

    private static JsonObject ParseObject(string? json, string label)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException($"{label} JSON is required.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{label} JSON is invalid.", ex);
        }

        if (node is not JsonObject obj)
        {
            throw new InvalidOperationException($"{label} must be a JSON object.");
        }

        return obj;
    }

    private static void AssertOnlyKnownProperties(JsonObject obj, params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in obj)
        {
            if (!set.Contains(property.Key))
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
                if (ForbiddenOutputKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key))
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
            || k.Contains("qaready", StringComparison.Ordinal)
            || k.Contains("blissready", StringComparison.Ordinal)
            || k.Contains("legalcleared", StringComparison.Ordinal)
            || k.Contains("matchid", StringComparison.Ordinal)
            || k.Contains("placementid", StringComparison.Ordinal)
            || k.Contains("inventoryid", StringComparison.Ordinal)
            || k.Contains("imagebytes", StringComparison.Ordinal)
            || k.Contains("base64", StringComparison.Ordinal)
            || k.Contains("pixel", StringComparison.Ordinal);
    }

    private static JsonObject OrderObjectKeys(JsonObject obj)
    {
        var ordered = new JsonObject();
        foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            ordered[property.Key] = property.Value is JsonObject child
                ? OrderObjectKeys(child)
                : property.Value is JsonArray arr
                    ? new JsonArray(arr.Select(x => x is JsonObject o ? OrderObjectKeys(o) : x?.DeepClone()).ToArray())
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

public sealed record CanonicalQaReviewBrief(
    string ReviewObjective,
    IReadOnlyList<string> FocusAreas,
    string? Notes,
    Guid ApprovedCreativePackageVersionId,
    string CreativePackageDocumentSha256,
    Guid CreativePackageDecisionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    string SelectedCreativeAssetSha256,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string InputJson,
    string InputSha256);

public sealed record CanonicalQaRiskNotes(
    IReadOnlyList<string> Boundary,
    IReadOnlyList<string> Provenance,
    IReadOnlyList<string> Brand,
    IReadOnlyList<string> Claims);

public sealed record CanonicalQaChaperoneContribution(
    string LogicalRole,
    string Summary,
    CanonicalQaRiskNotes RiskNotes,
    IReadOnlyList<string> Uncertainties);

public sealed record CanonicalQaChaperoneStageOutput(
    string CanonicalJson,
    string WorkerProfileVersion,
    string SelectedVariantId,
    CanonicalQaChaperoneContribution Contribution);

public sealed record CanonicalQaAlignmentNotes(
    IReadOnlyList<string> Format,
    IReadOnlyList<string> Copy,
    IReadOnlyList<string> AssetMetadata);

public sealed record CanonicalQaInspectionContribution(
    string LogicalRole,
    string Summary,
    CanonicalQaAlignmentNotes AlignmentNotes,
    IReadOnlyList<string> Uncertainties,
    string ProposedOutcome);

public sealed record CanonicalQaInspectionStageOutput(
    string CanonicalJson,
    string WorkerProfileVersion,
    string SelectedVariantId,
    string RulesOverallSeverity,
    CanonicalQaInspectionContribution Contribution);

public sealed record CanonicalQaStewardContribution(
    string LogicalRole,
    string ContributionSource,
    string Summary,
    string RulesOverallSeverity,
    IReadOnlyList<string> BlockerCodes,
    IReadOnlyList<string> WarnCodes,
    string ProposedRouting,
    string ContributionJson);

public sealed record CanonicalQaRoleContributionPayload(
    string LogicalRole,
    string ContributionSource,
    Guid? ProducingAgentRunId,
    string ContributionJson);

public sealed record CanonicalQaReviewReport(
    string DocumentJson,
    string Summary,
    IReadOnlyList<CanonicalQaRoleContributionPayload> RoleContributions);

public sealed record CanonicalQaRulesFinding(string Code, string Severity, string Message);

public sealed record CanonicalQaRulesFindings(
    string OverallSeverity,
    IReadOnlyList<CanonicalQaRulesFinding> Findings,
    string FindingsJson);

public sealed record SelectedVariantQaSnapshot(
    string Id,
    string Format,
    int Width,
    int Height,
    string CopyKind,
    string Headline,
    string Body,
    string Cta,
    IReadOnlyList<CanonicalCreativeFactualClaim> FactualClaims,
    IReadOnlyList<string> PaletteRoleRefs,
    Guid? PackageAssetId,
    string? PackageAssetContentType,
    int? PackageAssetByteSize,
    string? PackageAssetSha256,
    int? PackageAssetWidth,
    int? PackageAssetHeight);

public sealed record QaAssetMetaSnapshot(
    string ContentType,
    int ByteSize,
    int Width,
    int Height);
