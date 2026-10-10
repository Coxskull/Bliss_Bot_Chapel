using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

public sealed class VisualDnaAcceptanceIntegrationTests
{
    [Fact]
    public void A_clean_visual_gate_only_advances_a_stored_draft_to_human_review()
    {
        var voyage = GeneratedVoyage();
        var reviewed = CreativeAcceptance.ApplyVisualQualityEvidence(
            voyage, 96, [], true, true, true, true, true);

        Assert.Equal("AWAITING_REVIEW", reviewed.Status);
        Assert.Equal("PASS", reviewed.QualityQa.Status);
        Assert.DoesNotContain("VISUAL_QUALITY_QA_REQUIRED", reviewed.Blockers);
        Assert.Contains("HUMAN_REVIEW_REQUIRED", reviewed.Blockers);
        Assert.DoesNotContain("USAGE_COST_UNRECORDED", reviewed.Blockers);
        Assert.Equal("RECORDED", reviewed.ProviderJob.CostStatus);
        Assert.Equal(0.05m, reviewed.ProviderJob.Cost);
        Assert.False(reviewed.CampaignReady);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
        Assert.Equal("PASS", reviewed.Trace.Single(item => item.Sequence == 7).Status);
    }

    [Fact]
    public void A_hard_visual_defect_blocks_the_draft_even_with_a_perfect_score()
    {
        var voyage = GeneratedVoyage();
        var reviewed = CreativeAcceptance.ApplyVisualQualityEvidence(
            voyage, 100, ["BRAND_DNA_VIOLATION"], true, false, true, true, true);

        Assert.Equal("BLOCKED", reviewed.Status);
        Assert.Equal("FAIL", reviewed.QualityQa.Status);
        Assert.Contains("VISUAL_QUALITY_QA_REQUIRED", reviewed.Blockers);
        Assert.Contains("GLOBAL_VISUAL_DNA_FAIL", reviewed.Blockers);
        Assert.Contains("VISUAL_DEFECT_BRAND_DNA_VIOLATION", reviewed.Blockers);
        Assert.False(reviewed.CampaignReady);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
    }

    [Fact]
    public void Missing_visual_evidence_withholds_release_and_does_not_invent_an_image()
    {
        var voyage = GeneratedVoyage();
        var reviewed = CreativeAcceptance.ApplyVisualQualityEvidence(
            voyage, 100, [], false, true, true, true, true);

        Assert.Equal("BLOCKED", reviewed.Status);
        Assert.Equal("WITHHELD", reviewed.QualityQa.Status);
        Assert.Contains("GLOBAL_VISUAL_DNA_WITHHELD", reviewed.Blockers);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
    }

    [Fact]
    public void Visual_evidence_cannot_be_applied_before_a_generated_draft_exists()
    {
        var board = RealEstateCatalog.Board();
        var campaign = new AcceptanceCampaignBrief(
            "visual-gate-no-image", "Harborlight Pharmacy", "Panama City", "Panama",
            "pharmacy", "Introduce prescription pickup", "ARE-P01");
        var voyage = CreativeAcceptance.Run(
            campaign, [], board.Products.Single(item => item.ProductId == "ARE-P01"),
            board.Slots, "OpenAI", "gpt-image-1", false);

        var error = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcceptance.ApplyVisualQualityEvidence(voyage, 100, [], true, true, true, true, true));
        Assert.Contains("stored generated draft", error.Message);
    }

    [Fact]
    public void Unrecorded_catalog_geometry_blocks_generation_before_any_provider_call()
    {
        var board = RealEstateCatalog.Board();
        var references = new List<AcademyReferenceRecord>
        {
            Active("ACA-GEOM-001-V1", 1, "pharmacy", "clear product hierarchy", "exact packaging"),
            Active("ACA-GEOM-002-V1", 2, "retail", "strong contrast", "exact brand marks")
        };
        var campaign = new AcceptanceCampaignBrief(
            "visual-gate-unrecorded-geometry",
            "Harborlight Pharmacy",
            "Panama City",
            "Panama",
            "pharmacy",
            "Introduce prescription pickup",
            "ARE-P01");

        var voyage = CreativeAcceptance.Run(
            campaign,
            references,
            board.Products.Single(item => item.ProductId == "ARE-P01"),
            board.Slots,
            "OpenAI",
            "gpt-image-1",
            providerConfigured: true);

        Assert.Equal("GEOMETRY_UNRECORDED", voyage.InventoryPreflight.Status);
        Assert.Equal("BLOCKED_INVENTORY_GEOMETRY", voyage.ProviderJob.Status);
        Assert.Equal("NOT_STARTED", voyage.ProviderJob.JobId);
        Assert.Equal(0, voyage.ProviderJob.ModelCalls);
        Assert.Contains("INVENTORY_GEOMETRY_REQUIRED", voyage.Blockers);
        Assert.False(voyage.CampaignReady);
        Assert.Equal("NOT_SENT", voyage.Delivery);
    }

    private static CreativeAcceptanceVoyage GeneratedVoyage()
    {
        var board = RealEstateCatalog.Board();
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();
        var references = new List<AcademyReferenceRecord>
        {
            Active("ACA-001-V1", 1, "pharmacy", "premium product lighting", "do not copy exact packaging"),
            Active("ACA-006-V1", 6, "restaurant", "lighting and depth", "exact bay"),
            Active("ACA-008-V1", 8, "auto-repair", "typography", "reference headline")
        };
        var campaign = new AcceptanceCampaignBrief(
            "visual-gate-generated", "Harborlight Pharmacy", "Panama City", "Panama",
            "pharmacy", "Introduce prescription pickup", "ARE-P01");
        var voyage = CreativeAcceptance.Run(
            campaign, references, board.Products.Single(item => item.ProductId == "ARE-P01"),
            slots, "OpenAI", "gpt-image-1", true);
        return CreativeAcceptance.WithGeneration(
            voyage,
            new AcceptanceGenerationOutcome(
                true, "visual-gate-job", "/operations/generated/visual-gate.png",
                1, "RECORDED", 0.05m, "USD", "Draft stored for review."),
            references);
    }

    private static AcademyReferenceRecord Active(
        string id, int number, string niche, string learn, string doNotCopy) =>
        new(id, number, niche, niche, id + ".jpeg", ReferenceLibrary.Active,
            ReferenceLibrary.Uploaded, true, learn, doNotCopy);
}
