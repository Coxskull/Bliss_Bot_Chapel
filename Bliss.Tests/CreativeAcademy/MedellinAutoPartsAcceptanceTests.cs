using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

public sealed class MedellinAutoPartsAcceptanceTests
{
    [Fact]
    public void Medellin_auto_parts_requires_retrieved_intelligence_and_recorded_slot_geometry()
    {
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();
        var references = new List<AcademyReferenceRecord>
        {
            Active("ACA-MDE-001", 1, "auto-parts", "Automotive parts", "product texture and clean component detail", "do not copy the reference brand, package, or layout"),
            Active("ACA-MDE-002", 2, "auto-parts", "Automotive parts", "credible workshop lighting", "do not copy signage, logos, or composition"),
            Active("ACA-OTHER-003", 3, "pharmacy", "Pharmacy", "legible typography", "do not copy its headline or product arrangement")
        };
        var campaign = new AcceptanceCampaignBrief(
            "medellin-auto-parts-acceptance-v1",
            "Andes Motor Supply",
            "Medellín",
            "Colombia",
            "auto-parts",
            "Promote automotive parts with clear product detail and a credible local workshop feel",
            product.ProductId);

        var voyage = CreativeAcceptance.Run(
            campaign,
            references,
            product,
            slots,
            "OpenAI",
            "gpt-image-1",
            true);

        Assert.Equal("READY_FOR_GENERATION", voyage.Status);
        Assert.Equal("READY", voyage.ReferenceIntelligence.Status);
        Assert.Equal("RETRIEVED", voyage.ProductionBrief.Retrieval.Status);
        Assert.Contains(voyage.ProductionBrief.Retrieval.Selected, item =>
            item.ReferenceId == "ACA-MDE-001" && item.Reason == "niche match");
        Assert.DoesNotContain(voyage.ProductionBrief.Retrieval.Selected, item =>
            item.ReferenceId == "ACA-OTHER-003");
        Assert.Equal("RECOMPOSED", voyage.InventoryPreflight.Status);
        Assert.Equal(180, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "LEFT_VERTICAL").Width);
        Assert.Equal(640, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "LEFT_VERTICAL").Height);
        Assert.Equal(1280, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "BOTTOM_FULL").Width);
        Assert.Equal(160, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "BOTTOM_FULL").Height);
        Assert.False(voyage.ReferenceAssetsSentToProvider);
        Assert.Equal(0, voyage.ModelCalls);
        Assert.Equal(0, voyage.ProviderJob.ModelCalls);
        Assert.False(voyage.CampaignReady);
        Assert.Equal("NOT_SENT", voyage.Delivery);
        Assert.Contains("VISUAL_QUALITY_QA_REQUIRED", voyage.Blockers);
        Assert.Contains("HUMAN_REVIEW_REQUIRED", voyage.Blockers);
        Assert.Contains("USAGE_COST_UNRECORDED", voyage.Blockers);
    }

    [Fact]
    public void Medellin_auto_parts_is_blocked_when_slot_geometry_is_missing()
    {
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var references = new List<AcademyReferenceRecord>
        {
            Active("ACA-MDE-001", 1, "auto-parts", "Automotive parts", "product texture and clean component detail", "do not copy the reference brand, package, or layout")
        };
        var campaign = new AcceptanceCampaignBrief(
            "medellin-auto-parts-missing-geometry-v1",
            "Andes Motor Supply",
            "Medellín",
            "Colombia",
            "auto-parts",
            "Promote automotive parts with clear product detail",
            product.ProductId);

        var voyage = CreativeAcceptance.Run(
            campaign,
            references,
            product,
            board.Slots,
            "OpenAI",
            "gpt-image-1",
            true);

        Assert.Equal("BLOCKED", voyage.Status);
        Assert.Equal("READY", voyage.ReferenceIntelligence.Status);
        Assert.Equal("BLOCKED_INVENTORY_GEOMETRY", voyage.ProviderJob.Status);
        Assert.Equal("GEOMETRY_UNRECORDED", voyage.InventoryPreflight.Status);
        Assert.Equal(0, voyage.ModelCalls);
        Assert.False(voyage.CampaignReady);
        Assert.Equal("NOT_SENT", voyage.Delivery);
    }

    private static AcademyReferenceRecord Active(
        string referenceId,
        int number,
        string nicheKey,
        string nicheName,
        string learn,
        string doNotCopy) =>
        new(
            referenceId,
            number,
            nicheKey,
            nicheName,
            referenceId + ".jpeg",
            ReferenceLibrary.Active,
            ReferenceLibrary.Uploaded,
            true,
            learn,
            doNotCopy);
}
