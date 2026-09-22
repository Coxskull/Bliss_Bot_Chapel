using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Strict validators/canonicalizers for measurement-learning briefs, metrics, two stage outputs,
/// and measurement-learning-report.v1 merge. AI never receives asset bytes, URLs, raw events, or PII.
/// </summary>
public static class WeddingPlannerMeasurementLearningValidation
{
    public const int MaxSourceLabelLength = 128;
    public const int MaxSourceSystemLength = 64;
    public const int MaxNotesLength = 4000;
    public const int MaxRationaleLength = 2000;
    public const int MaxSummaryLength = 2000;
    public const int MaxContributionSummaryLength = 2000;
    public const int MaxListItemLength = 1000;
    public const int MaxListItems = 12;
    public const int MaxObservationDays = 366;
    public const int MetricScale = 6;

    private static readonly Regex CurrencyCodeRegex = new("^[A-Z]{3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly HashSet<string> DecisionSet = new(WeddingPlannerMeasurementLearningDecisions.All, StringComparer.Ordinal);

    private static readonly HashSet<string> ForbiddenInputKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "events", "event", "eventArray", "event_level", "impressionsEvents",
        "userId", "userIds", "deviceId", "deviceIds", "contactId", "email", "phone",
        "cookie", "cookies", "ip", "ipAddress", "ipAddresses",
        "url", "urls", "href", "src", "pixel", "pixels", "script", "scripts",
        "html", "css", "svg", "markup", "base64",
        "credential", "credentials", "apiKey", "password", "token",
        "rawProviderPayload", "providerPayload", "rawPayload",
        "ctr", "cpm", "cpc", "cpa", "roas", "conversionRate", "clickThroughRate",
        "confidence", "significance", "pValue", "statisticallySignificant",
        "attributionModel", "attribution", "incrementality",
        "statusOverride", "handshakeStatusOverride", "placementStatusOverride",
        "aiProvider", "aiEndpoint", "aiApiKey", "providerToolConfiguration",
        "mutateUpstream", "activateCampaign", "reviseCreative", "changeSpend",
        "campaignReadinessHandshakeVersionIdOverride",
        "approvedCreativePackageVersionId", "selectedVariantId", "creativeAssetId",
        "qaReviewReportVersionId", "blissMatchId", "campaignId", "placementId",
        "campaignPlacementId", "adInventorySlotId", "contentItemId"
    };

    private static readonly HashSet<string> ForbiddenOutputKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "css", "svg", "script", "src", "url", "href", "base64",
        "imageBytes", "image_bytes", "imageData", "image_data", "pixels", "pixelArray",
        "events", "event", "userId", "deviceId", "cookie", "ip", "email", "phone",
        "credential", "credentials", "apiKey", "password", "token",
        "activateCampaign", "reviseCreative", "changeSpend", "reserveInventory",
        "mutateUpstream", "aiProvider", "aiEndpoint", "aiApiKey", "providerToolConfiguration"
    };

    private static readonly Regex ForbiddenClaimRegex = new(
        @"\b(proved|caused|guaranteed|certified)\b|statistically\s+significant|autonomously\s+applied|delivered\s+by\s+bliss|attribution\s+certainty",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void RejectForbiddenInputFields(JsonNode? node, string path = "$")
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (ForbiddenInputKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key))
                {
                    throw new InvalidOperationException(
                        $"Forbidden measurement-learning field '{property.Key}' is not allowed.");
                }

                RejectForbiddenInputFields(property.Value, path + "." + property.Key);
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                RejectForbiddenInputFields(arr[i], path + $"[{i}]");
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

    public static bool IsIso4217LikeCurrency(string? currencyCode) =>
        !string.IsNullOrWhiteSpace(currencyCode) && CurrencyCodeRegex.IsMatch(currencyCode);

    public static bool IsValidObservationWindow(DateTime observationStart, DateTime observationEnd, DateTime utcNow)
    {
        if (observationStart.Kind != DateTimeKind.Utc || observationEnd.Kind != DateTimeKind.Utc)
        {
            return false;
        }

        if (observationEnd <= observationStart)
        {
            return false;
        }

        if (observationEnd > utcNow)
        {
            return false;
        }

        var days = (observationEnd - observationStart).TotalDays;
        return days is > 0 and <= MaxObservationDays;
    }

    public static CanonicalMeasurementLearningMetrics ComputeMetrics(
        long impressions,
        long clicks,
        long conversions,
        decimal spend,
        decimal? revenue)
    {
        decimal? ctr = impressions > 0 ? Round(clicks / (decimal)impressions) : null;
        decimal? conversionRate = clicks > 0 ? Round(conversions / (decimal)clicks) : null;
        decimal? cpm = impressions > 0 ? Round(spend * 1000m / impressions) : null;
        decimal? cpc = clicks > 0 ? Round(spend / clicks) : null;
        decimal? cpa = conversions > 0 ? Round(spend / conversions) : null;
        decimal? roas = revenue is decimal rev && spend > 0m ? Round(rev / spend) : null;

        var document = new JsonObject
        {
            ["ctr"] = ctr is null ? null : JsonValue.Create(ctr.Value),
            ["conversionRate"] = conversionRate is null ? null : JsonValue.Create(conversionRate.Value),
            ["cpm"] = cpm is null ? null : JsonValue.Create(cpm.Value),
            ["cpc"] = cpc is null ? null : JsonValue.Create(cpc.Value),
            ["cpa"] = cpa is null ? null : JsonValue.Create(cpa.Value),
            ["roas"] = roas is null ? null : JsonValue.Create(roas.Value),
            ["roundingScale"] = MetricScale,
            ["nullDenominatorSemantics"] = "zero-denominator-yields-null"
        };

        return new CanonicalMeasurementLearningMetrics(
            ctr,
            conversionRate,
            cpm,
            cpc,
            cpa,
            roas,
            OrderObjectKeys(document).ToJsonString(NodeWriteOptions));
    }

    public static CanonicalMeasurementLearningBrief CanonicalizeBrief(
        Guid campaignReadinessHandshakeVersionId,
        DateTime observationStart,
        DateTime observationEnd,
        string sourceLabel,
        string observationSourceSystem,
        bool attestationAcknowledged,
        long impressions,
        long clicks,
        long conversions,
        decimal spend,
        decimal? revenue,
        string currencyCode,
        string? notes,
        Guid campaignPlacementId,
        Guid campaignPlacementRunId,
        Guid blissMatchId,
        Guid campaignId,
        Guid contentItemId,
        Guid adInventorySlotId,
        Guid qaReviewReportVersionId,
        Guid approvedCreativePackageVersionId,
        string selectedVariantId,
        Guid selectedCreativeAssetId,
        Guid approvedConceptPackageVersionId,
        string selectedConceptId,
        Guid approvedBrandDnaVersionId,
        int approvedBrandDnaVersionNumber,
        Guid approvedColorProfileVersionId,
        int approvedColorProfileVersionNumber,
        Guid approvedResearchReportVersionId,
        int approvedResearchReportVersionNumber,
        string handshakeStatusSnapshot,
        bool handshakeWasCurrentAtJobStart,
        string aiProviderKind,
        string aiWorkerKey,
        DateTime utcNow)
    {
        if (!attestationAcknowledged)
        {
            throw new InvalidOperationException("attestationAcknowledged must be true.");
        }

        if (!IsValidObservationWindow(observationStart, observationEnd, utcNow))
        {
            throw new InvalidOperationException(
                "observationStart/observationEnd must be UTC with end strictly after start, not future-ended, and within 366 days.");
        }

        var label = Required(sourceLabel, nameof(sourceLabel), MaxSourceLabelLength);
        var obsSource = Required(observationSourceSystem, nameof(observationSourceSystem), MaxSourceSystemLength)
            .ToUpperInvariant();
        if (!string.Equals(obsSource, observationSourceSystem.Trim(), StringComparison.Ordinal)
            && !string.Equals(obsSource, observationSourceSystem.Trim().ToUpperInvariant(), StringComparison.Ordinal))
        {
            // normalized upper already applied
        }

        if (impressions < 0 || clicks < 0 || conversions < 0 || clicks > impressions)
        {
            throw new InvalidOperationException(
                "impressions/clicks/conversions must be >= 0 and clicks must be <= impressions.");
        }

        if (spend < 0m || (revenue is < 0m))
        {
            throw new InvalidOperationException("spend and optional revenue must be >= 0.");
        }

        var currency = Required(currencyCode, nameof(currencyCode), 3).ToUpperInvariant();
        if (!IsIso4217LikeCurrency(currency))
        {
            throw new InvalidOperationException("currencyCode must be a three-letter uppercase ISO 4217-like value.");
        }

        string? normalizedNotes = null;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            normalizedNotes = Required(notes, nameof(notes), MaxNotesLength);
            AssertNoForbiddenClaims(normalizedNotes, "notes");
        }

        var metrics = ComputeMetrics(impressions, clicks, conversions, spend, revenue);

        var canonical = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.MeasurementLearningBriefV1,
            ["campaignReadinessHandshakeVersionId"] = campaignReadinessHandshakeVersionId.ToString("D"),
            ["observationStart"] = observationStart.ToString("O", CultureInfo.InvariantCulture),
            ["observationEnd"] = observationEnd.ToString("O", CultureInfo.InvariantCulture),
            ["sourceLabel"] = label,
            ["observationSourceSystem"] = obsSource,
            ["attestationAcknowledged"] = true,
            ["attestationText"] = WeddingPlannerMeasurementLearningAttestation.Text,
            ["impressions"] = impressions,
            ["clicks"] = clicks,
            ["conversions"] = conversions,
            ["spend"] = spend,
            ["revenue"] = revenue is null ? null : JsonValue.Create(revenue.Value),
            ["currencyCode"] = currency,
            ["notes"] = normalizedNotes,
            ["handshakeStatusSnapshot"] = Required(handshakeStatusSnapshot, nameof(handshakeStatusSnapshot), 64),
            ["handshakeWasCurrentAtJobStart"] = handshakeWasCurrentAtJobStart,
            ["campaignPlacementId"] = campaignPlacementId.ToString("D"),
            ["campaignPlacementRunId"] = campaignPlacementRunId.ToString("D"),
            ["blissMatchId"] = blissMatchId.ToString("D"),
            ["campaignId"] = campaignId.ToString("D"),
            ["contentItemId"] = contentItemId.ToString("D"),
            ["adInventorySlotId"] = adInventorySlotId.ToString("D"),
            ["qaReviewReportVersionId"] = qaReviewReportVersionId.ToString("D"),
            ["approvedCreativePackageVersionId"] = approvedCreativePackageVersionId.ToString("D"),
            ["selectedVariantId"] = Required(selectedVariantId, nameof(selectedVariantId), 32),
            ["selectedCreativeAssetId"] = selectedCreativeAssetId.ToString("D"),
            ["approvedConceptPackageVersionId"] = approvedConceptPackageVersionId.ToString("D"),
            ["selectedConceptId"] = Required(selectedConceptId, nameof(selectedConceptId), 32),
            ["approvedBrandDnaVersionId"] = approvedBrandDnaVersionId.ToString("D"),
            ["approvedBrandDnaVersionNumber"] = approvedBrandDnaVersionNumber,
            ["approvedColorProfileVersionId"] = approvedColorProfileVersionId.ToString("D"),
            ["approvedColorProfileVersionNumber"] = approvedColorProfileVersionNumber,
            ["approvedResearchReportVersionId"] = approvedResearchReportVersionId.ToString("D"),
            ["approvedResearchReportVersionNumber"] = approvedResearchReportVersionNumber,
            ["derivedMetrics"] = JsonNode.Parse(metrics.MetricsJson),
            ["measurementRulesContractVersion"] = WeddingPlannerMeasurementLearningContractVersions.MeasurementRulesV1,
            ["measurementLearningOrchestrationContractVersion"] =
                WeddingPlannerMeasurementLearningContractVersions.MeasurementLearningOrchestrationV1,
            ["aiProviderKind"] = Required(aiProviderKind, nameof(aiProviderKind), 64),
            ["aiWorkerKey"] = Required(aiWorkerKey, nameof(aiWorkerKey), 64)
        };

        var ordered = OrderObjectKeys(canonical);
        var inputJson = ordered.ToJsonString(NodeWriteOptions);
        return new CanonicalMeasurementLearningBrief(
            campaignReadinessHandshakeVersionId,
            observationStart,
            observationEnd,
            label,
            obsSource,
            true,
            impressions,
            clicks,
            conversions,
            spend,
            revenue,
            currency,
            normalizedNotes,
            campaignPlacementId,
            campaignPlacementRunId,
            blissMatchId,
            campaignId,
            contentItemId,
            adInventorySlotId,
            qaReviewReportVersionId,
            approvedCreativePackageVersionId,
            selectedVariantId,
            selectedCreativeAssetId,
            approvedConceptPackageVersionId,
            selectedConceptId,
            approvedBrandDnaVersionId,
            approvedBrandDnaVersionNumber,
            approvedColorProfileVersionId,
            approvedColorProfileVersionNumber,
            approvedResearchReportVersionId,
            approvedResearchReportVersionNumber,
            handshakeStatusSnapshot,
            handshakeWasCurrentAtJobStart,
            metrics,
            inputJson,
            Sha256Hex(inputJson));
    }

    public static CanonicalPerformanceAnalysisStageOutput CanonicalizePerformanceAnalysisOutput(
        string? stageJson,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Performance analysis stage output",
            WeddingPlannerSchemaVersions.PerformanceAnalysisWorkerOutputV1,
            WeddingPlannerMeasurementLearningWorkerProfiles.PerformanceAnalysisV1,
            requireSyntheticMarker);

        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "marker", "contributions");
        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 1)
        {
            throw new InvalidOperationException("Performance analysis stage must contain exactly one contribution.");
        }

        var contribution = contributions[0];
        AssertOnlyKnownProperties(
            contribution,
            "logicalRole",
            "summary",
            "descriptivePatterns",
            "limitations",
            "dataGaps");

        var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
        if (!string.Equals(role, WeddingPlannerMeasurementLearningLogicalRoles.PerformanceAnalyst, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Performance analysis contribution logicalRole must be {WeddingPlannerMeasurementLearningLogicalRoles.PerformanceAnalyst}.");
        }

        var summary = Required(contribution["summary"]?.GetValue<string>(), "summary", MaxContributionSummaryLength);
        AssertNoForbiddenClaims(summary, "summary");
        var patterns = ParseStringList(contribution["descriptivePatterns"], "descriptivePatterns", requireNonEmpty: true);
        var limitations = ParseStringList(contribution["limitations"], "limitations", requireNonEmpty: true);
        var dataGaps = ParseStringList(contribution["dataGaps"], "dataGaps", requireNonEmpty: true);

        var canonicalContribution = new CanonicalMeasurementLearningRoleContributionPayload(
            role,
            summary,
            patterns,
            limitations,
            dataGaps,
            Array.Empty<string>(),
            Array.Empty<string>(),
            contribution.ToJsonString(NodeWriteOptions));

        return new CanonicalPerformanceAnalysisStageOutput(
            WeddingPlannerMeasurementLearningWorkerProfiles.PerformanceAnalysisV1,
            requireSyntheticMarker ? WeddingPlannerMeasurementLearningMarkers.SyntheticDevelopmentMeasurementLearning : null,
            canonicalContribution,
            root.ToJsonString(NodeWriteOptions));
    }

    public static CanonicalLearningSynthesisStageOutput CanonicalizeLearningSynthesisOutput(
        string? stageJson,
        bool requireSyntheticMarker)
    {
        var root = ParseStageRoot(
            stageJson,
            "Learning synthesis stage output",
            WeddingPlannerSchemaVersions.LearningSynthesisWorkerOutputV1,
            WeddingPlannerMeasurementLearningWorkerProfiles.LearningSynthesisV1,
            requireSyntheticMarker);

        AssertOnlyKnownProperties(root, "schemaVersion", "workerProfileVersion", "marker", "contributions");
        var contributions = ParseContributionsArray(root);
        if (contributions.Count != 2)
        {
            throw new InvalidOperationException("Learning synthesis stage must contain exactly two contributions.");
        }

        CanonicalMeasurementLearningRoleContributionPayload? synthesizer = null;
        CanonicalMeasurementLearningRoleContributionPayload? advisor = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var contribution in contributions)
        {
            var role = Required(contribution["logicalRole"]?.GetValue<string>(), "logicalRole", 64);
            if (!seen.Add(role))
            {
                throw new InvalidOperationException($"Duplicate logicalRole '{role}' in learning synthesis output.");
            }

            var summary = Required(contribution["summary"]?.GetValue<string>(), "summary", MaxContributionSummaryLength);
            AssertNoForbiddenClaims(summary, "summary");

            if (string.Equals(role, WeddingPlannerMeasurementLearningLogicalRoles.LearningSynthesizer, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "learningHypotheses", "limitations");
                var hypotheses = ParseStringList(contribution["learningHypotheses"], "learningHypotheses", requireNonEmpty: true);
                var limitations = ParseStringList(contribution["limitations"], "limitations", requireNonEmpty: true);
                synthesizer = new CanonicalMeasurementLearningRoleContributionPayload(
                    role,
                    summary,
                    Array.Empty<string>(),
                    limitations,
                    Array.Empty<string>(),
                    hypotheses,
                    Array.Empty<string>(),
                    contribution.ToJsonString(NodeWriteOptions));
            }
            else if (string.Equals(role, WeddingPlannerMeasurementLearningLogicalRoles.OptimizationAdvisor, StringComparison.Ordinal))
            {
                AssertOnlyKnownProperties(contribution, "logicalRole", "summary", "recommendedFutureTests", "limitations");
                var tests = ParseStringList(contribution["recommendedFutureTests"], "recommendedFutureTests", requireNonEmpty: true);
                var limitations = ParseStringList(contribution["limitations"], "limitations", requireNonEmpty: true);
                advisor = new CanonicalMeasurementLearningRoleContributionPayload(
                    role,
                    summary,
                    Array.Empty<string>(),
                    limitations,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    tests,
                    contribution.ToJsonString(NodeWriteOptions));
            }
            else
            {
                throw new InvalidOperationException($"Unexpected learning synthesis logicalRole '{role}'.");
            }
        }

        if (synthesizer is null || advisor is null)
        {
            throw new InvalidOperationException(
                "Learning synthesis must include LEARNING_SYNTHESIZER and OPTIMIZATION_ADVISOR exactly once each.");
        }

        return new CanonicalLearningSynthesisStageOutput(
            WeddingPlannerMeasurementLearningWorkerProfiles.LearningSynthesisV1,
            requireSyntheticMarker ? WeddingPlannerMeasurementLearningMarkers.SyntheticDevelopmentMeasurementLearning : null,
            synthesizer,
            advisor,
            root.ToJsonString(NodeWriteOptions));
    }

    public static CanonicalMeasurementLearningReport MergeReport(
        CanonicalMeasurementLearningBrief brief,
        CanonicalMeasurementLearningRulesFindings rules,
        CanonicalPerformanceAnalysisStageOutput performance,
        CanonicalLearningSynthesisStageOutput synthesis,
        Guid performanceRunId,
        Guid synthesisRunId,
        Guid jobId,
        bool requireSyntheticMarker)
    {
        if (performanceRunId == Guid.Empty || synthesisRunId == Guid.Empty || jobId == Guid.Empty)
        {
            throw new InvalidOperationException("Report merge requires durable job and both agent run ids.");
        }

        if (performanceRunId == synthesisRunId)
        {
            throw new InvalidOperationException("Performance and learning synthesis runs must be distinct.");
        }

        var roles = new List<CanonicalMeasurementLearningMergedContribution>
        {
            new(
                WeddingPlannerMeasurementLearningLogicalRoles.PerformanceAnalyst,
                WeddingPlannerMeasurementLearningContributionSources.Ai,
                performanceRunId,
                performance.Contribution.ContributionJson),
            new(
                WeddingPlannerMeasurementLearningLogicalRoles.LearningSynthesizer,
                WeddingPlannerMeasurementLearningContributionSources.Ai,
                synthesisRunId,
                synthesis.Synthesizer.ContributionJson),
            new(
                WeddingPlannerMeasurementLearningLogicalRoles.OptimizationAdvisor,
                WeddingPlannerMeasurementLearningContributionSources.Ai,
                synthesisRunId,
                synthesis.Advisor.ContributionJson)
        };

        if (roles.Count != 3
            || roles.Select(r => r.LogicalRole).Distinct(StringComparer.Ordinal).Count() != 3)
        {
            throw new InvalidOperationException("Merged report must contain exactly three distinct role contributions.");
        }

        var summary = Truncate(
            $"Advisory measurement-learning report for handshake {brief.CampaignReadinessHandshakeVersionId:D} "
            + $"covering {brief.ObservationStart:O} to {brief.ObservationEnd:O}.",
            MaxSummaryLength);

        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.MeasurementLearningReportV1,
            ["summary"] = summary,
            ["disclaimer"] = WeddingPlannerMeasurementLearningReportDisclaimer.Text,
            ["attestationText"] = WeddingPlannerMeasurementLearningAttestation.Text,
            ["labels"] = new JsonArray(
                WeddingPlannerMeasurementLearningLabels.All.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray()),
            ["marker"] = requireSyntheticMarker
                ? WeddingPlannerMeasurementLearningMarkers.SyntheticDevelopmentMeasurementLearning
                : null,
            ["producingMeasurementLearningJobId"] = jobId.ToString("D"),
            ["performanceAnalysisAgentRunId"] = performanceRunId.ToString("D"),
            ["learningSynthesisAgentRunId"] = synthesisRunId.ToString("D"),
            ["campaignReadinessHandshakeVersionId"] = brief.CampaignReadinessHandshakeVersionId.ToString("D"),
            ["handshakeStatusSnapshot"] = brief.HandshakeStatusSnapshot,
            ["handshakeWasCurrentAtJobStart"] = brief.HandshakeWasCurrentAtJobStart,
            ["campaignPlacementId"] = brief.CampaignPlacementId.ToString("D"),
            ["campaignPlacementRunId"] = brief.CampaignPlacementRunId.ToString("D"),
            ["blissMatchId"] = brief.BlissMatchId.ToString("D"),
            ["campaignId"] = brief.CampaignId.ToString("D"),
            ["contentItemId"] = brief.ContentItemId.ToString("D"),
            ["adInventorySlotId"] = brief.AdInventorySlotId.ToString("D"),
            ["qaReviewReportVersionId"] = brief.QaReviewReportVersionId.ToString("D"),
            ["approvedCreativePackageVersionId"] = brief.ApprovedCreativePackageVersionId.ToString("D"),
            ["selectedVariantId"] = brief.SelectedVariantId,
            ["selectedCreativeAssetId"] = brief.SelectedCreativeAssetId.ToString("D"),
            ["approvedConceptPackageVersionId"] = brief.ApprovedConceptPackageVersionId.ToString("D"),
            ["selectedConceptId"] = brief.SelectedConceptId,
            ["approvedBrandDnaVersionId"] = brief.ApprovedBrandDnaVersionId.ToString("D"),
            ["approvedBrandDnaVersionNumber"] = brief.ApprovedBrandDnaVersionNumber,
            ["approvedColorProfileVersionId"] = brief.ApprovedColorProfileVersionId.ToString("D"),
            ["approvedColorProfileVersionNumber"] = brief.ApprovedColorProfileVersionNumber,
            ["approvedResearchReportVersionId"] = brief.ApprovedResearchReportVersionId.ToString("D"),
            ["approvedResearchReportVersionNumber"] = brief.ApprovedResearchReportVersionNumber,
            ["observedAggregates"] = new JsonObject
            {
                ["observationStart"] = brief.ObservationStart.ToString("O", CultureInfo.InvariantCulture),
                ["observationEnd"] = brief.ObservationEnd.ToString("O", CultureInfo.InvariantCulture),
                ["sourceLabel"] = brief.SourceLabel,
                ["observationSourceSystem"] = brief.ObservationSourceSystem,
                ["impressions"] = brief.Impressions,
                ["clicks"] = brief.Clicks,
                ["conversions"] = brief.Conversions,
                ["spend"] = brief.Spend,
                ["revenue"] = brief.Revenue is null ? null : JsonValue.Create(brief.Revenue.Value),
                ["currencyCode"] = brief.CurrencyCode,
                ["notes"] = brief.Notes
            },
            ["derivedMetrics"] = JsonNode.Parse(brief.Metrics.MetricsJson),
            ["rulesFindings"] = JsonNode.Parse(rules.FindingsJson),
            ["descriptivePatterns"] = ToJsonArray(performance.Contribution.DescriptivePatterns),
            ["limitations"] = ToJsonArray(
                performance.Contribution.Limitations
                    .Concat(synthesis.Synthesizer.Limitations)
                    .Concat(synthesis.Advisor.Limitations)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal)),
            ["dataGaps"] = ToJsonArray(performance.Contribution.DataGaps),
            ["learningHypotheses"] = ToJsonArray(synthesis.Synthesizer.LearningHypotheses),
            ["recommendedFutureTests"] = ToJsonArray(synthesis.Advisor.RecommendedFutureTests),
            ["roleContributions"] = new JsonArray(roles.Select(r => (JsonNode)new JsonObject
            {
                ["logicalRole"] = r.LogicalRole,
                ["contributionSource"] = r.ContributionSource,
                ["producingAgentRunId"] = r.ProducingAgentRunId.ToString("D")
            }).ToArray()),
            ["requiredLanguage"] = new JsonArray("association", "observation", "hypothesis", "recommendation"),
            ["placementDeliveryClaim"] = "PLANNED_ONLY_NOT_DELIVERY_PROOF"
        };

        var ordered = OrderObjectKeys(document);
        var documentJson = ordered.ToJsonString(NodeWriteOptions);

        return new CanonicalMeasurementLearningReport(
            summary,
            documentJson,
            roles);
    }

    public static string NormalizeDecision(string decision)
    {
        var value = Required(decision, nameof(decision), 64);
        if (!DecisionSet.Contains(value))
        {
            throw new InvalidOperationException($"Unknown measurement-learning decision '{value}'.");
        }

        return value;
    }

    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static JsonObject ParseStageRoot(
        string? stageJson,
        string label,
        string expectedSchema,
        string expectedProfile,
        bool requireSyntheticMarker)
    {
        if (string.IsNullOrWhiteSpace(stageJson))
        {
            throw new InvalidOperationException($"{label} is required.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(stageJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{label} is not valid JSON.", ex);
        }

        if (node is not JsonObject root)
        {
            throw new InvalidOperationException($"{label} must be a JSON object.");
        }

        RejectForbiddenOutputFields(root);
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

        if (requireSyntheticMarker)
        {
            var marker = Required(root["marker"]?.GetValue<string>(), "marker", 128);
            if (!string.Equals(
                    marker,
                    WeddingPlannerMeasurementLearningMarkers.SyntheticDevelopmentMeasurementLearning,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{label} marker must be {WeddingPlannerMeasurementLearningMarkers.SyntheticDevelopmentMeasurementLearning}.");
            }
        }
        else if (root["marker"] is not null)
        {
            var marker = Required(root["marker"]?.GetValue<string>(), "marker", 128);
            if (string.Equals(
                    marker,
                    WeddingPlannerMeasurementLearningMarkers.SyntheticDevelopmentMeasurementLearning,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Synthetic measurement-learning marker is not allowed for non-local providers.");
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

            RejectForbiddenOutputFields(obj);
            list.Add(obj);
        }

        return list;
    }

    private static IReadOnlyList<string> ParseStringList(JsonNode? node, string name, bool requireNonEmpty)
    {
        if (node is not JsonArray arr)
        {
            throw new InvalidOperationException($"{name} must be an array.");
        }

        if (requireNonEmpty && arr.Count == 0)
        {
            throw new InvalidOperationException($"{name} must contain at least one item.");
        }

        if (arr.Count > MaxListItems)
        {
            throw new InvalidOperationException($"{name} cannot exceed {MaxListItems} items.");
        }

        var values = new List<string>(arr.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in arr)
        {
            var text = Required(item?.GetValue<string>(), name + "[]", MaxListItemLength);
            AssertNoForbiddenClaims(text, name);
            if (!seen.Add(text))
            {
                throw new InvalidOperationException($"{name} must be unique.");
            }

            values.Add(text);
        }

        return values;
    }

    private static void RejectForbiddenOutputFields(JsonNode? node, string path = "$")
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (ForbiddenOutputKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key))
                {
                    throw new InvalidOperationException(
                        $"Forbidden output field '{property.Key}' is not allowed.");
                }

                RejectForbiddenOutputFields(property.Value, path + "." + property.Key);
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                RejectForbiddenOutputFields(arr[i], path + $"[{i}]");
            }
        }
    }

    private static void AssertNoForbiddenClaims(string text, string field)
    {
        var match = ForbiddenClaimRegex.Match(text);
        if (match.Success)
        {
            throw new InvalidOperationException(
                $"Forbidden causal/autonomous claim language '{match.Value}' is not allowed in {field}.");
        }
    }

    private static bool LooksLikeForbiddenAlias(string key) =>
        key.Contains("eventId", StringComparison.OrdinalIgnoreCase)
        || key.Contains("deviceFingerprint", StringComparison.OrdinalIgnoreCase)
        || key.Contains("clickId", StringComparison.OrdinalIgnoreCase)
        || key.Contains("conversionId", StringComparison.OrdinalIgnoreCase)
        || key.Contains("trackingPixel", StringComparison.OrdinalIgnoreCase)
        || key.Contains("externalUrl", StringComparison.OrdinalIgnoreCase);

    private static void AssertOnlyKnownProperties(JsonObject obj, params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var property in obj)
        {
            if (!set.Contains(property.Key))
            {
                throw new InvalidOperationException($"Unknown property '{property.Key}' is not allowed.");
            }
        }
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values) =>
        new(values.Select(v => (JsonNode)v).ToArray());

    private static JsonObject OrderObjectKeys(JsonObject source)
    {
        var ordered = new JsonObject();
        foreach (var property in source.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            ordered[property.Key] = property.Value is null ? null : DeepClone(property.Value);
        }

        return ordered;
    }

    private static JsonNode DeepClone(JsonNode node) =>
        node switch
        {
            JsonObject obj => OrderObjectKeys(obj),
            JsonArray arr => new JsonArray(arr.Select(x => x is null ? null : DeepClone(x)).ToArray()),
            _ => JsonNode.Parse(node.ToJsonString())!
        };

    private static decimal Round(decimal value) =>
        Math.Round(value, MetricScale, MidpointRounding.AwayFromZero);

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

public sealed record CanonicalMeasurementLearningMetrics(
    decimal? Ctr,
    decimal? ConversionRate,
    decimal? Cpm,
    decimal? Cpc,
    decimal? Cpa,
    decimal? Roas,
    string MetricsJson);

public sealed record CanonicalMeasurementLearningBrief(
    Guid CampaignReadinessHandshakeVersionId,
    DateTime ObservationStart,
    DateTime ObservationEnd,
    string SourceLabel,
    string ObservationSourceSystem,
    bool AttestationAcknowledged,
    long Impressions,
    long Clicks,
    long Conversions,
    decimal Spend,
    decimal? Revenue,
    string CurrencyCode,
    string? Notes,
    Guid CampaignPlacementId,
    Guid CampaignPlacementRunId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid QaReviewReportVersionId,
    Guid ApprovedCreativePackageVersionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string HandshakeStatusSnapshot,
    bool HandshakeWasCurrentAtJobStart,
    CanonicalMeasurementLearningMetrics Metrics,
    string InputJson,
    string InputSha256);

public sealed record CanonicalMeasurementLearningRoleContributionPayload(
    string LogicalRole,
    string Summary,
    IReadOnlyList<string> DescriptivePatterns,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> DataGaps,
    IReadOnlyList<string> LearningHypotheses,
    IReadOnlyList<string> RecommendedFutureTests,
    string ContributionJson);

public sealed record CanonicalPerformanceAnalysisStageOutput(
    string WorkerProfileVersion,
    string? Marker,
    CanonicalMeasurementLearningRoleContributionPayload Contribution,
    string StageJson);

public sealed record CanonicalLearningSynthesisStageOutput(
    string WorkerProfileVersion,
    string? Marker,
    CanonicalMeasurementLearningRoleContributionPayload Synthesizer,
    CanonicalMeasurementLearningRoleContributionPayload Advisor,
    string StageJson);

public sealed record CanonicalMeasurementLearningMergedContribution(
    string LogicalRole,
    string ContributionSource,
    Guid ProducingAgentRunId,
    string ContributionJson);

public sealed record CanonicalMeasurementLearningReport(
    string Summary,
    string DocumentJson,
    IReadOnlyList<CanonicalMeasurementLearningMergedContribution> RoleContributions);
