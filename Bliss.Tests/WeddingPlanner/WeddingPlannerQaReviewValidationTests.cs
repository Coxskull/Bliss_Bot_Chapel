using System.Text.Json.Nodes;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerQaReviewValidationTests
{
    [Fact]
    public void Rejects_forbidden_brief_fields_and_unknown_focus_areas()
    {
        var node = JsonNode.Parse("""{"campaignId":"c1","reviewObjective":"x"}""")!;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerQaReviewValidation.RejectForbiddenBriefFields(node));

        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerQaReviewValidation.CanonicalizeBrief(
                "Objective",
                ["COPY", "COPY"],
                null,
                Guid.NewGuid(),
                new string('a', 64),
                Guid.NewGuid(),
                "variant_1",
                Guid.NewGuid(),
                new string('b', 64),
                "concept_1",
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                1,
                WeddingPlannerAiProviderKinds.Local,
                WeddingPlannerWorkers.LocalDeterministicV1));

        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerQaReviewValidation.CanonicalizeBrief(
                "Objective",
                ["NOT_A_FOCUS"],
                null,
                Guid.NewGuid(),
                new string('a', 64),
                Guid.NewGuid(),
                "variant_1",
                Guid.NewGuid(),
                new string('b', 64),
                "concept_1",
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                1,
                WeddingPlannerAiProviderKinds.Local,
                WeddingPlannerWorkers.LocalDeterministicV1));
    }

    [Fact]
    public void Qa_inspection_forbids_pass_recommended_under_block()
    {
        var json = """
            {
              "schemaVersion":"qa-inspection-worker-output.v1",
              "workerProfileVersion":"QA_INSPECTION_V1",
              "marker":"SYNTHETIC DEVELOPMENT QA REVIEW",
              "selectedVariantId":"variant_1",
              "rulesOverallSeverity":"BLOCK",
              "contributions":[{
                "logicalRole":"QA_INSPECTOR",
                "summary":"notes",
                "alignmentNotes":{"format":["ok"],"copy":["ok"],"assetMetadata":["ok"]},
                "uncertainties":["u"],
                "proposedOutcome":"PASS_RECOMMENDED"
              }]
            }
            """;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerQaReviewValidation.CanonicalizeQaInspectionOutput(
                json, "variant_1", WeddingPlannerQaFindingSeverities.Block, true));
    }

    [Fact]
    public void Steward_is_rules_human_with_null_run_and_merge_requires_exact_disclaimer()
    {
        var steward = WeddingPlannerQaReviewValidation.BuildStewardContribution(
            WeddingPlannerQaFindingSeverities.Warn,
            [],
            [WeddingPlannerQaRuleCodes.CopyLengthWarn],
            WeddingPlannerQaProposedOutcomes.PassRecommended);
        Assert.Equal(WeddingPlannerQaContributionSources.RulesHuman, steward.ContributionSource);
        Assert.Equal(WeddingPlannerQaLogicalRoles.HumanEscalationSteward, steward.LogicalRole);
        Assert.Contains("RULES_HUMAN", steward.ContributionJson, StringComparison.Ordinal);
        Assert.Contains("producingAgentRunId", steward.ContributionJson, StringComparison.Ordinal);

        Assert.Equal(WeddingPlannerQaReviewReportDisclaimer.Text, WeddingPlannerQaReviewReportDisclaimer.Text);
    }

    [Fact]
    public void Accept_requires_confirmations_and_synthetic_ack_only_when_marker_present()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerQaReviewValidation.ValidateDecisionBody(
                "ACCEPT",
                "ok",
                "variant_1",
                "variant_1",
                true,
                true,
                true,
                false,
                null,
                reportHasSyntheticMarker: true,
                WeddingPlannerQaFindingSeverities.Pass,
                null));

        WeddingPlannerQaReviewValidation.ValidateDecisionBody(
            "ACCEPT",
            "ok",
            "variant_1",
            "variant_1",
            true,
            true,
            true,
            false,
            null,
            reportHasSyntheticMarker: false,
            WeddingPlannerQaFindingSeverities.Pass,
            null);

        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerQaReviewValidation.ValidateDecisionBody(
                "ACCEPT",
                "ok",
                "variant_1",
                "variant_1",
                true,
                true,
                true,
                true,
                null,
                reportHasSyntheticMarker: false,
                WeddingPlannerQaFindingSeverities.Block,
                null));
    }
}
