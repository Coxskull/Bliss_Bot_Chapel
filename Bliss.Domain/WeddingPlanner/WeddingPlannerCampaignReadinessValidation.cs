using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Phase 8 campaign-readiness commit/revoke validation and canonical document merge.
/// Zero AI. Rejects forbidden client overrides. Exact disclaimer/marker enforcement.
/// </summary>
public static class WeddingPlannerCampaignReadinessValidation
{
    public const int MaxRationaleLength = 2000;
    public const int MaxSourceSystemLength = 64;
    public const int MaxIdempotencyKeyLength = 128;
    public const string PlacementKeySuffix = ":PLACEMENT";
    public const string MarkKeySuffix = ":MARK";

    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly HashSet<string> ForbiddenCommitKeySet = new(StringComparer.OrdinalIgnoreCase)
    {
        "html", "css", "svg", "script", "src", "url", "href", "base64",
        "imageBytes", "image_bytes", "imageData", "image_data", "pixels", "pixelArray",
        "qaReviewReportVersionId", "qaReportId", "qaAcceptDecisionId", "qaDecisionId",
        "creativePackageVersionId", "approvedCreativePackageVersionId", "packageId",
        "creativePackageDecisionId", "selectedVariantId", "creativeAssetId", "selectedCreativeAssetId",
        "packageDocumentSha256", "documentSha256", "sha256", "hash", "score", "overallScore",
        "opportunityId", "advertiserOpportunityId", "placementId", "campaignPlacementId",
        "campaignPlacementRunId", "runId", "readiness", "campaignReady", "status",
        "handshakeStatus", "decision", "aiProvider", "aiEndpoint", "aiApiKey",
        "providerToolConfiguration", "isAvailable", "reservation", "activate", "activation",
        "legalCleared", "measurementReady", "paymentApproved", "matchScore", "evaluation",
        "overrideSelectedVariantId", "packageOverride", "provenanceOverride"
    };

    private static readonly HashSet<string> AllowedCommitTopLevel = new(StringComparer.OrdinalIgnoreCase)
    {
        "blissMatchId", "campaignId", "contentItemId", "adInventorySlotId",
        "rationale", "disclaimerAcknowledged", "syntheticMarkerAcknowledged",
        "sourceSystem", "idempotencyKey"
    };

    public static void RejectForbiddenCommitFields(JsonNode? node, string path = "$")
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (path == "$" && !AllowedCommitTopLevel.Contains(property.Key))
                {
                    if (ForbiddenCommitKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key))
                    {
                        throw new InvalidOperationException(
                            $"Forbidden commit field '{property.Key}' is not allowed.");
                    }

                    throw new InvalidOperationException(
                        $"Unknown commit field '{property.Key}' is not allowed.");
                }

                if (path != "$"
                    && (ForbiddenCommitKeySet.Contains(property.Key) || LooksLikeForbiddenAlias(property.Key)))
                {
                    throw new InvalidOperationException(
                        $"Forbidden commit field '{property.Key}' is not allowed.");
                }

                RejectForbiddenCommitFields(property.Value, path + "." + property.Key);
            }
        }
        else if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                RejectForbiddenCommitFields(arr[i], path + $"[{i}]");
            }
        }
    }

    public static void RejectForbiddenRevokeFields(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            return;
        }

        foreach (var property in obj)
        {
            if (property.Key.Equals("decision", StringComparison.OrdinalIgnoreCase)
                || property.Key.Equals("rationale", StringComparison.OrdinalIgnoreCase)
                || property.Key.Equals("sourceSystem", StringComparison.OrdinalIgnoreCase)
                || property.Key.Equals("idempotencyKey", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            throw new InvalidOperationException($"Forbidden revoke field '{property.Key}' is not allowed.");
        }
    }

    public static (string SourceSystem, string IdempotencyKey, string PlacementKey, string MarkKey)
        NormalizeCommitKeys(string? sourceSystem, string? idempotencyKey)
    {
        var source = Required(sourceSystem, nameof(sourceSystem), MaxSourceSystemLength).ToUpperInvariant();
        var key = Required(idempotencyKey, nameof(idempotencyKey), MaxIdempotencyKeyLength);
        var placementKey = key + PlacementKeySuffix;
        var markKey = key + MarkKeySuffix;
        if (placementKey.Length > MaxIdempotencyKeyLength || markKey.Length > MaxIdempotencyKeyLength)
        {
            throw new InvalidOperationException(
                $"IdempotencyKey is too long for derived keys; derived keys must be ≤ {MaxIdempotencyKeyLength} characters.");
        }

        return (source, key, placementKey, markKey);
    }

    public static void ValidateCommitAcknowledgements(
        bool? disclaimerAcknowledged,
        string rationale)
    {
        if (disclaimerAcknowledged != true)
        {
            throw new InvalidOperationException("disclaimerAcknowledged must be true.");
        }

        Required(rationale, nameof(rationale), MaxRationaleLength);
    }

    public static void ValidateRevokeDecision(string? decision, string? rationale)
    {
        var normalized = Required(decision, nameof(decision), 64).ToUpperInvariant();
        if (!string.Equals(
                normalized,
                WeddingPlannerCampaignReadinessDecisions.RevokeCampaignReady,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "decision must be REVOKE_CAMPAIGN_READY. MARK_CAMPAIGN_READY is not accepted on the decisions route.");
        }

        Required(rationale, nameof(rationale), MaxRationaleLength);
    }

    /// <summary>Null and false are equivalent for synthetic acknowledgement matching.</summary>
    public static bool SyntheticAckMatches(bool? existing, bool? request) =>
        (existing == true) == (request == true);

    public static string NormalizeRationale(string? rationale) =>
        Required(rationale, nameof(rationale), MaxRationaleLength);

    public static void ThrowIdempotencyConflict(string detail) =>
        throw new InvalidOperationException($"Idempotency conflict: {detail}");

    public static bool DetectSyntheticUpstream(string? creativePackageDocumentJson, string? qaReportDocumentJson)
    {
        var creative = creativePackageDocumentJson ?? string.Empty;
        var qa = qaReportDocumentJson ?? string.Empty;
        return creative.Contains(
                   WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
                   StringComparison.Ordinal)
               || qa.Contains(
                   WeddingPlannerQaMarkers.SyntheticDevelopmentQaReview,
                   StringComparison.Ordinal);
    }

    public static string BuildCanonicalHandshakeDocument(
        CanonicalCampaignReadinessRulesFindings rules,
        Guid qaReportId,
        string qaReviewReportDocumentSha256,
        Guid qaAcceptDecisionId,
        Guid creativePackageId,
        string packageDocumentSha256,
        Guid creativeApproveDecisionId,
        string selectedVariantId,
        Guid selectedAssetId,
        string selectedAssetSha256,
        int selectedAssetByteSize,
        int selectedAssetWidth,
        int selectedAssetHeight,
        Guid conceptPackageId,
        string selectedConceptId,
        Guid brandDnaId,
        int brandDnaVersionNumber,
        Guid colorProfileId,
        int colorProfileVersionNumber,
        Guid researchReportId,
        int researchReportVersionNumber,
        Guid blissMatchId,
        Guid creatorId,
        Guid advertiserOpportunityId,
        Guid ruleVersionId,
        Guid campaignId,
        Guid contentItemId,
        Guid adInventorySlotId,
        Guid campaignPlacementId,
        Guid campaignPlacementRunId,
        string qaStatusSnapshot,
        string packageStatusSnapshot,
        string matchStatusSnapshot,
        decimal? matchOverallScoreSnapshot,
        string opportunityStatusSnapshot,
        string campaignStatusSnapshot,
        string contentTitleSnapshot,
        string? contentTypeSnapshot,
        string slotTypeSnapshot,
        int? slotStartSecondSnapshot,
        int? slotDurationSecondsSnapshot,
        bool? slotIsAvailable,
        string? creatorNameSnapshot,
        string? opportunityNameSnapshot,
        string rationale,
        bool disclaimerAcknowledged,
        bool? syntheticMarkerAcknowledged,
        bool isSynthetic)
    {
        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CampaignReadinessHandshakeV1,
            ["disclaimer"] = WeddingPlannerCampaignReadinessHandshakeDisclaimer.Text,
            ["pins"] = new JsonObject
            {
                ["qaReviewReportVersionId"] = qaReportId.ToString(),
                ["qaReviewReportDocumentSha256"] = qaReviewReportDocumentSha256.ToLowerInvariant(),
                ["qaAcceptDecisionId"] = qaAcceptDecisionId.ToString(),
                ["approvedCreativePackageVersionId"] = creativePackageId.ToString(),
                ["creativePackageDocumentSha256"] = packageDocumentSha256.ToLowerInvariant(),
                ["creativePackageDecisionId"] = creativeApproveDecisionId.ToString(),
                ["selectedVariantId"] = selectedVariantId,
                ["selectedCreativeAssetId"] = selectedAssetId.ToString(),
                ["selectedCreativeAssetSha256"] = selectedAssetSha256.ToLowerInvariant(),
                ["selectedCreativeAssetByteSize"] = selectedAssetByteSize,
                ["selectedCreativeAssetWidth"] = selectedAssetWidth,
                ["selectedCreativeAssetHeight"] = selectedAssetHeight,
                ["approvedConceptPackageVersionId"] = conceptPackageId.ToString(),
                ["selectedConceptId"] = selectedConceptId,
                ["approvedBrandDnaVersionId"] = brandDnaId.ToString(),
                ["approvedBrandDnaVersionNumber"] = brandDnaVersionNumber,
                ["approvedColorProfileVersionId"] = colorProfileId.ToString(),
                ["approvedColorProfileVersionNumber"] = colorProfileVersionNumber,
                ["approvedResearchReportVersionId"] = researchReportId.ToString(),
                ["approvedResearchReportVersionNumber"] = researchReportVersionNumber,
                ["blissMatchId"] = blissMatchId.ToString(),
                ["creatorId"] = creatorId.ToString(),
                ["advertiserOpportunityId"] = advertiserOpportunityId.ToString(),
                ["ruleVersionId"] = ruleVersionId.ToString(),
                ["campaignId"] = campaignId.ToString(),
                ["contentItemId"] = contentItemId.ToString(),
                ["adInventorySlotId"] = adInventorySlotId.ToString()
            },
            ["rules"] = JsonNode.Parse(rules.DocumentJson)!,
            ["snapshots"] = new JsonObject
            {
                ["qaStatus"] = qaStatusSnapshot,
                ["packageStatus"] = packageStatusSnapshot,
                ["matchStatus"] = matchStatusSnapshot,
                ["matchOverallScore"] = matchOverallScoreSnapshot is null
                    ? null
                    : JsonValue.Create(matchOverallScoreSnapshot.Value),
                ["opportunityStatus"] = opportunityStatusSnapshot,
                ["campaignStatus"] = campaignStatusSnapshot,
                ["contentTitle"] = contentTitleSnapshot,
                ["contentType"] = contentTypeSnapshot,
                ["slotType"] = slotTypeSnapshot,
                ["slotStartSecond"] = slotStartSecondSnapshot,
                ["slotDurationSeconds"] = slotDurationSecondsSnapshot,
                ["slotIsAvailableInformational"] = slotIsAvailable,
                ["slotAvailabilityNonAuthoritative"] = true,
                ["creatorName"] = creatorNameSnapshot,
                ["opportunityName"] = opportunityNameSnapshot
            },
            ["campaignPlacementId"] = campaignPlacementId.ToString(),
            ["campaignPlacementRunId"] = campaignPlacementRunId.ToString(),
            ["rationale"] = rationale.Trim(),
            ["disclaimerAcknowledged"] = disclaimerAcknowledged,
            ["syntheticMarkerAcknowledged"] = syntheticMarkerAcknowledged,
            ["disclosures"] = new JsonObject
            {
                ["noReservation"] = WeddingPlannerCampaignReadinessDisclosures.NoReservation,
                ["notActivation"] = WeddingPlannerCampaignReadinessDisclosures.NotActivation,
                ["notLegalMeasurementPayment"] = WeddingPlannerCampaignReadinessDisclosures.NotLegalMeasurementPayment
            }
        };

        if (isSynthetic)
        {
            document["marker"] = WeddingPlannerCampaignReadinessMarkers.SyntheticDevelopmentCampaignReadiness;
        }

        ValidateHandshakeDocument(document, requireSyntheticMarker: isSynthetic);
        return OrderObjectKeys(document).ToJsonString(NodeWriteOptions);
    }

    public static void ValidateHandshakeDocument(JsonNode? node, bool requireSyntheticMarker)
    {
        if (node is not JsonObject document)
        {
            throw new InvalidOperationException("Handshake DocumentJson must be an object.");
        }

        RejectForbiddenDocumentKeys(document);
        RejectUrlsBase64Markup(document);

        if (!string.Equals(
                document["schemaVersion"]?.GetValue<string>(),
                WeddingPlannerSchemaVersions.CampaignReadinessHandshakeV1,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Handshake schemaVersion must be campaign-readiness-handshake.v1.");
        }

        if (!string.Equals(
                document["disclaimer"]?.GetValue<string>(),
                WeddingPlannerCampaignReadinessHandshakeDisclaimer.Text,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Handshake disclaimer must match the locked Phase 8 string.");
        }

        var marker = document["marker"]?.GetValue<string>();
        if (requireSyntheticMarker)
        {
            if (!string.Equals(
                    marker,
                    WeddingPlannerCampaignReadinessMarkers.SyntheticDevelopmentCampaignReadiness,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Synthetic handshake requires SYNTHETIC DEVELOPMENT CAMPAIGN READINESS marker.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(marker))
        {
            throw new InvalidOperationException(
                "Non-synthetic handshake must not claim SYNTHETIC DEVELOPMENT CAMPAIGN READINESS.");
        }

        var pins = document["pins"] as JsonObject
            ?? throw new InvalidOperationException("Handshake pins object is required.");
        foreach (var key in RequiredPinKeys)
        {
            if (pins[key] is null)
            {
                throw new InvalidOperationException($"Handshake pins.{key} is required.");
            }
        }

        if (document["campaignPlacementId"] is null || document["campaignPlacementRunId"] is null)
        {
            throw new InvalidOperationException("Handshake campaignPlacementId and campaignPlacementRunId are required.");
        }

        if (document["disclaimerAcknowledged"] is null)
        {
            throw new InvalidOperationException("Handshake disclaimerAcknowledged is required.");
        }

        var disclosures = document["disclosures"] as JsonObject
            ?? throw new InvalidOperationException("Handshake disclosures object is required.");
        if (disclosures["noReservation"] is null
            || disclosures["notActivation"] is null
            || disclosures["notLegalMeasurementPayment"] is null)
        {
            throw new InvalidOperationException("Handshake disclosures must include planning disclosure strings.");
        }

        var rules = document["rules"] as JsonObject
            ?? throw new InvalidOperationException("Handshake rules object is required.");
        if (!string.Equals(
                rules["schemaVersion"]?.GetValue<string>(),
                WeddingPlannerSchemaVersions.CampaignReadinessRulesV1,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Embedded rules schemaVersion must be campaign-readiness-rules.v1.");
        }

        if (!string.Equals(
                rules["overallSeverity"]?.GetValue<string>(),
                WeddingPlannerCampaignReadinessFindingSeverities.Pass,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Embedded rules overallSeverity must be PASS.");
        }

        var findings = rules["findings"] as JsonArray
            ?? throw new InvalidOperationException("Embedded rules findings are required.");
        if (findings.Count != WeddingPlannerCampaignReadinessRuleCodes.All.Count)
        {
            throw new InvalidOperationException("Embedded rules must include all 16 CR_* codes exactly once.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in findings)
        {
            if (finding is not JsonObject fo)
            {
                throw new InvalidOperationException("Embedded rules findings must be objects.");
            }

            var code = fo["code"]?.GetValue<string>();
            var severity = fo["severity"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(code) || !WeddingPlannerCampaignReadinessRuleCodes.All.Contains(code))
            {
                throw new InvalidOperationException($"Embedded rules contain unknown or missing code '{code}'.");
            }

            if (!seen.Add(code))
            {
                throw new InvalidOperationException($"Embedded rules duplicate code '{code}'.");
            }

            if (!string.Equals(
                    severity,
                    WeddingPlannerCampaignReadinessFindingSeverities.Pass,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Embedded rules code '{code}' must have severity PASS (got '{severity}').");
            }
        }

        foreach (var code in WeddingPlannerCampaignReadinessRuleCodes.All)
        {
            if (!seen.Contains(code))
            {
                throw new InvalidOperationException($"Embedded rules missing code '{code}'.");
            }
        }
    }

    private static readonly string[] RequiredPinKeys =
    [
        "qaReviewReportVersionId",
        "qaReviewReportDocumentSha256",
        "qaAcceptDecisionId",
        "approvedCreativePackageVersionId",
        "creativePackageDocumentSha256",
        "creativePackageDecisionId",
        "selectedVariantId",
        "selectedCreativeAssetId",
        "selectedCreativeAssetSha256",
        "selectedCreativeAssetByteSize",
        "selectedCreativeAssetWidth",
        "selectedCreativeAssetHeight",
        "approvedConceptPackageVersionId",
        "selectedConceptId",
        "approvedBrandDnaVersionId",
        "approvedBrandDnaVersionNumber",
        "approvedColorProfileVersionId",
        "approvedColorProfileVersionNumber",
        "approvedResearchReportVersionId",
        "approvedResearchReportVersionNumber",
        "blissMatchId",
        "creatorId",
        "advertiserOpportunityId",
        "ruleVersionId",
        "campaignId",
        "contentItemId",
        "adInventorySlotId"
    ];

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

    public static string Required(string? value, string name, int maxLength)
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

    private static bool LooksLikeForbiddenAlias(string key)
    {
        var lower = key.ToLowerInvariant();
        return lower.Contains("base64", StringComparison.Ordinal)
               || lower.Contains("markup", StringComparison.Ordinal)
               || lower.Contains("html", StringComparison.Ordinal)
               || lower.Contains("campaignready", StringComparison.Ordinal)
               || lower.Contains("aiProvider", StringComparison.OrdinalIgnoreCase)
               || lower.Contains("isavailable", StringComparison.Ordinal)
               || lower.EndsWith("url", StringComparison.Ordinal)
               || lower.Contains("waiver", StringComparison.Ordinal);
    }

    private static void RejectForbiddenDocumentKeys(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var key = property.Key;
                if (key.Equals("url", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("href", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("base64", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("html", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("css", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("svg", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("script", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("aiProvider", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("legalCleared", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("paymentApproved", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("statusOverride", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Forbidden field '{key}' is not allowed in handshake document.");
                }

                RejectForbiddenDocumentKeys(property.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                RejectForbiddenDocumentKeys(item);
            }
        }
    }

    private static void RejectUrlsBase64Markup(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            var lower = text.ToLowerInvariant();
            if (lower.Contains("http://", StringComparison.Ordinal)
                || lower.Contains("https://", StringComparison.Ordinal)
                || lower.Contains("data:image", StringComparison.Ordinal)
                || lower.Contains("<script", StringComparison.Ordinal)
                || lower.Contains("<svg", StringComparison.Ordinal)
                || lower.Contains("<html", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Handshake document must not contain URLs, base64 media, or markup.");
            }
        }
        else if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                RejectUrlsBase64Markup(property.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                RejectUrlsBase64Markup(item);
            }
        }
    }

    private static JsonObject OrderObjectKeys(JsonObject source)
    {
        var ordered = new JsonObject();
        foreach (var property in source.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            ordered[property.Key] = property.Value is JsonObject child
                ? OrderObjectKeys(child)
                : property.Value?.DeepClone();
        }

        return ordered;
    }
}
