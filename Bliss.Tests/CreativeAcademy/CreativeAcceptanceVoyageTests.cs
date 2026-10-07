using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

public sealed class CreativeAcceptanceVoyageTests
{
    [Fact]
    public void Candidate_files_do_not_cross_the_reference_gate()
    {
        var references = Enumerable.Range(1, 50)
            .Select(number => new AcademyReferenceRecord(
                $"ACA-{number:000}-V1",
                number,
                number == 1 ? "pharmacy" : "other-" + number,
                number == 1 ? "Pharmacy" : "Other",
                $"Reference-{number}.jpeg",
                ReferenceLibrary.Candidate,
                ReferenceLibrary.Uploaded,
                number != 5,
                "",
                ""))
            .ToList();
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var campaign = new AcceptanceCampaignBrief(
            "one-voyage-test",
            "Harborlight Pharmacy",
            "Panama City",
            "Panama",
            "pharmacy",
            "Introduce prescription pickup",
            "ARE-P01");

        var voyage = CreativeAcceptance.Run(
            campaign,
            references,
            product,
            board.Slots,
            "OpenAI",
            "gpt-image-1",
            true);

        Assert.Equal("BLOCKED", voyage.Status);
        Assert.Equal(CreativeAcceptance.Open, voyage.AmendmentStatus);
        Assert.Equal(0, voyage.ReferenceIntelligence.Active);
        Assert.Equal("REFERENCE_INTELLIGENCE_INCOMPLETE", voyage.ReferenceIntelligence.Status);
        Assert.Equal("NICHE_REFERENCE_NOT_ACTIVE", voyage.ProductionBrief.Retrieval.Status);
        Assert.Equal("NOT_CREATED", voyage.BrandDna.Status);
        Assert.Equal("BLOCKED_REFERENCE_GATE", voyage.ProviderJob.Status);
        Assert.Equal("NOT_STARTED", voyage.ProviderJob.JobId);
        Assert.Equal(0, voyage.ProviderJob.ModelCalls);
        Assert.Equal("NOT_CREATED", voyage.FinishedCreative.Status);
        Assert.Equal("GEOMETRY_UNRECORDED", voyage.InventoryPreflight.Status);
        Assert.Equal("HUMAN REVIEW", voyage.Originality.Status);
        Assert.Equal("NOT_RUN", voyage.QualityQa.Status);
        Assert.Equal("NOT_REQUESTED", voyage.HumanReview.Status);
        Assert.Contains("ACTIVE_REFERENCE_INTELLIGENCE_REQUIRED", voyage.Blockers);
        Assert.Contains("INVENTORY_GEOMETRY_REQUIRED", voyage.Blockers);
        Assert.Contains("USAGE_COST_UNRECORDED", voyage.Blockers);
        Assert.Equal(13, voyage.Trace.Count);
        Assert.Equal("INVENTORY_GEOMETRY_FAILURE", voyage.Rejections.Single().Code);
        Assert.False(voyage.Rejections.Single().PositiveReference);
        Assert.Equal("BASELINE_NOT_RECORDED", voyage.Regression.Status);
        Assert.False(voyage.Regression.Passed);
        Assert.Equal(12, voyage.Regression.Cases.Count);
        Assert.All(voyage.Provenance, item => Assert.Equal("NOT_AUTHORIZED", item.PermittedProviderUse));
        Assert.False(voyage.ReferenceAssetsSentToProvider);
        Assert.Equal("UNCLASSIFIED", voyage.StoredQuality.Single().Status);
        Assert.Equal(0, voyage.StoredQuality.Single().ModelCalls);
        Assert.Equal("GQD-1", voyage.ProductionBrief.QualityDnaVersion);
        Assert.Equal(ReferenceLibrary.QualityDna.Count, voyage.GlobalQualityDna.Count);
        Assert.False(voyage.CampaignReady);
        Assert.Equal(0, voyage.ModelCalls);
        Assert.Equal("NOT_SENT", voyage.Delivery);
    }

    [Fact]
    public void A_reference_identity_is_not_accepted_as_the_new_advertiser()
    {
        var board = RealEstateCatalog.Board();
        var campaign = new AcceptanceCampaignBrief(
            "copied-voyage",
            "VidaCare Pharmacy",
            "Panama City",
            "Panama",
            "pharmacy",
            "Introduce prescription pickup",
            "ARE-P01");

        var error = Assert.Throws<InvalidOperationException>(() => CreativeAcceptance.Run(
            campaign,
            [],
            board.Products.Single(item => item.ProductId == "ARE-P01"),
            board.Slots,
            "",
            "",
            false));

        Assert.Contains("reference identity", error.Message);
    }
}
