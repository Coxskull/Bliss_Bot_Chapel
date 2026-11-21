using Bliss.Domain.AdvertisingRealEstate;

namespace Bliss.Tests.AdvertisingRealEstate;

/// <summary>
/// ALPHA Directive 345 — fictional/offline commercial evidence only.
/// Never authorize live payments, inventory reservation, delivery or outreach.
/// </summary>
public sealed class CommercialInventoryReviewGateTests
{
    private const string OwnerGeometry = """
        slotId	version	canvasWidth	canvasHeight	width	height	originX	originY	area	basis	approvedOn	approvingAuthority
        LEFT_VERTICAL	G1	1920	1080	320	1080	0	0	345600	Synthetic owner design	2026-11-07	Offline fixture
        RIGHT_VERTICAL	G1	1920	1080	320	1080	1600	0	345600	Synthetic owner design	2026-11-07	Offline fixture
        TOP_LEFT	G1	1920	1080	640	180	320	0	115200	Synthetic owner design	2026-11-07	Offline fixture
        TOP_RIGHT	G1	1920	1080	640	180	960	0	115200	Synthetic owner design	2026-11-07	Offline fixture
        BOTTOM_LEFT	G1	1920	1080	640	180	320	900	115200	Synthetic owner design	2026-11-07	Offline fixture
        BOTTOM_RIGHT	G1	1920	1080	640	180	960	900	115200	Synthetic owner design	2026-11-07	Offline fixture
        BOTTOM_FULL	G1	1920	1080	1280	180	320	900	230400	Synthetic owner design	2026-11-07	Offline fixture
        """;

    private static readonly IReadOnlyDictionary<string, OwnerSlotGeometry> Geometry =
        OwnerGeometryFile.Read(OwnerGeometry);

    private static readonly ProtectedCreatorRegion[] Protected =
        [new("G1", 320, 180, 1280, 720)];

    private static CatalogBoard Board(bool active = true, bool authorized = true) =>
        RealEstateCatalog.Board(
            assets: new Dictionary<string, bool> { ["ARE-001-V1"] = true },
            creatorAuthorized: authorized,
            lifecycleOverrides: active ? new Dictionary<string, string>
            {
                ["ARE-P01"] = RealEstateCatalog.Active,
                ["ARE-001-V1"] = RealEstateCatalog.Active
            } : null,
            deviceStatus: "OWNER_APPROVED",
            platformStatus: "OWNER_APPROVED",
            geometry: Geometry);

    private static CreatorProductProof Creator() =>
        new("ARE-P01", "V1", "fictional-creator", "AUTH-FICTIONAL-V1", "synthetic://owner/rights", true);

    private static InventoryAvailabilityProof Availability() =>
        new("ARE-P01", "V1", "AVAILABLE", 1, "synthetic://availability", "AVAIL-V1");

    private static DevicePlatformProof Compatibility() =>
        new("podcast", "desktop", "DEVICE-V1", "SUPPORTED");

    private static ShowcaseAssetProof Showcase() =>
        new("ARE-001-V1", "V1", new string('a', 64), "synthetic://asset-digest", true);

    private static CommercialInventoryReview Evaluate(
        CatalogBoard? board = null,
        IReadOnlyDictionary<string, OwnerSlotGeometry>? geometry = null,
        ProtectedCreatorRegion[]? protectedRegions = null,
        long? sellable = 1152000,
        CreatorProductProof? creator = null,
        InventoryAvailabilityProof? availability = null,
        DevicePlatformProof? compatibility = null,
        ShowcaseAssetProof? showcase = null,
        int occurrences = 5,
        bool exclusive = true) =>
        CommercialInventoryReviewGate.Evaluate(
            board ?? Board(), "ARE-P01", geometry ?? Geometry,
            protectedRegions ?? Protected, sellable,
            creator ?? Creator(), availability ?? Availability(),
            compatibility ?? Compatibility(), showcase ?? Showcase(), occurrences, exclusive);

    [Fact]
    public void Geometry_union_is_exact_even_when_rectangles_overlap()
    {
        Assert.Equal(150L, CommercialGeometryGuard.UnionArea(
            [(0, 0, 10, 10), (5, 0, 10, 10)]));
        Assert.Equal(0L, CommercialGeometryGuard.UnionArea([]));
        Assert.Equal(200L, CommercialGeometryGuard.UnionArea(
            [(0, 0, 10, 10), (10, 0, 10, 10)]));
    }

    [Fact]
    public void Geometry_is_versioned_and_occupancy_uses_declared_frame_and_sellable_bases()
    {
        var product = Board().Products.Single(p => p.ProductId == "ARE-P01");
        var check = CommercialGeometryGuard.Review(product, Geometry, Protected, 1152000);
        Assert.True(check.Validated);
        Assert.Equal("G1", check.GeometryVersion);
        Assert.Equal(576000L, check.UnionAdvertiserArea);
        Assert.Equal(2073600L, check.ReferenceFrameArea);
        Assert.Equal(2778, check.FrameOccupancyBasisPoints);
        Assert.Equal(5000, check.SellableOccupancyBasisPoints);
        Assert.NotEqual(check.FrameOccupancyBasisPoints, check.SellableOccupancyBasisPoints);
    }

    [Fact]
    public void Incomplete_or_mixed_owner_geometry_and_unknown_sellable_basis_fail_closed()
    {
        var product = Board().Products.Single(p => p.ProductId == "ARE-P01");
        Assert.Equal("GEOMETRY_UNVERIFIED",
            CommercialGeometryGuard.Review(product, null, Protected, 1152000).Status);
        Assert.Equal("GEOMETRY_UNVERIFIED",
            CommercialGeometryGuard.Review(product, Geometry, [], 1152000).Status);
        Assert.Equal("SELLABLE_BASIS_INVALID",
            CommercialGeometryGuard.Review(product, Geometry, Protected).Status);

        var wrongVersion = Geometry.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        wrongVersion["BOTTOM_FULL"] = wrongVersion["BOTTOM_FULL"] with { Version = "G2" };
        Assert.Equal("GEOMETRY_UNVERIFIED",
            CommercialGeometryGuard.Review(product, wrongVersion, Protected, 1152000).Status);

        var unknownArea = Geometry.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        unknownArea["BOTTOM_FULL"] = unknownArea["BOTTOM_FULL"] with { Area = 2 };
        Assert.Equal("GEOMETRY_INVALID",
            CommercialGeometryGuard.Review(product, unknownArea, Protected, 1152000).Status);
    }

    [Fact]
    public void Owner_creator_perimeter_is_preserved_and_overlapping_advertiser_slots_are_refused()
    {
        var product = Board().Products.Single(p => p.ProductId == "ARE-P01");
        Assert.Equal("CREATOR_CONTENT_OVERLAP",
            CommercialGeometryGuard.Review(product, Geometry,
                [new ProtectedCreatorRegion("G1", 0, 0, 100, 100)], 1152000).Status);

        var doubleSold = product with { SlotIds = ["BOTTOM_FULL", "BOTTOM_LEFT"] };
        Assert.Equal("UNAUTHORIZED_SLOT_OVERLAP",
            CommercialGeometryGuard.Review(doubleSold, Geometry, Protected, 1152000).Status);

        Assert.Equal("PROTECTED_REGION_UNVERIFIED",
            CommercialGeometryGuard.Review(product, Geometry,
                [new ProtectedCreatorRegion("G2", 320, 180, 1280, 720)], 1152000).Status);
    }

    [Fact]
    public void Synthetic_authoritative_chain_reaches_AskAlpha_but_stops_at_real_Economics_quote()
    {
        var turn = Evaluate();
        Assert.Equal("OFFLINE_SYNTHETIC_ONLY", turn.Mode);
        Assert.Equal("NEEDS_REVIEW", turn.AvailabilityStatus);
        Assert.Equal("ARE-P01", turn.ProductId);
        Assert.Equal("V1", turn.ProductVersion);
        Assert.Equal("ARE-001-V1", turn.ShowcaseId);
        Assert.True(turn.ShowcaseAllowed);
        Assert.Equal("GEOMETRY_VALIDATED", turn.Geometry?.Status);
        Assert.Equal("PRICING_UNAVAILABLE", turn.PricingStatus);
        Assert.True(turn.HumanEscalation);
        Assert.False(turn.ReservationAuthorized);
        Assert.False(turn.CommitmentAuthorized);
        Assert.False(turn.DeliveryVerified);
        Assert.Equal("NOT_SENT", turn.DeliveryStatus);
        Assert.DoesNotContain("$", turn.SafeReply);
        Assert.Equal("PRICING_UNAVAILABLE",
            turn.Trace.Single(t => t.Step == "EconomicsQuoteLookup").Result);
        Assert.Equal("HUMAN_ESCALATION_REQUIRED",
            turn.Trace.Single(t => t.Step == "NegotiationBoundaryValidation").Result);
    }

    [Fact]
    public void Missing_creator_authorization_or_wrong_version_blocks_lookup()
    {
        Assert.Equal("NEEDS_REVIEW", Evaluate(board: Board(authorized: false)).AvailabilityStatus);
        Assert.Equal("NEEDS_REVIEW",
            CommercialInventoryReviewGate.Evaluate(
                Board(), "ARE-P01", Geometry, Protected, 1152000,
                Creator() with { ProductVersion = "V2" }, Availability(), Compatibility(), Showcase()).AvailabilityStatus);
    }

    [Fact]
    public void Draft_inventory_and_unknown_identity_are_not_offered()
    {
        Assert.Equal("UNAVAILABLE", Evaluate(board: Board(active: false)).AvailabilityStatus);
        var unknown = CommercialInventoryReviewGate.Evaluate(Board(), "ARE-NEW-INVENTED", Geometry,
            Protected, 1152000, Creator(), Availability(), Compatibility(), Showcase());
        Assert.Equal("UNKNOWN", unknown.AvailabilityStatus);
        Assert.False(unknown.ShowcaseAllowed);
        Assert.False(unknown.CommitmentAuthorized);
    }

    [Fact]
    public void Exclusivity_and_capacity_are_bound_to_existing_product_configuration()
    {
        var shared = Board().Products.Single(x => x.ProductId == "ARE-P01") with
        {
            MaximumAdvertisers = 2,
            Exclusivity = "SHARED"
        };
        var basis = Board();
        var modified = basis with { Products = [shared] };
        Assert.Equal("UNAVAILABLE", Evaluate(board: modified).AvailabilityStatus);
        Assert.Equal("UNAVAILABLE", Evaluate(availability: Availability() with
        {
            AvailableAdvertiserPlaces = 0
        }).AvailabilityStatus);
    }

    [Fact]
    public void Mobile_is_not_guessed_from_desktop_and_unsupported_platform_is_held()
    {
        var mobile = CommercialInventoryReviewGate.Evaluate(Board(), "ARE-P01", Geometry,
            Protected, 1152000, Creator(), Availability(),
            Compatibility() with { Device = "mobile" }, Showcase());
        Assert.Equal("NEEDS_REVIEW", mobile.AvailabilityStatus);
        Assert.Equal("PlatformCompatibilityFilter", mobile.Trace.Last().Step);

        var unknown = CommercialInventoryReviewGate.Evaluate(Board(), "ARE-P01", Geometry,
            Protected, 1152000, Creator(), Availability(),
            Compatibility() with { State = "UNKNOWN" }, Showcase());
        Assert.Equal("NEEDS_REVIEW", unknown.AvailabilityStatus);
    }

    [Fact]
    public void Five_vs_twenty_occurrences_keep_distinct_requests_but_neither_is_priced()
    {
        var five = Evaluate(occurrences: 5);
        var twenty = Evaluate(occurrences: 20);
        Assert.Contains("5.", five.Trace.Single(t => t.Step == "EconomicsQuoteLookup").Reference);
        Assert.Contains("20.", twenty.Trace.Single(t => t.Step == "EconomicsQuoteLookup").Reference);
        Assert.Equal("PRICING_UNAVAILABLE", five.PricingStatus);
        Assert.Equal("PRICING_UNAVAILABLE", twenty.PricingStatus);
    }

    [Fact]
    public void Missing_asset_or_unapproved_showcase_cannot_act_as_inventory_authority()
    {
        Assert.Equal("NEEDS_REVIEW", CommercialInventoryReviewGate.Evaluate(Board(),
            "ARE-P01", Geometry, Protected, 1152000, Creator(), Availability(), Compatibility(),
            Showcase() with { Approved = false }).AvailabilityStatus);
        Assert.Equal("NEEDS_REVIEW", CommercialInventoryReviewGate.Evaluate(Board(),
            "ARE-P01", Geometry, Protected, 1152000, Creator(), Availability(), Compatibility(),
            Showcase() with { Sha256 = "not-a-digest" }).AvailabilityStatus);
    }

    [Fact]
    public void No_synthetic_display_is_real_delivery_or_reservation()
    {
        var result = Evaluate();
        Assert.False(result.DeliveryVerified);
        Assert.False(result.ReservationAuthorized);
        Assert.False(result.CommitmentAuthorized);
        Assert.Contains("Synthetic reference only", result.SafeReply);
    }

    [Fact]
    public void Existing_AskAlpha_customer_intent_reaches_stored_premium_product_but_not_a_price_or_commitment()
    {
        var turn = CommercialInventoryReviewGate.SimulateConversation(
            Board(), "We want a premium campaign with a larger visual presence.",
            "ARE-P01", Geometry, Protected, 1152000,
            Creator(), Availability(), Compatibility(), Showcase());

        Assert.Equal("PREMIUM", turn.Intent);
        Assert.Equal("ARE-P01", turn.SelectedProductId);
        Assert.Equal("OFFLINE_SYNTHETIC_ONLY", turn.Mode);
        Assert.Equal("NEEDS_REVIEW", turn.Status);
        Assert.Contains(turn.Trace, step => step.Step == "CustomerIntent" && step.Result == "PREMIUM");
        Assert.Contains(turn.Trace, step => step.Step == "GeometryVariantResolution" && step.Result == "PASS_SYNTHETIC");
        Assert.Contains(turn.Trace, step => step.Step == "EconomicsQuoteLookup" && step.Result == "PRICING_UNAVAILABLE");
        Assert.False(turn.InventedProduct);
        Assert.False(turn.InventedPrice);
        Assert.False(turn.CampaignReady);
        Assert.True(turn.HumanEscalation);
        Assert.Equal("NOT_SENT", turn.DeliveryStatus);
        Assert.StartsWith("OFFLINE FIXTURE ONLY", turn.Transcript);
        Assert.DoesNotContain("$", turn.Transcript);
    }

    [Fact]
    public void Existing_AskAlpha_rejects_unmatched_or_unsupported_product_requests_before_evidence_lookup()
    {
        var unsupported = CommercialInventoryReviewGate.SimulateConversation(
            Board(), "I want 70% of the screen for $10.", "ARE-P01",
            Geometry, Protected, 1152000,
            Creator(), Availability(), Compatibility(), Showcase());
        Assert.Equal("UNSUPPORTED", unsupported.Intent);
        Assert.Null(unsupported.ProductReview);
        Assert.Equal("NEEDS_REVIEW", unsupported.Status);
        Assert.False(unsupported.InventedPrice);
        Assert.Equal("NOT_SENT", unsupported.DeliveryStatus);

        var unknown = CommercialInventoryReviewGate.SimulateConversation(
            Board(), "We want premium visual presence.", "INVENTED_PRODUCT",
            Geometry, Protected, 1152000,
            Creator(), Availability(), Compatibility(), Showcase());
        Assert.Equal("PREMIUM", unknown.Intent);
        Assert.Null(unknown.ProductReview);
        Assert.Null(unknown.SelectedProductId);
        Assert.True(unknown.HumanEscalation);
    }

    [Fact]
    public void Unsupported_screen_percentage_and_ten_dollar_offer_is_escalated_by_existing_AskAlpha_catalog()
    {
        var result = RealEstateCatalog.Reply(
            "I want 70% of the podcast screen for $10.", RealEstateCatalog.Board());
        Assert.Equal("UNSUPPORTED", result.Intent);
        Assert.True(result.HumanEscalation);
        Assert.False(result.InventedPrice);
        Assert.False(result.InventedProduct);
        Assert.False(result.CampaignReady);
    }
}
