using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;

namespace Bliss.Domain.WeddingPlanner;

/// <summary>
/// Deterministic measurement-rules.v1. Severity model: PASS | BLOCK only — no WARN.
/// Every code appears exactly once. Overall = BLOCK if any finding is BLOCK; else PASS.
/// Any BLOCK fails the job before AI calls or report creation.
/// </summary>
public static class WeddingPlannerMeasurementLearningRulesEngine
{
    private static readonly JsonSerializerOptions NodeWriteOptions = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static CanonicalMeasurementLearningRulesFindings Evaluate(WeddingPlannerMeasurementLearningRulesContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var findings = new List<CanonicalMeasurementLearningFinding>
        {
            EvalHandshakeScope(context),
            EvalHandshakePlacementLink(context),
            EvalPlacementStillPlanned(context),
            EvalObservationWindow(context),
            EvalSourceAttested(context),
            EvalAggregateCounts(context),
            EvalFinancialValues(context),
            EvalCurrencyCode(context),
            EvalNoEventLevelData(context),
            EvalProvenanceChain(context)
        };

        if (findings.Count == 0)
        {
            throw new InvalidOperationException("measurement-rules.v1 findings array cannot be empty.");
        }

        foreach (var code in WeddingPlannerMeasurementLearningRuleCodes.All)
        {
            if (findings.All(f => !string.Equals(f.Code, code, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"measurement-rules.v1 missing required code '{code}'.");
            }
        }

        if (findings.Count != WeddingPlannerMeasurementLearningRuleCodes.All.Count)
        {
            throw new InvalidOperationException("measurement-rules.v1 must emit each code exactly once.");
        }

        if (findings.Any(f =>
                !string.Equals(f.Severity, WeddingPlannerMeasurementLearningFindingSeverities.Pass, StringComparison.Ordinal)
                && !string.Equals(f.Severity, WeddingPlannerMeasurementLearningFindingSeverities.Block, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("measurement-rules.v1 must not emit WARN or unknown severities.");
        }

        var overall = findings.Any(f =>
            string.Equals(f.Severity, WeddingPlannerMeasurementLearningFindingSeverities.Block, StringComparison.Ordinal))
            ? WeddingPlannerMeasurementLearningFindingSeverities.Block
            : WeddingPlannerMeasurementLearningFindingSeverities.Pass;

        var document = new JsonObject
        {
            ["schemaVersion"] = WeddingPlannerSchemaVersions.MeasurementRulesV1,
            ["overallSeverity"] = overall,
            ["findings"] = new JsonArray(findings.Select(f => (JsonNode)new JsonObject
            {
                ["code"] = f.Code,
                ["severity"] = f.Severity,
                ["message"] = f.Message
            }).ToArray())
        };

        return new CanonicalMeasurementLearningRulesFindings(
            overall,
            findings,
            document.ToJsonString(NodeWriteOptions));
    }

    private static CanonicalMeasurementLearningFinding EvalHandshakeScope(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.Handshake is not null
                 && context.Handshake.Id == context.JobCampaignReadinessHandshakeVersionId
                 && context.Handshake.WorkspaceId == context.WorkspaceId
                 && context.Handshake.AdvertiserId == context.WorkspaceAdvertiserId
                 && (string.Equals(
                         context.Handshake.Status,
                         WeddingPlannerCampaignReadinessHandshakeStatuses.CampaignReady,
                         StringComparison.Ordinal)
                     || string.Equals(
                         context.Handshake.Status,
                         WeddingPlannerCampaignReadinessHandshakeStatuses.Revoked,
                         StringComparison.Ordinal));
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.HandshakeScope,
            ok,
            "Handshake exists in workspace scope with CAMPAIGN_READY or REVOKED status.",
            "Handshake is missing, cross-tenant, or status is not CAMPAIGN_READY/REVOKED.");
    }

    private static CanonicalMeasurementLearningFinding EvalHandshakePlacementLink(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.Handshake is not null
                 && context.Placement is not null
                 && context.PlacementRun is not null
                 && context.Handshake.CampaignPlacementId == context.Placement.Id
                 && context.Handshake.CampaignPlacementRunId == context.PlacementRun.Id
                 && context.Placement.Id == context.JobCampaignPlacementId
                 && context.PlacementRun.Id == context.JobCampaignPlacementRunId
                 && context.PlacementRun.CampaignPlacementId == context.Placement.Id
                 && context.Placement.CampaignId == context.Handshake.CampaignId
                 && context.Placement.ContentItemId == context.Handshake.ContentItemId
                 && context.Placement.AdInventorySlotId == context.Handshake.AdInventorySlotId;
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.HandshakePlacementLink,
            ok,
            "Handshake placement and placement-run pins resolve and link consistently.",
            "Handshake placement/placement-run pins are missing or inconsistent.");
    }

    private static CanonicalMeasurementLearningFinding EvalPlacementStillPlanned(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.Placement is not null
                 && string.Equals(context.Placement.Status, EntityStatuses.Planned, StringComparison.Ordinal)
                 && context.PlacementRun is not null
                 && string.Equals(context.PlacementRun.Outcome, EntityStatuses.Planned, StringComparison.Ordinal);
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.PlacementStillPlanned,
            ok,
            "Linked placement remains PLANNED (not treated as delivery proof).",
            "Linked placement or placement-run is missing or no longer PLANNED.");
    }

    private static CanonicalMeasurementLearningFinding EvalObservationWindow(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.ObservationWindowValid;
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.ObservationWindow,
            ok,
            "Observation window is UTC, end strictly after start, not future-ended, and within 366 days.",
            "Observation window is invalid (ordering, future end, duration, or non-UTC).");
    }

    private static CanonicalMeasurementLearningFinding EvalSourceAttested(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.AttestationAcknowledged
                 && !string.IsNullOrWhiteSpace(context.SourceLabel)
                 && !string.IsNullOrWhiteSpace(context.ObservationSourceSystem);
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.SourceAttested,
            ok,
            "Human attestation is acknowledged with source label and source system.",
            "Attestation is missing or source label/system is blank.");
    }

    private static CanonicalMeasurementLearningFinding EvalAggregateCounts(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.Impressions >= 0
                 && context.Clicks >= 0
                 && context.Conversions >= 0
                 && context.Clicks <= context.Impressions;
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.AggregateCounts,
            ok,
            "Aggregate counts are non-negative integers with clicks <= impressions.",
            "Aggregate counts are negative or clicks exceed impressions.");
    }

    private static CanonicalMeasurementLearningFinding EvalFinancialValues(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.Spend >= 0m
                 && (context.Revenue is null || context.Revenue >= 0m);
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.FinancialValues,
            ok,
            "Spend is >= 0 and optional revenue is null or >= 0.",
            "Spend or revenue financial values are invalid.");
    }

    private static CanonicalMeasurementLearningFinding EvalCurrencyCode(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = WeddingPlannerMeasurementLearningValidation.IsIso4217LikeCurrency(context.CurrencyCode);
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.CurrencyCode,
            ok,
            "Currency code is a three-letter uppercase ISO 4217-like value.",
            "Currency code is missing or not three-letter uppercase.");
    }

    private static CanonicalMeasurementLearningFinding EvalNoEventLevelData(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.NoEventLevelDataPresent;
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.NoEventLevelData,
            ok,
            "Input contains only human-attested aggregates (no event-level data).",
            "Event-level, identity, URL, credential, or raw provider payload fields were detected.");
    }

    private static CanonicalMeasurementLearningFinding EvalProvenanceChain(
        WeddingPlannerMeasurementLearningRulesContext context)
    {
        var ok = context.Handshake is not null
                 && context.ProvenancePinsValid
                 && context.Handshake.QaReviewReportVersionId == context.JobQaReviewReportVersionId
                 && context.Handshake.ApprovedCreativePackageVersionId == context.JobApprovedCreativePackageVersionId
                 && string.Equals(context.Handshake.SelectedVariantId, context.JobSelectedVariantId, StringComparison.Ordinal)
                 && context.Handshake.SelectedCreativeAssetId == context.JobSelectedCreativeAssetId
                 && context.Handshake.ApprovedConceptPackageVersionId == context.JobApprovedConceptPackageVersionId
                 && string.Equals(context.Handshake.SelectedConceptId, context.JobSelectedConceptId, StringComparison.Ordinal)
                 && context.Handshake.ApprovedBrandDnaVersionId == context.JobApprovedBrandDnaVersionId
                 && context.Handshake.ApprovedBrandDnaVersionNumber == context.JobApprovedBrandDnaVersionNumber
                 && context.Handshake.ApprovedColorProfileVersionId == context.JobApprovedColorProfileVersionId
                 && context.Handshake.ApprovedColorProfileVersionNumber == context.JobApprovedColorProfileVersionNumber
                 && context.Handshake.ApprovedResearchReportVersionId == context.JobApprovedResearchReportVersionId
                 && context.Handshake.ApprovedResearchReportVersionNumber == context.JobApprovedResearchReportVersionNumber
                 && context.Handshake.BlissMatchId == context.JobBlissMatchId
                 && context.Handshake.CampaignId == context.JobCampaignId
                 && context.Handshake.ContentItemId == context.JobContentItemId
                 && context.Handshake.AdInventorySlotId == context.JobAdInventorySlotId;
        return Finding(
            WeddingPlannerMeasurementLearningRuleCodes.ProvenanceChain,
            ok,
            "Handshake provenance pins resolve in-scope and match job pins.",
            "Handshake provenance pins are missing, cross-tenant, or inconsistent with job pins.");
    }

    private static CanonicalMeasurementLearningFinding Finding(string code, bool ok, string pass, string block) =>
        new(
            code,
            ok
                ? WeddingPlannerMeasurementLearningFindingSeverities.Pass
                : WeddingPlannerMeasurementLearningFindingSeverities.Block,
            ok ? pass : block);
}

public sealed record CanonicalMeasurementLearningFinding(string Code, string Severity, string Message);

public sealed record CanonicalMeasurementLearningRulesFindings(
    string OverallSeverity,
    IReadOnlyList<CanonicalMeasurementLearningFinding> Findings,
    string FindingsJson);

public sealed record WeddingPlannerMeasurementLearningRulesContext(
    Guid WorkspaceId,
    Guid WorkspaceAdvertiserId,
    Guid JobCampaignReadinessHandshakeVersionId,
    Guid JobCampaignPlacementId,
    Guid JobCampaignPlacementRunId,
    Guid JobQaReviewReportVersionId,
    Guid JobApprovedCreativePackageVersionId,
    string JobSelectedVariantId,
    Guid JobSelectedCreativeAssetId,
    Guid JobApprovedConceptPackageVersionId,
    string JobSelectedConceptId,
    Guid JobApprovedBrandDnaVersionId,
    int JobApprovedBrandDnaVersionNumber,
    Guid JobApprovedColorProfileVersionId,
    int JobApprovedColorProfileVersionNumber,
    Guid JobApprovedResearchReportVersionId,
    int JobApprovedResearchReportVersionNumber,
    Guid JobBlissMatchId,
    Guid JobCampaignId,
    Guid JobContentItemId,
    Guid JobAdInventorySlotId,
    WeddingPlannerCampaignReadinessHandshakeVersion? Handshake,
    CampaignPlacement? Placement,
    CampaignPlacementRun? PlacementRun,
    bool ObservationWindowValid,
    bool AttestationAcknowledged,
    string SourceLabel,
    string ObservationSourceSystem,
    long Impressions,
    long Clicks,
    long Conversions,
    decimal Spend,
    decimal? Revenue,
    string CurrencyCode,
    bool NoEventLevelDataPresent,
    bool ProvenancePinsValid);
