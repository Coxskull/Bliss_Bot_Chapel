using Bliss.Domain.AdvertisingRealEstate;

namespace Bliss.Tests.AdvertisingRealEstate;

public sealed class RealEstateCatalogTests
{
    [Fact]
    public void Occupancy_stays_unrecorded_until_areas_are_supplied()
    {
        var missing = RealEstateCatalog.Measure(null, null);

        Assert.False(missing.Recorded);
        Assert.Equal(RealEstateCatalog.Unrecorded, missing.Status);
        Assert.Contains("not a geometry specification", missing.Notice);

        var recorded = RealEstateCatalog.Measure(2500, 10000);
        Assert.True(recorded.Recorded);
        Assert.Equal(2500, recorded.BasisPoints);
        Assert.Contains("not a price", recorded.Notice);
    }

    [Fact]
    public void Occupancy_refuses_an_area_outside_the_sellable_specification()
    {
        var error = Assert.Throws<InvalidOperationException>(() => RealEstateCatalog.Measure(11, 10));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Draft_catalog_is_not_offered_for_sale_and_states_no_price()
    {
        var board = RealEstateCatalog.Board();
        var turn = RealEstateCatalog.Reply(
            "This is my first campaign. I want to try Alpha, but I don't want to spend very much.",
            board);

        Assert.Equal("ENTRY", turn.Intent);
        Assert.Contains("ARE-S01", turn.Reply);
        Assert.Contains("nothing is offered for sale", turn.Reply);
        Assert.Contains("will not state a number", turn.Reply);
        Assert.DoesNotContain("$", turn.Reply);
        Assert.False(turn.ShowcaseDisplayed);
        Assert.False(turn.InventedProduct);
        Assert.False(turn.InventedPrice);
        Assert.Equal(0, turn.ModelCalls);
        Assert.False(turn.CampaignReady);
        Assert.Equal("NOT_SENT", turn.Delivery);
        Assert.All(board.Products, product => Assert.Equal(RealEstateCatalog.Draft, product.Lifecycle));
        Assert.All(board.Slots, slot => Assert.Null(slot.Area));
    }

    [Fact]
    public void Premium_exclusivity_mobile_frequency_and_delivery_do_not_invent_facts()
    {
        var board = RealEstateCatalog.Board();

        var premium = RealEstateCatalog.Reply("We want a premium campaign with a much larger visual presence.", board);
        Assert.Equal("PREMIUM", premium.Intent);
        Assert.Contains("ARE-P01", string.Join(",", premium.ProductIds));
        Assert.Contains("ARE-P02", string.Join(",", premium.ProductIds));
        Assert.False(premium.ShowcaseDisplayed);

        var exclusive = RealEstateCatalog.Reply("We don't want another advertiser displayed beside us.", board);
        Assert.Equal("EXCLUSIVITY", exclusive.Intent);
        Assert.Contains("ARE-E01", exclusive.Reply);
        Assert.Contains("not promised", exclusive.Reply);

        var mobile = RealEstateCatalog.Reply("Will this same advertising configuration work on mobile?", board);
        Assert.Equal("DEVICE", mobile.Intent);
        Assert.Contains("NEEDS_REVIEW", mobile.Reply);
        Assert.Contains("will not guess", mobile.Reply);

        var frequency = RealEstateCatalog.Reply("What if I want the advertisement displayed 20 times instead of 5?", board);
        Assert.Equal("FREQUENCY", frequency.Intent);
        Assert.Equal(20, frequency.RequestedOccurrences);
        Assert.Contains("will not state a number", frequency.Reply);

        var delivery = RealEstateCatalog.Reply("How will I know my advertisement actually ran?", board);
        Assert.Equal("DELIVERY", delivery.Intent);
        Assert.Contains("will not invent impressions", delivery.Reply);
    }

    [Fact]
    public void An_unknown_configuration_and_price_are_refused()
    {
        var turn = RealEstateCatalog.Reply(
            "I want a configuration covering 70% of the podcast screen for $10.",
            RealEstateCatalog.Board());

        Assert.Equal("UNSUPPORTED", turn.Intent);
        Assert.True(turn.HumanEscalation);
        Assert.False(turn.InventedProduct);
        Assert.False(turn.InventedPrice);
        Assert.Contains("not an Inventory Product", turn.Reply);
        Assert.Contains("escalated", turn.Reply);
        Assert.DoesNotContain("70%", turn.Reply);
        Assert.Equal("NOT_SENT", turn.Delivery);
    }

    [Fact]
    public void An_active_authorized_product_can_name_its_showcase_without_a_price()
    {
        var board = RealEstateCatalog.Board(
            new Dictionary<string, bool> { ["ARE-003-V1"] = true },
            creatorAuthorized: true,
            lifecycleOverrides: new Dictionary<string, string>
            {
                ["ARE-S01"] = RealEstateCatalog.Active,
                ["ARE-003-V1"] = RealEstateCatalog.Active
            });

        var turn = RealEstateCatalog.Reply("This is my first campaign and I don't want to spend very much.", board);

        Assert.True(turn.ShowcaseDisplayed);
        Assert.Contains("ARE-S01", turn.ProductIds);
        Assert.Contains("will not state a number", turn.Reply);
        Assert.False(turn.InventedPrice);
        Assert.Equal(RealEstateCatalog.Unrecorded, turn.GeometryStatus);
    }

    [Fact]
    public void Live_adaptation_names_slots_and_does_not_scale_a_prototype()
    {
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var adaptation = RealEstateCatalog.Adapt(product, board.Slots, false);

        Assert.Equal("GEOMETRY_UNRECORDED", adaptation.Status);
        Assert.Equal(["LEFT_VERTICAL", "BOTTOM_FULL"], adaptation.Slots.Select(item => item.SlotId).ToArray());
        Assert.All(adaptation.Slots, slot => Assert.Null(slot.Width));
        Assert.All(adaptation.Slots, slot => Assert.Null(slot.Height));
        Assert.False(adaptation.ScaledFromPrototype);
        Assert.Equal(0, adaptation.ModelCalls);
        Assert.False(adaptation.CampaignReady);
        Assert.Equal("NOT_SENT", adaptation.Delivery);
        Assert.Contains("not scaled", adaptation.Notice);
        Assert.Contains("unrecorded", adaptation.Notice);
    }

    [Fact]
    public void Recorded_slot_geometry_is_used_exactly_and_area_is_not_calculated()
    {
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();

        var adaptation = RealEstateCatalog.Adapt(product, slots, false);

        Assert.Equal("RECOMPOSED", adaptation.Status);
        Assert.Equal(180, adaptation.Slots[0].Width);
        Assert.Equal(640, adaptation.Slots[0].Height);
        Assert.Equal(1280, adaptation.Slots[1].Width);
        Assert.Equal(160, adaptation.Slots[1].Height);
        Assert.False(adaptation.ScaledFromPrototype);
        Assert.Contains("Area was not calculated", adaptation.Notice);
    }

    [Fact]
    public void A_missing_slot_dimension_keeps_the_whole_adaptation_unrecorded()
    {
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var slots = board.Slots.Select(slot =>
            slot.SlotId == "LEFT_VERTICAL" ? slot with { Width = 180, Height = 640 } : slot).ToList();

        var adaptation = RealEstateCatalog.Adapt(product, slots, false);

        Assert.Equal("GEOMETRY_UNRECORDED", adaptation.Status);
        Assert.Null(adaptation.Slots.Single(item => item.SlotId == "BOTTOM_FULL").Width);
        Assert.Equal(180, adaptation.Slots.Single(item => item.SlotId == "LEFT_VERTICAL").Width);
    }

    [Fact]
    public void Scaling_a_prototype_or_an_unknown_slot_is_refused()
    {
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var scaled = Assert.Throws<InvalidOperationException>(() => RealEstateCatalog.Adapt(product, board.Slots, true));
        Assert.Equal(
            "The Academy prototype was not scaled into the placement. Prototype dimensions are not podcast-advertising dimensions.",
            scaled.Message);

        var invented = product with { SlotIds = ["INVENTED_SLOT"] };
        var missing = Assert.Throws<InvalidOperationException>(() => RealEstateCatalog.Adapt(invented, board.Slots, false));
        Assert.Contains("None was invented", missing.Message);
    }
}
