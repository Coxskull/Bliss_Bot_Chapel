using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Deterministic campaign-readiness-rules.v1. Severity model: PASS | BLOCK only — no WARN.
/// Overall = BLOCK if any finding is BLOCK; else PASS. All 16 CR_* codes always appear.
/// </summary>
public static class WeddingPlannerCampaignReadinessRulesEngine
{
    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        WriteIndented = false,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static CanonicalCampaignReadinessRulesFindings Evaluate(WeddingPlannerCampaignReadinessRulesContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = new List<CanonicalCampaignReadinessFinding>
        {
            EvalCurrentQaPointer(context),
            EvalQaCleanAccepted(context),
            EvalQaAcceptDecision(context),
            EvalPackageCurrentApproved(context),
            EvalPackageDocumentSha(context),
            EvalCreativeDecisionVariant(context),
            EvalAssetIntegrity(context),
            EvalProvenanceChain(context),
            EvalMatchApproved(context),
            EvalOpportunityActive(context),
            EvalAdvertiserScope(context),
            EvalCampaignDraft(context),
            EvalCampaignOpportunity(context),
            EvalContentCreator(context),
            EvalSlotContent(context),
            EvalSyntheticEnvironment(context)
        };

        if (findings.Count == 0)
        {
            throw new InvalidOperationException("campaign-readiness-rules.v1 findings array cannot be empty.");
        }

        foreach (var code in WeddingPlannerCampaignReadinessRuleCodes.All)
        {
            if (findings.All(f => !string.Equals(f.Code, code, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"campaign-readiness-rules.v1 missing required code '{code}'.");
            }
        }

        if (findings.Any(f => string.Equals(f.Severity, WeddingPlannerQaFindingSeverities.Warn, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("campaign-readiness-rules.v1 must not emit WARN severity.");
        }

        var overall = findings.Any(f =>
            string.Equals(f.Severity, WeddingPlannerCampaignReadinessFindingSeverities.Block, StringComparison.Ordinal))
            ? WeddingPlannerCampaignReadinessFindingSeverities.Block
            : WeddingPlannerCampaignReadinessFindingSeverities.Pass;

        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.CampaignReadinessRulesV1,
            ["overallSeverity"] = overall,
            ["findings"] = new JsonArray(findings.Select(f => (JsonNode)new JsonObject
            {
                ["code"] = f.Code,
                ["severity"] = f.Severity,
                ["message"] = f.Message
            }).ToArray())
        };

        return new CanonicalCampaignReadinessRulesFindings(
            overall,
            findings,
            document.ToJsonString(NodeWriteOptions));
    }

    private static CanonicalCampaignReadinessFinding EvalCurrentQaPointer(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.WorkspaceCurrentAcceptedQaReviewReportVersionId is Guid pointer
                 && pointer != Guid.Empty
                 && context.QaReport is not null
                 && context.QaReport.Id == pointer
                 && context.QaReport.WorkspaceId == context.WorkspaceId
                 && context.QaReport.AdvertiserId == context.WorkspaceAdvertiserId;
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.CurrentQaPointer,
            ok,
            "Current accepted QA pointer is present and scoped to the workspace.",
            "Current accepted QA pointer is missing or not scoped to the workspace.");
    }

    private static CanonicalCampaignReadinessFinding EvalQaCleanAccepted(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.QaReport is not null
                 && string.Equals(
                     context.QaReport.Status,
                     WeddingPlannerQaReviewReportStatuses.Accepted,
                     StringComparison.Ordinal);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.QaCleanAccepted,
            ok,
            "Current QA report Status is exactly ACCEPTED.",
            "Current QA report is missing or Status is not exactly ACCEPTED (ACCEPTED_WITH_EXCEPTION is ineligible).");
    }

    private static CanonicalCampaignReadinessFinding EvalQaAcceptDecision(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.LatestQaDecision is not null
                 && string.Equals(
                     context.LatestQaDecision.Decision,
                     WeddingPlannerQaReviewDecisions.Accept,
                     StringComparison.Ordinal)
                 && context.LatestQaDecision.QaReviewReportVersionId == context.QaReport?.Id;
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.QaAcceptDecision,
            ok,
            "Latest human decision for the current QA report is exactly ACCEPT.",
            "Latest QA decision is missing or is not a clean ACCEPT (waive path is ineligible).");
    }

    private static CanonicalCampaignReadinessFinding EvalPackageCurrentApproved(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.WorkspaceCurrentApprovedCreativePackageVersionId is Guid packageId
                 && packageId != Guid.Empty
                 && context.QaReport is not null
                 && packageId == context.QaReport.ApprovedCreativePackageVersionId
                 && context.Package is not null
                 && context.Package.Id == packageId
                 && context.Package.WorkspaceId == context.WorkspaceId
                 && context.Package.AdvertiserId == context.WorkspaceAdvertiserId
                 && string.Equals(
                     context.Package.Status,
                     WeddingPlannerCreativePackageStatuses.Approved,
                     StringComparison.Ordinal);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.PackageCurrentApproved,
            ok,
            "Current approved creative package equals QA package pin and is APPROVED.",
            "Current approved creative package is missing, not APPROVED, or does not equal QA package pin.");
    }

    private static CanonicalCampaignReadinessFinding EvalPackageDocumentSha(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Package is not null
                 && context.QaReport is not null
                 && !string.IsNullOrWhiteSpace(context.RecomputedPackageDocumentSha256)
                 && string.Equals(
                     context.RecomputedPackageDocumentSha256,
                     context.QaReport.CreativePackageDocumentSha256,
                     StringComparison.OrdinalIgnoreCase)
                 && string.Equals(
                     context.RecomputedPackageDocumentSha256,
                     WeddingPlannerCampaignReadinessValidation.Sha256Hex(context.Package.DocumentJson),
                     StringComparison.OrdinalIgnoreCase);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.PackageDocumentSha,
            ok,
            "Recomputed package DocumentJson SHA-256 matches QA and package integrity pins.",
            "Package DocumentJson SHA-256 does not match QA pin or recomputed package hash.");
    }

    private static CanonicalCampaignReadinessFinding EvalCreativeDecisionVariant(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.LatestCreativeApproveDecision is not null
                 && context.Package is not null
                 && context.QaReport is not null
                 && context.Package.WorkspaceId == context.WorkspaceId
                 && context.Package.AdvertiserId == context.WorkspaceAdvertiserId
                 && context.QaReport.WorkspaceId == context.WorkspaceId
                 && context.QaReport.AdvertiserId == context.WorkspaceAdvertiserId
                 && context.LatestCreativeApproveDecision.CreativePackageVersionId == context.Package.Id
                 && context.LatestCreativeApproveDecision.Id == context.QaReport.CreativePackageDecisionId
                 && string.Equals(
                     context.LatestCreativeApproveDecision.Decision,
                     WeddingPlannerCreativePackageDecisions.Approve,
                     StringComparison.Ordinal)
                 && !string.IsNullOrWhiteSpace(context.LatestCreativeApproveDecision.SelectedVariantId)
                 && string.Equals(
                     context.LatestCreativeApproveDecision.SelectedVariantId,
                     context.QaReport.SelectedVariantId,
                     StringComparison.Ordinal)
                 && context.SelectedVariantSnapshot is not null
                 && string.Equals(
                     context.SelectedVariantSnapshot.Id,
                     context.QaReport.SelectedVariantId,
                     StringComparison.Ordinal);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.CreativeDecisionVariant,
            ok,
            "Latest creative APPROVE decision id matches QA pin with SelectedVariantId present in package.",
            "Latest creative APPROVE decision id/variant is missing or inconsistent with QA package pins.");
    }

    private static CanonicalCampaignReadinessFinding EvalAssetIntegrity(WeddingPlannerCampaignReadinessRulesContext context)
    {
        if (context.SelectedAsset is null
            || context.SelectedVariantSnapshot is null
            || context.QaReport is null
            || context.Package is null
            || context.AssetsForSelectedVariantCount != 1)
        {
            return Finding(
                WeddingPlannerCampaignReadinessRuleCodes.AssetIntegrity,
                false,
                "Exactly one selected PNG asset revalidates against stored bytes and hash/meta pins.",
                "Selected asset/variant snapshot missing or asset count for variant is not exactly one.");
        }

        try
        {
            var asset = context.SelectedAsset;
            var variant = context.SelectedVariantSnapshot;
            var qa = context.QaReport;
            var validated = WeddingPlannerPngValidator.ValidateExactCanvas(
                asset.Bytes,
                asset.Width,
                asset.Height);
            var recomputedSha = WeddingPlannerCampaignReadinessValidation.Sha256Hex(asset.Bytes);
            var ok = asset.Id == qa.SelectedCreativeAssetId
                     && asset.CreativePackageVersionId == context.Package.Id
                     && string.Equals(asset.VariantId, qa.SelectedVariantId, StringComparison.Ordinal)
                     && variant.PackageAssetId == asset.Id
                     && string.Equals(variant.PackageAssetContentType, asset.ContentType, StringComparison.Ordinal)
                     && string.Equals(variant.PackageAssetSha256, asset.Sha256, StringComparison.OrdinalIgnoreCase)
                     && variant.PackageAssetByteSize == asset.ByteSize
                     && variant.PackageAssetWidth == asset.Width
                     && variant.PackageAssetHeight == asset.Height
                     && string.Equals(recomputedSha, asset.Sha256, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(recomputedSha, qa.SelectedCreativeAssetSha256, StringComparison.OrdinalIgnoreCase)
                     && validated.ByteSize == asset.ByteSize
                     && validated.Width == asset.Width
                     && validated.Height == asset.Height
                     && string.Equals(asset.ContentType, "image/png", StringComparison.Ordinal);
            return Finding(
                WeddingPlannerCampaignReadinessRuleCodes.AssetIntegrity,
                ok,
                "Exactly one selected PNG asset revalidates against stored bytes and hash/meta pins.",
                "Selected PNG revalidation failed or hash/size/dims diverge from pins.");
        }
        catch (Exception)
        {
            return Finding(
                WeddingPlannerCampaignReadinessRuleCodes.AssetIntegrity,
                false,
                "Exactly one selected PNG asset revalidates against stored bytes and hash/meta pins.",
                "Selected PNG revalidation failed or hash/size/dims diverge from pins.");
        }
    }

    private static CanonicalCampaignReadinessFinding EvalProvenanceChain(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Package is not null
                 && context.QaReport is not null
                 && context.ConceptPackage is not null
                 && context.BrandDna is not null
                 && context.ColorProfile is not null
                 && context.ResearchReport is not null
                 && context.Package.WorkspaceId == context.WorkspaceId
                 && context.Package.AdvertiserId == context.WorkspaceAdvertiserId
                 && context.QaReport.WorkspaceId == context.WorkspaceId
                 && context.QaReport.AdvertiserId == context.WorkspaceAdvertiserId
                 && !string.IsNullOrWhiteSpace(context.Package.SelectedConceptId)
                 && string.Equals(
                     context.Package.SelectedConceptId,
                     context.QaReport.SelectedConceptId,
                     StringComparison.Ordinal)
                 && context.SelectedConceptPresentInConceptPackage
                 && context.Package.ApprovedConceptPackageVersionId == context.ConceptPackage.Id
                 && context.ConceptPackage.WorkspaceId == context.WorkspaceId
                 && context.ConceptPackage.AdvertiserId == context.WorkspaceAdvertiserId
                 && string.Equals(
                     context.ConceptPackage.Status,
                     WeddingPlannerConceptPackageStatuses.Approved,
                     StringComparison.Ordinal)
                 && context.Package.ApprovedBrandDnaVersionId == context.QaReport.ApprovedBrandDnaVersionId
                 && context.Package.ApprovedBrandDnaVersionNumber == context.QaReport.ApprovedBrandDnaVersionNumber
                 && context.BrandDna.Id == context.Package.ApprovedBrandDnaVersionId
                 && context.BrandDna.VersionNumber == context.Package.ApprovedBrandDnaVersionNumber
                 && context.BrandDna.WorkspaceId == context.WorkspaceId
                 && context.BrandDna.AdvertiserId == context.WorkspaceAdvertiserId
                 && string.Equals(context.BrandDna.Status, WeddingPlannerBrandDnaStatuses.Approved, StringComparison.Ordinal)
                 && context.Package.ApprovedColorProfileVersionId == context.QaReport.ApprovedColorProfileVersionId
                 && context.Package.ApprovedColorProfileVersionNumber == context.QaReport.ApprovedColorProfileVersionNumber
                 && context.ColorProfile.Id == context.Package.ApprovedColorProfileVersionId
                 && context.ColorProfile.VersionNumber == context.Package.ApprovedColorProfileVersionNumber
                 && context.ColorProfile.WorkspaceId == context.WorkspaceId
                 && context.ColorProfile.AdvertiserId == context.WorkspaceAdvertiserId
                 && string.Equals(
                     context.ColorProfile.Status,
                     WeddingPlannerColorProfileStatuses.Approved,
                     StringComparison.Ordinal)
                 && context.Package.ApprovedResearchReportVersionId == context.QaReport.ApprovedResearchReportVersionId
                 && context.Package.ApprovedResearchReportVersionNumber == context.QaReport.ApprovedResearchReportVersionNumber
                 && context.ResearchReport.Id == context.Package.ApprovedResearchReportVersionId
                 && context.ResearchReport.VersionNumber == context.Package.ApprovedResearchReportVersionNumber
                 && context.ResearchReport.WorkspaceId == context.WorkspaceId
                 && context.ResearchReport.AdvertiserId == context.WorkspaceAdvertiserId
                 && string.Equals(
                     context.ResearchReport.Status,
                     WeddingPlannerResearchReportStatuses.Approved,
                     StringComparison.Ordinal);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.ProvenanceChain,
            ok,
            "Concept package + selected concept + DNA/color/research lineage pins are present and consistent.",
            "Provenance lineage rows are missing, out of scope, or inconsistent with creative package pins.");
    }

    private static CanonicalCampaignReadinessFinding EvalMatchApproved(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Match is not null
                 && string.Equals(context.Match.Status, EntityStatuses.Approved, StringComparison.Ordinal);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.MatchApproved,
            ok,
            "Selected BlissMatch exists and Status is APPROVED.",
            "Selected BlissMatch is missing or Status is not APPROVED.");
    }

    private static CanonicalCampaignReadinessFinding EvalOpportunityActive(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Opportunity is not null
                 && string.Equals(context.Opportunity.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.OpportunityActive,
            ok,
            "Match opportunity exists and Status is ACTIVE.",
            "Match opportunity is missing or Status is not ACTIVE.");
    }

    private static CanonicalCampaignReadinessFinding EvalAdvertiserScope(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.OpportunityProgramAdvertiserId is Guid programAdvertiserId
                 && programAdvertiserId == context.WorkspaceAdvertiserId;
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.AdvertiserScope,
            ok,
            "Opportunity program AdvertiserId equals workspace AdvertiserId.",
            "Opportunity program AdvertiserId does not equal workspace AdvertiserId.");
    }

    private static CanonicalCampaignReadinessFinding EvalCampaignDraft(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Campaign is not null
                 && string.Equals(context.Campaign.Status, EntityStatuses.Draft, StringComparison.Ordinal);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.CampaignDraft,
            ok,
            "Selected campaign exists and Status is DRAFT.",
            "Selected campaign is missing or Status is not DRAFT.");
    }

    private static CanonicalCampaignReadinessFinding EvalCampaignOpportunity(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Campaign is not null
                 && context.Match is not null
                 && (context.Campaign.AdvertiserOpportunityId is null
                     || context.Campaign.AdvertiserOpportunityId == context.Match.AdvertiserOpportunityId);
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.CampaignOpportunity,
            ok,
            "Campaign opportunity is null or equals the match opportunity.",
            "Campaign opportunity does not match the approved match opportunity.");
    }

    private static CanonicalCampaignReadinessFinding EvalContentCreator(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Content is not null
                 && context.Match is not null
                 && context.Content.CreatorId == context.Match.CreatorId;
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.ContentCreator,
            ok,
            "Content exists and CreatorId equals match CreatorId.",
            "Content is missing or CreatorId does not equal match CreatorId.");
    }

    private static CanonicalCampaignReadinessFinding EvalSlotContent(WeddingPlannerCampaignReadinessRulesContext context)
    {
        var ok = context.Slot is not null
                 && context.Content is not null
                 && context.Slot.ContentItemId == context.Content.Id;
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.SlotContent,
            ok,
            "Slot exists and ContentItemId equals selected content.",
            "Slot is missing or ContentItemId does not equal selected content.");
    }

    private static CanonicalCampaignReadinessFinding EvalSyntheticEnvironment(WeddingPlannerCampaignReadinessRulesContext context)
    {
        if (!context.HasSyntheticUpstream)
        {
            return Finding(
                WeddingPlannerCampaignReadinessRuleCodes.SyntheticEnvironment,
                true,
                "Non-synthetic upstream path; synthetic acknowledgement is not required.",
                "Synthetic upstream policy failed.");
        }

        if (!context.IsDevelopmentHost)
        {
            return Finding(
                WeddingPlannerCampaignReadinessRuleCodes.SyntheticEnvironment,
                false,
                "Development host allows synthetic upstream with acknowledgement.",
                "Synthetic upstream artifacts are forbidden outside Development.");
        }

        var ok = context.SyntheticMarkerAcknowledged == true;
        return Finding(
            WeddingPlannerCampaignReadinessRuleCodes.SyntheticEnvironment,
            ok,
            "Synthetic upstream acknowledged on Development host.",
            "Synthetic upstream requires syntheticMarkerAcknowledged=true on Development host.");
    }

    private static CanonicalCampaignReadinessFinding Finding(string code, bool ok, string pass, string block) =>
        new(
            code,
            ok
                ? WeddingPlannerCampaignReadinessFindingSeverities.Pass
                : WeddingPlannerCampaignReadinessFindingSeverities.Block,
            ok ? pass : block);

    /// <summary>Safe concept-id extraction; returns empty set when package JSON is unusable.</summary>
    public static bool TrySelectedConceptPresentInPackage(string? conceptPackageDocumentJson, string? selectedConceptId)
    {
        if (string.IsNullOrWhiteSpace(conceptPackageDocumentJson) || string.IsNullOrWhiteSpace(selectedConceptId))
        {
            return false;
        }

        try
        {
            var ids = WeddingPlannerConceptWorkshopValidation.ExtractConceptIdsFromPackage(conceptPackageDocumentJson);
            return ids.Contains(selectedConceptId);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public sealed record CanonicalCampaignReadinessFinding(string Code, string Severity, string Message);

public sealed record CanonicalCampaignReadinessRulesFindings(
    string OverallSeverity,
    IReadOnlyList<CanonicalCampaignReadinessFinding> Findings,
    string DocumentJson);

public sealed record WeddingPlannerCampaignReadinessRulesContext(
    Guid WorkspaceId,
    Guid WorkspaceAdvertiserId,
    Guid? WorkspaceCurrentAcceptedQaReviewReportVersionId,
    Guid? WorkspaceCurrentApprovedCreativePackageVersionId,
    WeddingPlannerQaReviewReportVersion? QaReport,
    WeddingPlannerQaReviewDecision? LatestQaDecision,
    WeddingPlannerCreativePackageVersion? Package,
    string? RecomputedPackageDocumentSha256,
    WeddingPlannerCreativePackageDecision? LatestCreativeApproveDecision,
    SelectedVariantQaSnapshot? SelectedVariantSnapshot,
    WeddingPlannerCreativeAsset? SelectedAsset,
    int AssetsForSelectedVariantCount,
    WeddingPlannerConceptPackageVersion? ConceptPackage,
    bool SelectedConceptPresentInConceptPackage,
    WeddingPlannerBrandDnaVersion? BrandDna,
    WeddingPlannerColorProfileVersion? ColorProfile,
    WeddingPlannerResearchReportVersion? ResearchReport,
    BlissMatch? Match,
    AdvertiserOpportunity? Opportunity,
    Guid? OpportunityProgramAdvertiserId,
    Campaign? Campaign,
    ContentItem? Content,
    AdInventorySlot? Slot,
    bool HasSyntheticUpstream,
    bool IsDevelopmentHost,
    bool? SyntheticMarkerAcknowledged);
