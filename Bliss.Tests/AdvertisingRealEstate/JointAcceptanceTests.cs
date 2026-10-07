using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.AdvertisingRealEstate;

public sealed class JointAcceptanceTests
{
    [Fact]
    public void Live_catalog_blocks_joint_acceptance_without_inventing_facts()
    {
        var reading = JointAcceptance.Read(RealEstateCatalog.Board(), [], null, null, 18);

        Assert.Equal(JointAcceptance.Blocked, reading.Status);
        Assert.Equal(RealEstateCatalog.AmendmentOpen, reading.CatalogAmendment);
        Assert.Equal(CreativeAcceptance.Open, reading.AcademyAmendment);
        Assert.Equal(JointAcceptance.Unclaimed, reading.HostedAcceptance);
        Assert.Equal(7, reading.Conversations.Count);
        Assert.All(reading.Conversations, item => Assert.True(item.IntegrityPassed));
        Assert.All(reading.Conversations, item => Assert.False(item.InventedProduct));
        Assert.All(reading.Conversations, item => Assert.False(item.InventedPrice));
        Assert.Contains(reading.Conversations, item => item.CaseId == "ARE-11-1" && !item.AcceptancePassed && !item.ShowcaseDisplayed);
        Assert.Contains(reading.Conversations, item => item.CaseId == "ARE-11-4" && item.AcceptancePassed);
        Assert.Contains(reading.Conversations, item => item.CaseId == "ARE-11-5" && item.RequestedOccurrences == 20 && item.AcceptancePassed);
        Assert.Contains(reading.Conversations, item => item.CaseId == "ARE-11-6" && item.AcceptancePassed);
        Assert.Contains(reading.Conversations, item => item.CaseId == "ARE-11-7" && item.AcceptancePassed && item.HumanEscalation);
        Assert.Equal("NO_AUTHORIZED_PRICE", reading.Gates.Single(item => item.GateId == "ECONOMICS").Status);
        Assert.Equal("NICHE_REFERENCE_NOT_ACTIVE", reading.RetrievalStatus);
        Assert.Equal("GEOMETRY_UNRECORDED", reading.AdaptationStatus);
        Assert.Equal("ARE-P01", reading.AdaptedProductId);
        Assert.Equal("UNRECORDED", reading.Gates.Single(item => item.GateId == "AVAILABILITY").Status);
        Assert.Equal("OPEN", reading.Gates.Single(item => item.GateId == "OWNER_INPUTS").Status);
        Assert.Equal("UNRECORDED", reading.MissionControl.Single(item => item.ProductId == "ARE-P01").Disclosure);
        Assert.Equal("NOT_RECORDED", reading.MissionControl.Single(item => item.ProductId == "ARE-P01").ProofOfDelivery);
        Assert.DoesNotContain(reading.Conversations, item => item.Notice.Contains('$'));
        Assert.Equal(0, reading.ModelCalls);
        Assert.False(reading.CampaignReady);
        Assert.Equal("NOT_SENT", reading.Delivery);
    }

    [Fact]
    public void Supplied_facts_wait_for_owner_review_without_closing_the_amendments()
    {
        var board = RealEstateCatalog.Board(
            new Dictionary<string, bool>
            {
                ["ARE-001-V1"] = true,
                ["ARE-002-V1"] = true,
                ["ARE-003-V1"] = true
            },
            creatorAuthorized: true,
            economics: "ECONOMICS-RULE-1",
            lifecycleOverrides: new Dictionary<string, string>
            {
                ["ARE-P01"] = RealEstateCatalog.Active,
                ["ARE-P02"] = RealEstateCatalog.Active,
                ["ARE-S01"] = RealEstateCatalog.Active,
                ["ARE-E01"] = RealEstateCatalog.Active,
                ["ARE-001-V1"] = RealEstateCatalog.Active,
                ["ARE-002-V1"] = RealEstateCatalog.Active,
                ["ARE-003-V1"] = RealEstateCatalog.Active
            });
        board = board with
        {
            Products = board.Products.Select(item => item with
            {
                DeviceStatus = "RECORDED",
                PlatformStatus = "RECORDED"
            }).ToList()
        };
        var slots = board.Slots.Select(slot => slot.SlotId is "LEFT_VERTICAL" or "BOTTOM_FULL"
            ? slot with { Width = slot.SlotId == "LEFT_VERTICAL" ? 180 : 1280, Height = slot.SlotId == "LEFT_VERTICAL" ? 640 : 160 }
            : slot).ToList();
        var references = new List<AcademyReferenceRecord>
        {
            Active("ACA-001-V1", "pharmacy", "Pharmacy", "premium product lighting", "VidaCare"),
            Active("ACA-006-V1", "restaurant", "Restaurant", "lighting and depth", "exact bay"),
            Active("ACA-008-V1", "auto-repair", "Auto repair", "typography", "reference headline")
        };

        var reading = JointAcceptance.Read(board, references, slots, "stored availability", 0);

        Assert.Equal(JointAcceptance.AwaitingReview, reading.Status);
        Assert.Equal(RealEstateCatalog.AmendmentOpen, reading.CatalogAmendment);
        Assert.Equal(CreativeAcceptance.Open, reading.AcademyAmendment);
        Assert.Equal(JointAcceptance.Unclaimed, reading.HostedAcceptance);
        Assert.All(reading.Conversations, item => Assert.True(item.AcceptancePassed));
        Assert.Equal(3, reading.RetrievedReferences.Count);
        Assert.Equal("RETRIEVED", reading.RetrievalStatus);
        Assert.Equal("RECOMPOSED", reading.AdaptationStatus);
        Assert.Equal("RECORDED", reading.Gates.Single(item => item.GateId == "ECONOMICS").Status);
        Assert.Equal("RECORDED", reading.Gates.Single(item => item.GateId == "AVAILABILITY").Status);
        Assert.False(reading.CampaignReady);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal(0, reading.ModelCalls);
    }

    private static AcademyReferenceRecord Active(
        string referenceId,
        string nicheKey,
        string nicheName,
        string learn,
        string doNotCopy) =>
        new(
            referenceId,
            1,
            nicheKey,
            nicheName,
            referenceId + ".jpeg",
            ReferenceLibrary.Active,
            ReferenceLibrary.Uploaded,
            true,
            learn,
            doNotCopy);
}
