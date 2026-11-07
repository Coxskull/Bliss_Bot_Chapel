using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.AdvertisingRealEstate;

public sealed class SyntheticCommercialDeliveryEvidenceTests
{
    private static CommercialInventoryReview Inventory() => new(
        "OFFLINE_SYNTHETIC_ONLY", "NEEDS_REVIEW", "ARE-P01", "V1",
        "ARE-001-V1", new CommercialGeometryEvidence(
            "GEOMETRY_VALIDATED", "G1", 576000, 2073600, 2778, 5000, []),
        "PRICING_UNAVAILABLE", true, true, false, false, false,
        "NOT_SENT", "Fictional quote withheld", []);

    private static CommercialCreativeQaDecision Creative() => new(
        "OFFLINE_SYNTHETIC_ONLY", "PASS_SYNTHETIC", "PASS_SYNTHETIC",
        "PASS_SYNTHETIC", "PASS_SYNTHETIC", "PASS_SYNTHETIC",
        true, false, false, "NOT_SENT", new string('a', 64),
        "FICTIONAL_BRAND_DNA/V3", "FICTIONAL_REFERENCE", [], "Fictional preview only");

    private static SyntheticCommercialDeliveryReceipt Run(
        CommercialInventoryReview? inventory = null,
        CommercialCreativeQaDecision? creative = null, string state = "DISPLAY_VERIFIED") =>
        SyntheticCommercialDeliveryEvidence.Simulate(inventory ?? Inventory(),
            creative ?? Creative(), "FICTIONAL_CREATOR", "FICTIONAL_ADVERTISER",
            15, 5, state);

    [Fact]
    public void T18_requested_synthetic_display_verified_never_becomes_actual_impression()
    {
        var receipt = Run();
        Assert.Equal("OFFLINE_SYNTHETIC_ONLY", receipt.Mode);
        Assert.StartsWith("SYNTHETIC-", receipt.ReceiptId);
        Assert.Equal("DISPLAY_VERIFIED", receipt.AttemptedStage);
        Assert.Equal("NOT_VERIFIED", receipt.VerifiedStage);
        Assert.Equal(0, receipt.RealObservedOccurrences);
        Assert.False(receipt.ActualDisplayVerified);
        Assert.False(receipt.ReservationAuthorized);
        Assert.False(receipt.PaymentAuthorized);
        Assert.False(receipt.ExternalUseAuthorized);
        Assert.Equal("NOT_SENT", receipt.DeliveryStatus);
        Assert.Contains(receipt.Exceptions, x => x.Contains("SYNTHETIC"));
    }

    [Fact]
    public void T20_historical_versions_are_bound_to_immutable_distinct_snapshot_digests()
    {
        var old = Run();
        var newProduct = Run(inventory: Inventory() with { ProductVersion = "V2" });
        var newGeometry = Run(inventory: Inventory() with {
            Geometry = Inventory().Geometry! with { GeometryVersion = "G2" }
        });
        var newCreative = Run(creative: Creative() with {
            CandidateSha256 = new string('b', 64)
        });
        Assert.Equal("V1", old.ProductVersion);
        Assert.Equal("G1", old.GeometryVersion);
        Assert.NotEqual(old.SnapshotSha256, newProduct.SnapshotSha256);
        Assert.NotEqual(old.SnapshotSha256, newGeometry.SnapshotSha256);
        Assert.NotEqual(old.SnapshotSha256, newCreative.SnapshotSha256);
        Assert.Equal(64, old.SnapshotSha256.Length);
        Assert.Equal(old.SnapshotSha256, Run().SnapshotSha256);
    }

    [Theory]
    [InlineData("RESERVED")]
    [InlineData("SCHEDULED")]
    [InlineData("CREATIVE_APPROVED")]
    [InlineData("RENDERED")]
    [InlineData("DISPLAY_STARTED")]
    [InlineData("DISPLAY_VERIFIED")]
    [InlineData("PARTIALLY_DELIVERED")]
    [InlineData("DELIVERED")]
    [InlineData("FAILED")]
    [InlineData("DISPUTED")]
    public void All_requested_lifecycle_stages_remain_unverified_in_synthetic_mode(string attempted)
    {
        var receipt = Run(state: attempted);
        Assert.Equal(attempted, receipt.AttemptedStage);
        Assert.Equal("NOT_VERIFIED", receipt.VerifiedStage);
        Assert.Equal("NOT_SENT", receipt.DeliveryStatus);
        Assert.Equal(0, receipt.RealObservedOccurrences);
        Assert.False(receipt.ExternalUseAuthorized);
    }

    [Fact]
    public void Live_mode_and_invalid_stages_or_missing_version_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Run(inventory: Inventory() with { Mode = "LIVE" }));
        Assert.Throws<InvalidOperationException>(() =>
            Run(creative: Creative() with { Mode = "LIVE" }));
        Assert.Throws<ArgumentException>(() => Run(state: "ACTUALLY_DELIVERED"));
        Assert.Throws<InvalidOperationException>(() =>
            Run(inventory: Inventory() with { ProductVersion = "" }));
    }
}
