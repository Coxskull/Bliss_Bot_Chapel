using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Bliss.Domain.Entities;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Versioned deterministic qa-rules.v1 engine. Authoritative overall severity is BLOCK &gt; WARN &gt; PASS.
/// AI cannot downgrade. Rules exceptions fail the job pre-AI; BLOCK findings do not fail the job.
/// WARN applicability: QA_COPY_LENGTH_WARN always appears (PASS or WARN); QA_LOCAL_SYNTHETIC_MARKER
/// appears WARN when the synthetic creative marker is present, otherwise omitted.
/// </summary>
public static class WeddingPlannerQaRulesEngine
{
    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static CanonicalQaRulesFindings Evaluate(WeddingPlannerQaRulesContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = new List<CanonicalQaRulesFinding>();

        findings.Add(EvalCurrentPackagePin(context));
        findings.Add(EvalCurrentApproveDecision(context));
        findings.Add(EvalSchemaVersion(context));
        findings.Add(EvalDisclaimerExact(context));
        findings.Add(EvalProvenanceChain(context));
        findings.Add(EvalContributionCount13(context));
        findings.Add(EvalCreativeRuns6(context));
        findings.Add(EvalVariantRefs(context));
        findings.Add(EvalClaimPreservation(context));
        findings.Add(EvalSelectedAssetMeta(context));
        findings.Add(EvalPngRevalidate(context));
        findings.Add(EvalForbiddenMarkupMedia(context));
        findings.Add(EvalCopyNonEmpty(context));
        findings.Add(EvalCopyLengthWarn(context));

        var synthetic = EvalLocalSyntheticMarker(context);
        if (synthetic is not null)
        {
            findings.Add(synthetic);
        }

        if (findings.Count == 0)
        {
            throw new InvalidOperationException("qa-rules.v1 findings array cannot be empty.");
        }

        foreach (var code in WeddingPlannerQaRuleCodes.BlockCodesAlways)
        {
            if (findings.All(f => !string.Equals(f.Code, code, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"qa-rules.v1 missing required BLOCK code '{code}'.");
            }
        }

        var overall = MaxSeverity(findings.Select(f => f.Severity));
        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.QaRulesV1,
            ["overallSeverity"] = overall,
            ["findings"] = new JsonArray(findings.Select(f => (JsonNode)new JsonObject
            {
                ["code"] = f.Code,
                ["severity"] = f.Severity,
                ["message"] = f.Message
            }).ToArray())
        };

        return new CanonicalQaRulesFindings(
            overall,
            findings,
            document.ToJsonString(NodeWriteOptions));
    }

    private static CanonicalQaRulesFinding EvalCurrentPackagePin(WeddingPlannerQaRulesContext context)
    {
        var ok = context.WorkspaceCurrentApprovedCreativePackageVersionId == context.JobApprovedCreativePackageVersionId
                 && context.Package is not null
                 && context.Package.Id == context.JobApprovedCreativePackageVersionId
                 && context.Package.WorkspaceId == context.WorkspaceId;
        return Finding(
            WeddingPlannerQaRuleCodes.CurrentPackagePin,
            ok,
            "Current-approved creative package pin matches job pin.",
            "Current-approved creative package pin does not match job pin or package is missing.");
    }

    private static CanonicalQaRulesFinding EvalCurrentApproveDecision(WeddingPlannerQaRulesContext context)
    {
        var ok = context.LatestApproveDecision is not null
                 && context.LatestApproveDecision.Id == context.JobCreativePackageDecisionId
                 && string.Equals(
                     context.LatestApproveDecision.SelectedVariantId,
                     context.JobSelectedVariantId,
                     StringComparison.Ordinal)
                 && context.LatestApproveDecision.CreativePackageVersionId == context.JobApprovedCreativePackageVersionId;
        return Finding(
            WeddingPlannerQaRuleCodes.CurrentApproveDecision,
            ok,
            "Latest APPROVE decision for pinned package matches job pin.",
            "Latest APPROVE decision is missing or SelectedVariantId does not match job pin.");
    }

    private static CanonicalQaRulesFinding EvalSchemaVersion(WeddingPlannerQaRulesContext context)
    {
        var ok = context.Package is not null
                 && string.Equals(context.Package.SchemaVersion, WeddingPlannerSchemaVersions.CreativePackageV1, StringComparison.Ordinal)
                 && PackageDocumentSchemaIsExact(context.Package.DocumentJson);
        return Finding(
            WeddingPlannerQaRuleCodes.SchemaVersion,
            ok,
            "Package schemaVersion is creative-package.v1.",
            "Package schemaVersion is not exactly creative-package.v1.");
    }

    private static CanonicalQaRulesFinding EvalDisclaimerExact(WeddingPlannerQaRulesContext context)
    {
        var ok = context.Package is not null
                 && PackageDisclaimerIsExact(context.Package.DocumentJson);
        return Finding(
            WeddingPlannerQaRuleCodes.DisclaimerExact,
            ok,
            "Package disclaimer matches Phase 6 locked disclaimer.",
            "Package disclaimer does not equal the Phase 6 locked disclaimer verbatim.");
    }

    private static CanonicalQaRulesFinding EvalProvenanceChain(WeddingPlannerQaRulesContext context)
    {
        var ok = context.Package is not null
                 && !string.IsNullOrWhiteSpace(context.Package.SelectedConceptId)
                 && context.Package.SelectedConceptId == context.JobSelectedConceptId
                 && context.Package.ApprovedBrandDnaVersionId == context.JobApprovedBrandDnaVersionId
                 && context.Package.ApprovedBrandDnaVersionNumber == context.JobApprovedBrandDnaVersionNumber
                 && context.Package.ApprovedColorProfileVersionId == context.JobApprovedColorProfileVersionId
                 && context.Package.ApprovedColorProfileVersionNumber == context.JobApprovedColorProfileVersionNumber
                 && context.Package.ApprovedResearchReportVersionId == context.JobApprovedResearchReportVersionId
                 && context.Package.ApprovedResearchReportVersionNumber == context.JobApprovedResearchReportVersionNumber
                 && PackageProvenanceMatches(context.Package)
                 && context.ProvenancePinsValid;
        return Finding(
            WeddingPlannerQaRuleCodes.ProvenanceChain,
            ok,
            "Package concept + DNA/color/research pins are present and consistent with job pins.",
            "Package provenance pins are missing or inconsistent with job pins.");
    }

    private static CanonicalQaRulesFinding EvalContributionCount13(WeddingPlannerQaRulesContext context)
    {
        var ok = context.CreativeContributionCount == 13;
        return Finding(
            WeddingPlannerQaRuleCodes.ContributionCount13,
            ok,
            "Exactly 13 CreativeRoleContribution rows exist for the package.",
            "Creative package does not have exactly 13 role contributions.");
    }

    private static CanonicalQaRulesFinding EvalCreativeRuns6(WeddingPlannerQaRulesContext context)
    {
        var ok = context.SuccessfulPhase6RunCount == 6
                 && context.Phase6RunIds.Count == 6
                 && context.Phase6RunIds.Distinct().Count() == 6;
        return Finding(
            WeddingPlannerQaRuleCodes.CreativeRuns6,
            ok,
            "Exactly 6 successful Phase 6 agent runs are linked to the producing creative job.",
            "Producing creative job does not have exactly six successful Phase 6 agent-run FKs.");
    }

    private static CanonicalQaRulesFinding EvalVariantRefs(WeddingPlannerQaRulesContext context)
    {
        var ok = false;
        if (context.SelectedVariant is not null
            && string.Equals(context.SelectedVariant.Id, context.JobSelectedVariantId, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(context.SelectedVariant.Format)
            && context.SelectedVariant.Width > 0
            && context.SelectedVariant.Height > 0
            && CanvasMatchesFormat(context.SelectedVariant.Format, context.SelectedVariant.Width, context.SelectedVariant.Height)
            && string.Equals(
                context.SelectedVariant.CopyKind,
                WeddingPlannerCopyKinds.CreativeNonFactual,
                StringComparison.Ordinal)
            && context.SelectedVariant.PaletteRoleRefs.Count > 0
            && context.SelectedVariant.PaletteRoleRefs.All(r =>
                !string.IsNullOrWhiteSpace(r) && context.KnownPaletteRoleNames.Contains(r))
            && context.SelectedVariant.PackageAssetId is Guid assetId
            && assetId != Guid.Empty
            && !string.IsNullOrWhiteSpace(context.SelectedVariant.PackageAssetContentType)
            && !string.IsNullOrWhiteSpace(context.SelectedVariant.PackageAssetSha256)
            && context.SelectedVariant.PackageAssetByteSize is > 0
            && context.SelectedVariant.PackageAssetWidth is > 0
            && context.SelectedVariant.PackageAssetHeight is > 0)
        {
            ok = true;
        }

        return Finding(
            WeddingPlannerQaRuleCodes.VariantRefs,
            ok,
            "Selected variant id exists; refs/canvas/format are consistent with package JSON.",
            "Selected variant refs/canvas/format are missing or inconsistent.");
    }

    private static CanonicalQaRulesFinding EvalClaimPreservation(WeddingPlannerQaRulesContext context)
    {
        if (context.SelectedVariant is null)
        {
            return Finding(
                WeddingPlannerQaRuleCodes.ClaimPreservation,
                false,
                "Selected-variant factualClaims preserve exact statement + source IDs.",
                "Selected variant or concept snapshot is missing for claim preservation.");
        }

        // Empty claims are vacuously preserved.
        if (context.SelectedVariant.FactualClaims.Count == 0)
        {
            return Finding(
                WeddingPlannerQaRuleCodes.ClaimPreservation,
                true,
                "Selected-variant factualClaims preserve exact statement + source IDs from pinned concept.",
                "Selected-variant factualClaims do not preserve exact statement + source IDs from pinned concept.");
        }

        if (context.SelectedConcept is null)
        {
            return Finding(
                WeddingPlannerQaRuleCodes.ClaimPreservation,
                false,
                "Selected-variant factualClaims preserve exact statement + source IDs.",
                "Selected variant or concept snapshot is missing for claim preservation.");
        }

        var ok = true;
        foreach (var claim in context.SelectedVariant.FactualClaims)
        {
            if (!context.SelectedConcept.FactualClaims.Any(c =>
                    string.Equals(c.Statement, claim.Statement, StringComparison.Ordinal)
                    && SourceIdSetsEquivalent(c.SourceIds, claim.SourceIds)))
            {
                ok = false;
                break;
            }
        }

        return Finding(
            WeddingPlannerQaRuleCodes.ClaimPreservation,
            ok,
            "Selected-variant factualClaims preserve exact statement + source IDs from pinned concept.",
            "Selected-variant factualClaims do not preserve exact statement + source IDs from pinned concept.");
    }

    /// <summary>Source ID lists are set-equivalent (same members; order and duplicate multiplicity ignored).</summary>
    public static bool SourceIdSetsEquivalent(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        var leftSet = left.ToHashSet(StringComparer.Ordinal);
        var rightSet = right.ToHashSet(StringComparer.Ordinal);
        return leftSet.SetEquals(rightSet);
    }

    private static CanonicalQaRulesFinding EvalSelectedAssetMeta(WeddingPlannerQaRulesContext context)
    {
        var ok = context.SelectedAsset is not null
                 && context.SelectedVariant is not null
                 && context.SelectedAsset.Id == context.JobSelectedCreativeAssetId
                 && string.Equals(context.SelectedAsset.Sha256, context.JobSelectedCreativeAssetSha256, StringComparison.OrdinalIgnoreCase)
                 && string.Equals(context.SelectedAsset.ContentType, context.JobSelectedCreativeAssetContentType, StringComparison.Ordinal)
                 && context.SelectedAsset.ByteSize == context.JobSelectedCreativeAssetByteSize
                 && context.SelectedAsset.Width == context.JobSelectedCreativeAssetWidth
                 && context.SelectedAsset.Height == context.JobSelectedCreativeAssetHeight
                 && context.SelectedVariant.PackageAssetId == context.SelectedAsset.Id
                 && string.Equals(context.SelectedVariant.PackageAssetSha256, context.SelectedAsset.Sha256, StringComparison.OrdinalIgnoreCase)
                 && context.SelectedVariant.PackageAssetByteSize == context.SelectedAsset.ByteSize
                 && context.SelectedVariant.PackageAssetWidth == context.SelectedAsset.Width
                 && context.SelectedVariant.PackageAssetHeight == context.SelectedAsset.Height
                 && string.Equals(context.SelectedVariant.PackageAssetContentType, context.SelectedAsset.ContentType, StringComparison.Ordinal)
                 && context.AssetsForSelectedVariantCount == 1;
        return Finding(
            WeddingPlannerQaRuleCodes.SelectedAssetMeta,
            ok,
            "Exactly one selected asset; contentType/size/dims/sha meta match package asset refs.",
            "Selected asset meta does not match package refs or asset count is not exactly one.");
    }

    private static CanonicalQaRulesFinding EvalPngRevalidate(WeddingPlannerQaRulesContext context)
    {
        if (context.SelectedAsset is null)
        {
            return Finding(
                WeddingPlannerQaRuleCodes.PngRevalidate,
                false,
                "Stored PNG bytes revalidated against meta.",
                "Selected asset missing for PNG revalidation.");
        }

        try
        {
            var validated = WeddingPlannerPngValidator.ValidateExactCanvas(
                context.SelectedAsset.Bytes,
                context.SelectedAsset.Width,
                context.SelectedAsset.Height);
            var recomputedSha = WeddingPlannerQaReviewValidation.Sha256Hex(context.SelectedAsset.Bytes);
            var ok = validated.ByteSize == context.SelectedAsset.ByteSize
                     && validated.Width == context.SelectedAsset.Width
                     && validated.Height == context.SelectedAsset.Height
                     && string.Equals(recomputedSha, context.SelectedAsset.Sha256, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(recomputedSha, context.JobSelectedCreativeAssetSha256, StringComparison.OrdinalIgnoreCase)
                     && validated.ByteSize == context.JobSelectedCreativeAssetByteSize;
            return Finding(
                WeddingPlannerQaRuleCodes.PngRevalidate,
                ok,
                "Stored PNG bytes revalidated; hash/size/dims match stored + package refs.",
                "Stored PNG revalidation failed or hash/size/dims diverge from stored + package refs.");
        }
        catch (Exception)
        {
            return Finding(
                WeddingPlannerQaRuleCodes.PngRevalidate,
                false,
                "Stored PNG bytes revalidated; hash/size/dims match stored + package refs.",
                "Stored PNG revalidation failed or hash/size/dims diverge from stored + package refs.");
        }
    }

    private static CanonicalQaRulesFinding EvalForbiddenMarkupMedia(WeddingPlannerQaRulesContext context)
    {
        var ok = true;
        try
        {
            if (context.Package is not null)
            {
                RejectForbiddenMarkup(JsonNode.Parse(context.Package.DocumentJson));
            }
            else
            {
                ok = false;
            }
        }
        catch (Exception)
        {
            ok = false;
        }

        return Finding(
            WeddingPlannerQaRuleCodes.ForbiddenMarkupMedia,
            ok,
            "Package/selected-variant structured text contains no forbidden markup/media keys.",
            "Package/selected-variant structured text contains forbidden markup/media keys or URLs/base64.");
    }

    private static CanonicalQaRulesFinding EvalCopyNonEmpty(WeddingPlannerQaRulesContext context)
    {
        var ok = context.SelectedVariant is not null
                 && !string.IsNullOrWhiteSpace(context.SelectedVariant.Headline)
                 && !string.IsNullOrWhiteSpace(context.SelectedVariant.Body)
                 && !string.IsNullOrWhiteSpace(context.SelectedVariant.Cta);
        return Finding(
            WeddingPlannerQaRuleCodes.CopyNonEmpty,
            ok,
            "Selected-variant headline/body/cta are non-empty trimmed.",
            "Selected-variant headline/body/cta must be non-empty trimmed.");
    }

    private static CanonicalQaRulesFinding EvalCopyLengthWarn(WeddingPlannerQaRulesContext context)
    {
        if (context.SelectedVariant is null)
        {
            return new CanonicalQaRulesFinding(
                WeddingPlannerQaRuleCodes.CopyLengthWarn,
                WeddingPlannerQaFindingSeverities.Warn,
                "Selected-variant copy length could not be evaluated.");
        }

        var over =
            context.SelectedVariant.Headline.Length > WeddingPlannerQaReviewValidation.HeadlineWarnThreshold
            || context.SelectedVariant.Body.Length > WeddingPlannerQaReviewValidation.BodyWarnThreshold
            || context.SelectedVariant.Cta.Length > WeddingPlannerQaReviewValidation.CtaWarnThreshold;
        return new CanonicalQaRulesFinding(
            WeddingPlannerQaRuleCodes.CopyLengthWarn,
            over ? WeddingPlannerQaFindingSeverities.Warn : WeddingPlannerQaFindingSeverities.Pass,
            over
                ? "Selected-variant headline/body/cta exceeds warn thresholds (120/800/40)."
                : "Selected-variant headline/body/cta within warn thresholds.");
    }

    private static CanonicalQaRulesFinding? EvalLocalSyntheticMarker(WeddingPlannerQaRulesContext context)
    {
        if (context.Package is null)
        {
            return null;
        }

        var present = context.Package.DocumentJson.Contains(
            WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage,
            StringComparison.Ordinal);
        if (!present)
        {
            // Applicability gate false — omit WARN code.
            return null;
        }

        return new CanonicalQaRulesFinding(
            WeddingPlannerQaRuleCodes.LocalSyntheticMarker,
            WeddingPlannerQaFindingSeverities.Warn,
            "Local/synthetic creative package marker is present.");
    }

    private static CanonicalQaRulesFinding Finding(string code, bool ok, string passMessage, string blockMessage) =>
        new(
            code,
            ok ? WeddingPlannerQaFindingSeverities.Pass : WeddingPlannerQaFindingSeverities.Block,
            ok ? passMessage : blockMessage);

    private static string MaxSeverity(IEnumerable<string> severities)
    {
        var max = WeddingPlannerQaFindingSeverities.Pass;
        foreach (var severity in severities)
        {
            if (string.Equals(severity, WeddingPlannerQaFindingSeverities.Block, StringComparison.Ordinal))
            {
                return WeddingPlannerQaFindingSeverities.Block;
            }

            if (string.Equals(severity, WeddingPlannerQaFindingSeverities.Warn, StringComparison.Ordinal))
            {
                max = WeddingPlannerQaFindingSeverities.Warn;
            }
        }

        return max;
    }

    private static bool PackageDocumentSchemaIsExact(string documentJson)
    {
        try
        {
            var root = JsonNode.Parse(documentJson) as JsonObject;
            return string.Equals(
                root?["schemaVersion"]?.GetValue<string>(),
                WeddingPlannerSchemaVersions.CreativePackageV1,
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool PackageDisclaimerIsExact(string documentJson)
    {
        try
        {
            var root = JsonNode.Parse(documentJson) as JsonObject;
            return string.Equals(
                root?["disclaimer"]?.GetValue<string>(),
                WeddingPlannerCreativePackageDisclaimer.Text,
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static bool PackageProvenanceMatches(WeddingPlannerCreativePackageVersion package)
    {
        try
        {
            var root = JsonNode.Parse(package.DocumentJson) as JsonObject;
            var provenance = root?["provenance"] as JsonObject;
            if (provenance is null)
            {
                return false;
            }

            return string.Equals(provenance["selectedConceptId"]?.GetValue<string>(), package.SelectedConceptId, StringComparison.Ordinal)
                   && string.Equals(provenance["approvedBrandDnaVersionId"]?.GetValue<string>(), package.ApprovedBrandDnaVersionId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                   && provenance["approvedBrandDnaVersionNumber"]?.GetValue<int>() == package.ApprovedBrandDnaVersionNumber
                   && string.Equals(provenance["approvedColorProfileVersionId"]?.GetValue<string>(), package.ApprovedColorProfileVersionId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                   && provenance["approvedColorProfileVersionNumber"]?.GetValue<int>() == package.ApprovedColorProfileVersionNumber
                   && string.Equals(provenance["approvedResearchReportVersionId"]?.GetValue<string>(), package.ApprovedResearchReportVersionId.ToString("D"), StringComparison.OrdinalIgnoreCase)
                   && provenance["approvedResearchReportVersionNumber"]?.GetValue<int>() == package.ApprovedResearchReportVersionNumber;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanvasMatchesFormat(string format, int width, int height)
    {
        try
        {
            var expected = WeddingPlannerCreativeDepartmentValidation.CanvasForFormat(format);
            return expected.Width == width && expected.Height == height;
        }
        catch
        {
            return false;
        }
    }

    private static void RejectForbiddenMarkup(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var key = property.Key;
                var k = key.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
                if (k is "html" or "css" or "svg" or "script" or "src" or "url" or "href" or "base64"
                    || k.Contains("imagebytes", StringComparison.Ordinal)
                    || k.Contains("base64", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Forbidden markup/media key '{key}'.");
                }

                if (property.Value is JsonValue value
                    && value.TryGetValue<string>(out var text)
                    && (text.Contains("data:image", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("http://", StringComparison.OrdinalIgnoreCase)
                        || text.Contains("https://", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Forbidden URL/base64 content in package text.");
                }

                RejectForbiddenMarkup(property.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                RejectForbiddenMarkup(item);
            }
        }
    }

    public static SelectedVariantQaSnapshot? ExtractSelectedVariant(string packageDocumentJson, string selectedVariantId)
    {
        try
        {
            var root = JsonNode.Parse(packageDocumentJson) as JsonObject;
            if (root?["variants"] is not JsonArray variants)
            {
                return null;
            }

            foreach (var item in variants)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var id = obj["id"]?.GetValue<string>();
                if (!string.Equals(id, selectedVariantId, StringComparison.Ordinal))
                {
                    continue;
                }

                var format = obj["format"]?.GetValue<string>() ?? string.Empty;
                var canvas = obj["canvas"] as JsonObject;
                var width = canvas?["width"]?.GetValue<int>() ?? 0;
                var height = canvas?["height"]?.GetValue<int>() ?? 0;
                var copy = obj["copy"] as JsonObject;
                var kind = copy?["kind"]?.GetValue<string>() ?? string.Empty;
                var headline = copy?["headline"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var body = copy?["body"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var cta = copy?["cta"]?.GetValue<string>()?.Trim() ?? string.Empty;
                var claims = new List<CanonicalCreativeFactualClaim>();
                if (obj["factualClaims"] is JsonArray claimsArr)
                {
                    foreach (var claimNode in claimsArr)
                    {
                        if (claimNode is not JsonObject claimObj)
                        {
                            continue;
                        }

                        var statement = claimObj["statement"]?.GetValue<string>() ?? string.Empty;
                        var sourceIds = new List<string>();
                        if (claimObj["sourceIds"] is JsonArray sourceArr)
                        {
                            foreach (var s in sourceArr)
                            {
                                var sid = s?.GetValue<string>();
                                if (!string.IsNullOrWhiteSpace(sid))
                                {
                                    sourceIds.Add(sid);
                                }
                            }
                        }

                        claims.Add(new CanonicalCreativeFactualClaim(statement, sourceIds));
                    }
                }

                var palette = new List<string>();
                if (obj["paletteRoleRefs"] is JsonArray paletteArr)
                {
                    foreach (var p in paletteArr)
                    {
                        var name = p?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            palette.Add(name);
                        }
                    }
                }

                Guid? assetId = null;
                string? contentType = null;
                int? byteSize = null;
                string? sha = null;
                int? assetWidth = null;
                int? assetHeight = null;
                if (obj["asset"] is JsonObject asset)
                {
                    if (Guid.TryParse(asset["creativeAssetId"]?.GetValue<string>(), out var parsed))
                    {
                        assetId = parsed;
                    }

                    contentType = asset["contentType"]?.GetValue<string>();
                    byteSize = asset["byteSize"]?.GetValue<int>();
                    sha = asset["sha256"]?.GetValue<string>();
                    assetWidth = asset["width"]?.GetValue<int>();
                    assetHeight = asset["height"]?.GetValue<int>();
                }

                return new SelectedVariantQaSnapshot(
                    id!,
                    format,
                    width,
                    height,
                    kind,
                    headline,
                    body,
                    cta,
                    claims,
                    palette,
                    assetId,
                    contentType,
                    byteSize,
                    sha,
                    assetWidth,
                    assetHeight);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}

public sealed record WeddingPlannerQaRulesContext(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? WorkspaceCurrentApprovedCreativePackageVersionId,
    Guid JobApprovedCreativePackageVersionId,
    Guid JobCreativePackageDecisionId,
    string JobSelectedVariantId,
    Guid JobSelectedCreativeAssetId,
    string JobSelectedCreativeAssetSha256,
    string JobSelectedCreativeAssetContentType,
    int JobSelectedCreativeAssetByteSize,
    int JobSelectedCreativeAssetWidth,
    int JobSelectedCreativeAssetHeight,
    string JobSelectedConceptId,
    Guid JobApprovedBrandDnaVersionId,
    int JobApprovedBrandDnaVersionNumber,
    Guid JobApprovedColorProfileVersionId,
    int JobApprovedColorProfileVersionNumber,
    Guid JobApprovedResearchReportVersionId,
    int JobApprovedResearchReportVersionNumber,
    WeddingPlannerCreativePackageVersion? Package,
    WeddingPlannerCreativePackageDecision? LatestApproveDecision,
    WeddingPlannerCreativeAsset? SelectedAsset,
    int AssetsForSelectedVariantCount,
    int CreativeContributionCount,
    int SuccessfulPhase6RunCount,
    IReadOnlyList<Guid> Phase6RunIds,
    SelectedVariantQaSnapshot? SelectedVariant,
    SelectedConceptSnapshot? SelectedConcept,
    bool ProvenancePinsValid,
    IReadOnlySet<string> KnownPaletteRoleNames);
