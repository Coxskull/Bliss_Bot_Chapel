using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerMeasurementLearningRulesEngineTests
{
    [Fact]
    public void All_ten_codes_appear_exactly_once_with_pass_or_block_only()
    {
        var result = WeddingPlannerMeasurementLearningRulesEngine.Evaluate(GoodContext());
        Assert.Equal(10, result.Findings.Count);
        Assert.Equal(
            WeddingPlannerMeasurementLearningRuleCodes.All.OrderBy(x => x).ToArray(),
            result.Findings.Select(f => f.Code).OrderBy(x => x).ToArray());
        Assert.All(result.Findings, f =>
            Assert.Contains(f.Severity, new[]
            {
                WeddingPlannerMeasurementLearningFindingSeverities.Pass,
                WeddingPlannerMeasurementLearningFindingSeverities.Block
            }));
        Assert.Equal(WeddingPlannerMeasurementLearningFindingSeverities.Pass, result.OverallSeverity);
    }

    [Fact]
    public void Placement_not_planned_blocks_before_overall_pass()
    {
        var ctx = GoodContext() with
        {
            Placement = new CampaignPlacement
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                CampaignId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                ContentItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                AdInventorySlotId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Status = "ACTIVE"
            }
        };
        var result = WeddingPlannerMeasurementLearningRulesEngine.Evaluate(ctx);
        Assert.Equal(
            WeddingPlannerMeasurementLearningFindingSeverities.Block,
            result.Findings.Single(f => f.Code == WeddingPlannerMeasurementLearningRuleCodes.PlacementStillPlanned).Severity);
        Assert.Equal(WeddingPlannerMeasurementLearningFindingSeverities.Block, result.OverallSeverity);
    }

    [Fact]
    public void Aggregate_and_financial_and_window_blockers_fire()
    {
        var badCounts = WeddingPlannerMeasurementLearningRulesEngine.Evaluate(
            GoodContext() with { Clicks = 20, Impressions = 10 });
        Assert.Equal(
            WeddingPlannerMeasurementLearningFindingSeverities.Block,
            badCounts.Findings.Single(f => f.Code == WeddingPlannerMeasurementLearningRuleCodes.AggregateCounts).Severity);

        var badFinance = WeddingPlannerMeasurementLearningRulesEngine.Evaluate(
            GoodContext() with { Spend = -1m });
        Assert.Equal(
            WeddingPlannerMeasurementLearningFindingSeverities.Block,
            badFinance.Findings.Single(f => f.Code == WeddingPlannerMeasurementLearningRuleCodes.FinancialValues).Severity);

        var badWindow = WeddingPlannerMeasurementLearningRulesEngine.Evaluate(
            GoodContext() with { ObservationWindowValid = false });
        Assert.Equal(
            WeddingPlannerMeasurementLearningFindingSeverities.Block,
            badWindow.Findings.Single(f => f.Code == WeddingPlannerMeasurementLearningRuleCodes.ObservationWindow).Severity);
    }

    private static WeddingPlannerMeasurementLearningRulesContext GoodContext()
    {
        var handshakeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var placementId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var runId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var campaignId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var contentId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var slotId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var workspaceId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var advertiserId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var handshake = new WeddingPlannerCampaignReadinessHandshakeVersion
        {
            Id = handshakeId,
            AdvertiserId = advertiserId,
            WorkspaceId = workspaceId,
            Status = WeddingPlannerCampaignReadinessHandshakeStatuses.CampaignReady,
            CampaignPlacementId = placementId,
            CampaignPlacementRunId = runId,
            CampaignId = campaignId,
            ContentItemId = contentId,
            AdInventorySlotId = slotId,
            QaReviewReportVersionId = Guid.Parse("12121212-1212-1212-1212-121212121212"),
            ApprovedCreativePackageVersionId = Guid.Parse("13131313-1313-1313-1313-131313131313"),
            SelectedVariantId = "variant_1",
            SelectedCreativeAssetId = Guid.Parse("14141414-1414-1414-1414-141414141414"),
            ApprovedConceptPackageVersionId = Guid.Parse("15151515-1515-1515-1515-151515151515"),
            SelectedConceptId = "concept_1",
            ApprovedBrandDnaVersionId = Guid.Parse("16161616-1616-1616-1616-161616161616"),
            ApprovedBrandDnaVersionNumber = 1,
            ApprovedColorProfileVersionId = Guid.Parse("17171717-1717-1717-1717-171717171717"),
            ApprovedColorProfileVersionNumber = 1,
            ApprovedResearchReportVersionId = Guid.Parse("18181818-1818-1818-1818-181818181818"),
            ApprovedResearchReportVersionNumber = 1,
            BlissMatchId = Guid.Parse("19191919-1919-1919-1919-191919191919")
        };

        return new WeddingPlannerMeasurementLearningRulesContext(
            workspaceId,
            advertiserId,
            handshakeId,
            placementId,
            runId,
            handshake.QaReviewReportVersionId,
            handshake.ApprovedCreativePackageVersionId,
            handshake.SelectedVariantId,
            handshake.SelectedCreativeAssetId,
            handshake.ApprovedConceptPackageVersionId,
            handshake.SelectedConceptId,
            handshake.ApprovedBrandDnaVersionId,
            handshake.ApprovedBrandDnaVersionNumber,
            handshake.ApprovedColorProfileVersionId,
            handshake.ApprovedColorProfileVersionNumber,
            handshake.ApprovedResearchReportVersionId,
            handshake.ApprovedResearchReportVersionNumber,
            handshake.BlissMatchId,
            campaignId,
            contentId,
            slotId,
            handshake,
            new CampaignPlacement
            {
                Id = placementId,
                CampaignId = campaignId,
                ContentItemId = contentId,
                AdInventorySlotId = slotId,
                Status = EntityStatuses.Planned
            },
            new CampaignPlacementRun
            {
                Id = runId,
                CampaignPlacementId = placementId,
                Outcome = EntityStatuses.Planned
            },
            true,
            true,
            "Operator spreadsheet",
            "OPS_SHEET",
            1000,
            20,
            2,
            40m,
            80m,
            "USD",
            true,
            true);
    }
}
