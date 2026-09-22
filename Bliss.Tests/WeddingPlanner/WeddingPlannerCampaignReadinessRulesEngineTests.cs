using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerCampaignReadinessRulesEngineTests
{
    [Fact]
    public void All_sixteen_codes_present_and_pass_when_context_valid()
    {
        var result = WeddingPlannerCampaignReadinessRulesEngine.Evaluate(GoodContext());
        Assert.Equal(WeddingPlannerCampaignReadinessFindingSeverities.Pass, result.OverallSeverity);
        Assert.Equal(16, result.Findings.Count);
        Assert.Equal(
            WeddingPlannerCampaignReadinessRuleCodes.All.ToArray(),
            result.Findings.Select(f => f.Code).ToArray());
        Assert.All(result.Findings, f =>
            Assert.Equal(WeddingPlannerCampaignReadinessFindingSeverities.Pass, f.Severity));
        Assert.DoesNotContain(result.Findings, f => f.Severity == "WARN");
    }

    [Theory]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.CurrentQaPointer)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.QaCleanAccepted)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.QaAcceptDecision)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.PackageCurrentApproved)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.PackageDocumentSha)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.CreativeDecisionVariant)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.AssetIntegrity)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.ProvenanceChain)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.MatchApproved)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.OpportunityActive)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.AdvertiserScope)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.CampaignDraft)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.CampaignOpportunity)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.ContentCreator)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.SlotContent)]
    [InlineData(WeddingPlannerCampaignReadinessRuleCodes.SyntheticEnvironment)]
    public void Each_code_independently_blocks(string code)
    {
        var ctx = Break(GoodContext(), code);
        var result = WeddingPlannerCampaignReadinessRulesEngine.Evaluate(ctx);
        Assert.Equal(16, result.Findings.Count);
        Assert.Equal(
            WeddingPlannerCampaignReadinessFindingSeverities.Block,
            result.Findings.Single(f => f.Code == code).Severity);
        Assert.Equal(WeddingPlannerCampaignReadinessFindingSeverities.Block, result.OverallSeverity);
    }

    [Fact]
    public void Creative_decision_blocks_when_approve_id_mismatches_qa_pin()
    {
        var good = GoodContext();
        var mismatched = good with
        {
            LatestCreativeApproveDecision = new WeddingPlannerCreativePackageDecision
            {
                Id = Guid.NewGuid(),
                CreativePackageVersionId = good.Package!.Id,
                Decision = WeddingPlannerCreativePackageDecisions.Approve,
                SelectedVariantId = "variant_1"
            }
        };
        var result = WeddingPlannerCampaignReadinessRulesEngine.Evaluate(mismatched);
        Assert.Equal(
            WeddingPlannerCampaignReadinessFindingSeverities.Block,
            result.Findings.Single(f => f.Code == WeddingPlannerCampaignReadinessRuleCodes.CreativeDecisionVariant)
                .Severity);
    }

    [Fact]
    public void Asset_integrity_blocks_wrong_package_asset_and_wrong_variant_meta()
    {
        var good = GoodContext();
        var wrongPackage = good with
        {
            SelectedAsset = new WeddingPlannerCreativeAsset
            {
                Id = good.SelectedAsset!.Id,
                Bytes = good.SelectedAsset.Bytes,
                Sha256 = good.SelectedAsset.Sha256,
                ByteSize = good.SelectedAsset.ByteSize,
                Width = good.SelectedAsset.Width,
                Height = good.SelectedAsset.Height,
                ContentType = "image/png",
                VariantId = "variant_1",
                CreativePackageVersionId = Guid.NewGuid(),
                WorkspaceId = good.WorkspaceId,
                AdvertiserId = good.WorkspaceAdvertiserId
            }
        };
        Assert.Equal(
            WeddingPlannerCampaignReadinessFindingSeverities.Block,
            WeddingPlannerCampaignReadinessRulesEngine.Evaluate(wrongPackage)
                .Findings.Single(f => f.Code == WeddingPlannerCampaignReadinessRuleCodes.AssetIntegrity)
                .Severity);

        var wrongMeta = good with
        {
            SelectedVariantSnapshot = good.SelectedVariantSnapshot! with
            {
                PackageAssetSha256 = new string('0', 64)
            }
        };
        Assert.Equal(
            WeddingPlannerCampaignReadinessFindingSeverities.Block,
            WeddingPlannerCampaignReadinessRulesEngine.Evaluate(wrongMeta)
                .Findings.Single(f => f.Code == WeddingPlannerCampaignReadinessRuleCodes.AssetIntegrity)
                .Severity);
    }

    [Fact]
    public void Provenance_blocks_when_selected_concept_missing_from_package()
    {
        var good = GoodContext();
        var broken = good with { SelectedConceptPresentInConceptPackage = false };
        Assert.Equal(
            WeddingPlannerCampaignReadinessFindingSeverities.Block,
            WeddingPlannerCampaignReadinessRulesEngine.Evaluate(broken)
                .Findings.Single(f => f.Code == WeddingPlannerCampaignReadinessRuleCodes.ProvenanceChain)
                .Severity);
    }

    [Fact]
    public void Exception_qa_status_blocks_clean_accepted()
    {
        var ctx = GoodContext();
        ctx = ctx with
        {
            QaReport = CloneQa(ctx.QaReport!, WeddingPlannerQaReviewReportStatuses.AcceptedWithException)
        };
        var result = WeddingPlannerCampaignReadinessRulesEngine.Evaluate(ctx);
        Assert.Equal(
            WeddingPlannerCampaignReadinessFindingSeverities.Block,
            result.Findings.Single(f => f.Code == WeddingPlannerCampaignReadinessRuleCodes.QaCleanAccepted).Severity);
    }

    [Fact]
    public void Synthetic_non_development_blocks_even_with_ack()
    {
        var ctx = GoodContext() with
        {
            HasSyntheticUpstream = true,
            IsDevelopmentHost = false,
            SyntheticMarkerAcknowledged = true
        };
        var result = WeddingPlannerCampaignReadinessRulesEngine.Evaluate(ctx);
        Assert.Equal(
            WeddingPlannerCampaignReadinessFindingSeverities.Block,
            result.Findings.Single(f => f.Code == WeddingPlannerCampaignReadinessRuleCodes.SyntheticEnvironment).Severity);
    }

    private static WeddingPlannerCampaignReadinessRulesContext Break(
        WeddingPlannerCampaignReadinessRulesContext good,
        string code) =>
        code switch
        {
            WeddingPlannerCampaignReadinessRuleCodes.CurrentQaPointer =>
                good with { WorkspaceCurrentAcceptedQaReviewReportVersionId = null },
            WeddingPlannerCampaignReadinessRuleCodes.QaCleanAccepted =>
                good with { QaReport = CloneQa(good.QaReport!, WeddingPlannerQaReviewReportStatuses.Proposed) },
            WeddingPlannerCampaignReadinessRuleCodes.QaAcceptDecision =>
                good with
                {
                    LatestQaDecision = new WeddingPlannerQaReviewDecision
                    {
                        Id = good.LatestQaDecision!.Id,
                        QaReviewReportVersionId = good.LatestQaDecision.QaReviewReportVersionId,
                        Decision = WeddingPlannerQaReviewDecisions.Escalate
                    }
                },
            WeddingPlannerCampaignReadinessRuleCodes.PackageCurrentApproved =>
                good with { WorkspaceCurrentApprovedCreativePackageVersionId = Guid.NewGuid() },
            WeddingPlannerCampaignReadinessRuleCodes.PackageDocumentSha =>
                good with { RecomputedPackageDocumentSha256 = new string('0', 64) },
            WeddingPlannerCampaignReadinessRuleCodes.CreativeDecisionVariant =>
                good with { SelectedVariantSnapshot = null },
            WeddingPlannerCampaignReadinessRuleCodes.AssetIntegrity =>
                good with { AssetsForSelectedVariantCount = 0, SelectedAsset = null },
            WeddingPlannerCampaignReadinessRuleCodes.ProvenanceChain =>
                good with { BrandDna = null },
            WeddingPlannerCampaignReadinessRuleCodes.MatchApproved =>
                good with { Match = CloneMatch(good.Match!, EntityStatuses.Created) },
            WeddingPlannerCampaignReadinessRuleCodes.OpportunityActive =>
                good with { Opportunity = CloneOpp(good.Opportunity!, "INACTIVE") },
            WeddingPlannerCampaignReadinessRuleCodes.AdvertiserScope =>
                good with { OpportunityProgramAdvertiserId = Guid.NewGuid() },
            WeddingPlannerCampaignReadinessRuleCodes.CampaignDraft =>
                good with { Campaign = CloneCampaign(good.Campaign!, "ACTIVE") },
            WeddingPlannerCampaignReadinessRuleCodes.CampaignOpportunity =>
                good with
                {
                    Campaign = CloneCampaign(good.Campaign!, EntityStatuses.Draft, Guid.NewGuid())
                },
            WeddingPlannerCampaignReadinessRuleCodes.ContentCreator =>
                good with { Content = CloneContent(good.Content!, Guid.NewGuid()) },
            WeddingPlannerCampaignReadinessRuleCodes.SlotContent =>
                good with { Slot = CloneSlot(good.Slot!, Guid.NewGuid()) },
            WeddingPlannerCampaignReadinessRuleCodes.SyntheticEnvironment =>
                good with
                {
                    HasSyntheticUpstream = true,
                    IsDevelopmentHost = true,
                    SyntheticMarkerAcknowledged = false
                },
            _ => throw new InvalidOperationException(code)
        };

    private static WeddingPlannerCampaignReadinessRulesContext GoodContext()
    {
        var workspaceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var advertiserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var qaId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var packageId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var decisionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var assetId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var conceptId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var dnaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var colorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var researchId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var matchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var oppId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var campaignId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var contentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var slotId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var creatorId = Guid.Parse("abababab-abab-abab-abab-abababababab");
        var qaDecisionId = Guid.Parse("12121212-1212-1212-1212-121212121212");

        var png = MinimalPng(8, 8);
        var sha = WeddingPlannerCampaignReadinessValidation.Sha256Hex(png);
        var packageJson =
            "{\"schemaVersion\":\"creative-package.v1\",\"variants\":[{\"id\":\"variant_1\",\"format\":\"STATIC_SOCIAL_SQUARE\",\"canvas\":{\"width\":8,\"height\":8},\"copy\":{\"kind\":\"CREATIVE_NON_FACTUAL\",\"headline\":\"h\",\"body\":\"b\",\"cta\":\"c\"},\"factualClaims\":[],\"paletteRoleRefs\":[\"primary\"],\"asset\":{\"creativeAssetId\":\""
            + assetId
            + "\",\"contentType\":\"image/png\",\"byteSize\":"
            + png.Length
            + ",\"sha256\":\""
            + sha
            + "\",\"width\":8,\"height\":8}}]}";
        var packageSha = WeddingPlannerCampaignReadinessValidation.Sha256Hex(packageJson);
        var conceptJson = "{\"schemaVersion\":\"concept-package.v1\",\"concepts\":[{\"id\":\"concept_1\"}]}";

        var qa = new WeddingPlannerQaReviewReportVersion
        {
            Id = qaId,
            AdvertiserId = advertiserId,
            WorkspaceId = workspaceId,
            Status = WeddingPlannerQaReviewReportStatuses.Accepted,
            ApprovedCreativePackageVersionId = packageId,
            CreativePackageDocumentSha256 = packageSha,
            CreativePackageDecisionId = decisionId,
            SelectedVariantId = "variant_1",
            SelectedCreativeAssetId = assetId,
            SelectedCreativeAssetSha256 = sha,
            SelectedConceptId = "concept_1",
            ApprovedBrandDnaVersionId = dnaId,
            ApprovedBrandDnaVersionNumber = 1,
            ApprovedColorProfileVersionId = colorId,
            ApprovedColorProfileVersionNumber = 1,
            ApprovedResearchReportVersionId = researchId,
            ApprovedResearchReportVersionNumber = 1,
            DocumentJson = "{}"
        };

        var package = new WeddingPlannerCreativePackageVersion
        {
            Id = packageId,
            AdvertiserId = advertiserId,
            WorkspaceId = workspaceId,
            Status = WeddingPlannerCreativePackageStatuses.Approved,
            DocumentJson = packageJson,
            SelectedConceptId = "concept_1",
            ApprovedConceptPackageVersionId = conceptId,
            ApprovedBrandDnaVersionId = dnaId,
            ApprovedBrandDnaVersionNumber = 1,
            ApprovedColorProfileVersionId = colorId,
            ApprovedColorProfileVersionNumber = 1,
            ApprovedResearchReportVersionId = researchId,
            ApprovedResearchReportVersionNumber = 1
        };

        var snapshot = WeddingPlannerQaRulesEngine.ExtractSelectedVariant(packageJson, "variant_1");
        Assert.NotNull(snapshot);

        return new WeddingPlannerCampaignReadinessRulesContext(
            workspaceId,
            advertiserId,
            qaId,
            packageId,
            qa,
            new WeddingPlannerQaReviewDecision
            {
                Id = qaDecisionId,
                QaReviewReportVersionId = qaId,
                Decision = WeddingPlannerQaReviewDecisions.Accept
            },
            package,
            packageSha,
            new WeddingPlannerCreativePackageDecision
            {
                Id = decisionId,
                CreativePackageVersionId = packageId,
                Decision = WeddingPlannerCreativePackageDecisions.Approve,
                SelectedVariantId = "variant_1"
            },
            snapshot,
            new WeddingPlannerCreativeAsset
            {
                Id = assetId,
                Bytes = png,
                Sha256 = sha,
                ByteSize = png.Length,
                Width = 8,
                Height = 8,
                ContentType = "image/png",
                VariantId = "variant_1",
                CreativePackageVersionId = packageId,
                WorkspaceId = workspaceId,
                AdvertiserId = advertiserId
            },
            1,
            new WeddingPlannerConceptPackageVersion
            {
                Id = conceptId,
                WorkspaceId = workspaceId,
                AdvertiserId = advertiserId,
                Status = WeddingPlannerConceptPackageStatuses.Approved,
                DocumentJson = conceptJson
            },
            true,
            new WeddingPlannerBrandDnaVersion
            {
                Id = dnaId,
                WorkspaceId = workspaceId,
                AdvertiserId = advertiserId,
                VersionNumber = 1,
                Status = WeddingPlannerBrandDnaStatuses.Approved
            },
            new WeddingPlannerColorProfileVersion
            {
                Id = colorId,
                WorkspaceId = workspaceId,
                AdvertiserId = advertiserId,
                VersionNumber = 1,
                Status = WeddingPlannerColorProfileStatuses.Approved
            },
            new WeddingPlannerResearchReportVersion
            {
                Id = researchId,
                WorkspaceId = workspaceId,
                AdvertiserId = advertiserId,
                VersionNumber = 1,
                Status = WeddingPlannerResearchReportStatuses.Approved
            },
            new BlissMatch
            {
                Id = matchId,
                CreatorId = creatorId,
                AdvertiserOpportunityId = oppId,
                Status = EntityStatuses.Approved,
                RuleVersionId = Guid.NewGuid()
            },
            new AdvertiserOpportunity
            {
                Id = oppId,
                Status = EntityStatuses.Active,
                Name = "Opp"
            },
            advertiserId,
            new Campaign
            {
                Id = campaignId,
                Status = EntityStatuses.Draft,
                AdvertiserOpportunityId = null,
                Name = "Draft"
            },
            new ContentItem
            {
                Id = contentId,
                CreatorId = creatorId,
                Title = "Content"
            },
            new AdInventorySlot
            {
                Id = slotId,
                ContentItemId = contentId,
                SlotType = "PRE_ROLL",
                IsAvailable = true
            },
            false,
            true,
            null);
    }

    private static WeddingPlannerQaReviewReportVersion CloneQa(
        WeddingPlannerQaReviewReportVersion source,
        string status) =>
        new()
        {
            Id = source.Id,
            AdvertiserId = source.AdvertiserId,
            WorkspaceId = source.WorkspaceId,
            Status = status,
            ApprovedCreativePackageVersionId = source.ApprovedCreativePackageVersionId,
            CreativePackageDocumentSha256 = source.CreativePackageDocumentSha256,
            CreativePackageDecisionId = source.CreativePackageDecisionId,
            SelectedVariantId = source.SelectedVariantId,
            SelectedCreativeAssetId = source.SelectedCreativeAssetId,
            SelectedCreativeAssetSha256 = source.SelectedCreativeAssetSha256,
            SelectedConceptId = source.SelectedConceptId,
            ApprovedBrandDnaVersionId = source.ApprovedBrandDnaVersionId,
            ApprovedBrandDnaVersionNumber = source.ApprovedBrandDnaVersionNumber,
            ApprovedColorProfileVersionId = source.ApprovedColorProfileVersionId,
            ApprovedColorProfileVersionNumber = source.ApprovedColorProfileVersionNumber,
            ApprovedResearchReportVersionId = source.ApprovedResearchReportVersionId,
            ApprovedResearchReportVersionNumber = source.ApprovedResearchReportVersionNumber,
            DocumentJson = source.DocumentJson
        };

    private static BlissMatch CloneMatch(BlissMatch source, string status) =>
        new()
        {
            Id = source.Id,
            CreatorId = source.CreatorId,
            AdvertiserOpportunityId = source.AdvertiserOpportunityId,
            RuleVersionId = source.RuleVersionId,
            Status = status
        };

    private static AdvertiserOpportunity CloneOpp(AdvertiserOpportunity source, string status) =>
        new() { Id = source.Id, Status = status, Name = source.Name };

    private static Campaign CloneCampaign(Campaign source, string status, Guid? opportunityId = null) =>
        new()
        {
            Id = source.Id,
            Status = status,
            AdvertiserOpportunityId = opportunityId ?? source.AdvertiserOpportunityId,
            Name = source.Name
        };

    private static ContentItem CloneContent(ContentItem source, Guid creatorId) =>
        new() { Id = source.Id, CreatorId = creatorId, Title = source.Title };

    private static AdInventorySlot CloneSlot(AdInventorySlot source, Guid contentId) =>
        new()
        {
            Id = source.Id,
            ContentItemId = contentId,
            SlotType = source.SlotType,
            IsAvailable = source.IsAvailable
        };

    private static byte[] MinimalPng(int width, int height) =>
        LocalDeterministicWeddingPlannerCreativeAssetProvider
            .BuildDeterministicTruecolorPng(width, height, Enumerable.Range(0, 32).Select(i => (byte)i).ToArray());
}
