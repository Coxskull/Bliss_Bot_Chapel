using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

/// <summary>
/// Regression coverage for the Medellín automotive-parts acceptance path.
/// A clean score must not override missing originality, inventory geometry,
/// brand-compliance, or QR evidence, and a clean gate never sends a campaign.
/// </summary>
public sealed class MedellinAutomotivePartsAcceptanceTests
{
    [Theory]
    [InlineData(false, true, true, true, "GLOBAL_VISUAL_DNA_FAIL")]
    [InlineData(true, false, true, true, "GLOBAL_VISUAL_DNA_FAIL")]
    [InlineData(true, true, false, true, "GLOBAL_VISUAL_DNA_FAIL")]
    [InlineData(true, true, true, false, "GLOBAL_VISUAL_DNA_FAIL")]
    public void Medellin_automotive_parts_release_requires_every_evidence_check(
        bool brandDnaCompliant,
        bool originalityConfirmed,
        bool inventoryGeometryVerified,
        bool qrVerified,
        string expectedBlocker)
    {
        var voyage = GeneratedAutomotivePartsVoyage();

        var reviewed = CreativeAcceptance.ApplyVisualQualityEvidence(
            voyage,
            reportedScore: 100,
            defectCodes: [],
            visualEvidenceRecorded: true,
            brandDnaCompliant: brandDnaCompliant,
            originalityConfirmed: originalityConfirmed,
            inventoryGeometryVerified: inventoryGeometryVerified,
            qrVerified: qrVerified);

        Assert.Equal("BLOCKED", reviewed.Status);
        Assert.Equal("FAIL", reviewed.QualityQa.Status);
        Assert.Contains(expectedBlocker, reviewed.Blockers);
        Assert.False(reviewed.CampaignReady);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
        Assert.Equal("RECORDED", reviewed.ProviderJob.CostStatus);
        Assert.Equal(0.05m, reviewed.ProviderJob.Cost);
    }

    [Fact]
    public void Medellin_automotive_parts_clean_gate_still_requires_human_review_and_never_sends()
    {
        var reviewed = CreativeAcceptance.ApplyVisualQualityEvidence(
            GeneratedAutomotivePartsVoyage(),
            reportedScore: 100,
            defectCodes: [],
            visualEvidenceRecorded: true,
            brandDnaCompliant: true,
            originalityConfirmed: true,
            inventoryGeometryVerified: true,
            qrVerified: true);

        Assert.Equal("AWAITING_REVIEW", reviewed.Status);
        Assert.Equal("PASS", reviewed.QualityQa.Status);
        Assert.Contains("HUMAN_REVIEW_REQUIRED", reviewed.Blockers);
        Assert.False(reviewed.CampaignReady);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
    }

    private static CreativeAcceptanceVoyage GeneratedAutomotivePartsVoyage()
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
            Active("ACA-MDE-001-V1", 1, "automotive",
                "clear product hierarchy and accurate mechanical detail",
                "copying exact competitor packaging, logos, or catalog photography"),
            Active("ACA-MDE-002-V1", 2, "automotive",
                "legible typography and believable workshop lighting",
                "copying the reference headline or exact layout"),
            Active("ACA-MDE-003-V1", 3, "automotive",
                "high contrast between product and background",
                "copying brand marks or distinctive product arrangements")
        };

        var campaign = new AcceptanceCampaignBrief(
            "mde-auto-parts-acceptance",
            "Andina Motor Supply",
            "Medellín",
            "Colombia",
            "automotive",
            "Promote locally stocked replacement parts with a verified QR destination",
            "ARE-P01");

        var voyage = CreativeAcceptance.Run(
            campaign,
            references,
            board.Products.Single(item => item.ProductId == "ARE-P01"),
            slots,
            "OpenAI",
            "gpt-image-1",
            providerConfigured: true);

        Assert.Equal("READY_FOR_GENERATION", voyage.ProviderJob.Status);

        return CreativeAcceptance.WithGeneration(
            voyage,
            new AcceptanceGenerationOutcome(
                true,
                "mde-auto-parts-job",
                "/operations/generated/mde-auto-parts.png",
                1,
                "RECORDED",
                0.05m,
                "USD",
                "Synthetic test draft stored for acceptance-gate verification."),
            references);
    }

    private static AcademyReferenceRecord Active(
        string id,
        int number,
        string niche,
        string learn,
        string doNotCopy) =>
        new(id, number, niche, niche, id + ".jpeg", ReferenceLibrary.Active,
            ReferenceLibrary.Uploaded, true, learn, doNotCopy);
}
