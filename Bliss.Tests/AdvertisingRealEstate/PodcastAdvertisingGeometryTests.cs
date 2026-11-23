using Bliss.Domain.AdvertisingRealEstate;

namespace Bliss.Tests.AdvertisingRealEstate;

public sealed class PodcastAdvertisingGeometryTests
{
    [Theory]
    [InlineData(2, 2500, 7500)]
    [InlineData(4, 3333, 6667)]
    [InlineData(6, 4740, 4792)]
    public void Canonical_layouts_have_correct_dimensions_and_calculated_area(
        int rectangleCount, int expectedAdCoverageBasisPoints, int expectedCreatorCoverageBasisPoints)
    {
        var layout = PodcastAdvertisingGeometry.Get(rectangleCount);

        Assert.Equal("ARE-PODCAST-16X9-V2", layout.Version);
        Assert.Equal(1920, layout.CanvasWidth);
        Assert.Equal(1080, layout.CanvasHeight);
        Assert.Equal(rectangleCount, layout.RectangleCount);
        Assert.Equal(rectangleCount, layout.AdSlots.Count);
        Assert.Equal(expectedAdCoverageBasisPoints, layout.AdCoverageBasisPoints);
        Assert.Equal(expectedCreatorCoverageBasisPoints, layout.ProtectedCreatorBasisPoints);
        Assert.False(layout.RendererIntegrated);
        Assert.Equal(PodcastAdvertisingGeometry.Status, layout.Status);

        var totalArea = 0;
        foreach (var slot in layout.AdSlots)
        {
            Assert.InRange(slot.X, 0, 1919);
            Assert.InRange(slot.Y, 0, 1079);
            Assert.InRange(slot.Right, 1, 1920);
            Assert.InRange(slot.Bottom, 1, 1080);
            Assert.True(slot.Area > 0);
            Assert.False(Overlaps(slot, layout.ProtectedCreatorRegion));
            totalArea += slot.Area;
        }
        Assert.Equal(rectangleCount, layout.AdSlots.Select(slot => slot.SlotId).Distinct().Count());
        for (var i = 0; i < rectangleCount; i++)
        {
            for (var j = i + 1; j < rectangleCount; j++)
            {
                Assert.False(Overlaps(layout.AdSlots[i], layout.AdSlots[j]));
            }
        }

        Assert.InRange(totalArea + layout.ProtectedCreatorRegion.Area, 1, 1920 * 1080);
    }

    [Fact]
    public void Two_slot_layout_has_exact_split_bottom_ad_dimensions_and_creator_safe_region()
    {
        var layout = PodcastAdvertisingGeometry.Get(2);
        Assert.Equal(new PodcastAdvertisingRect("BOTTOM_LEFT", 0, 810, 960, 270), layout.AdSlots[0]);
        Assert.Equal(new PodcastAdvertisingRect("BOTTOM_RIGHT", 960, 810, 960, 270), layout.AdSlots[1]);
        Assert.Equal(new PodcastAdvertisingRect("CREATOR_SAFE", 0, 0, 1920, 810), layout.ProtectedCreatorRegion);
    }

    [Fact]
    public void Four_slot_layout_places_top_and_bottom_at_exact_edges()
    {
        var layout = PodcastAdvertisingGeometry.Get(4);
        Assert.Equal(new PodcastAdvertisingRect("TOP_LEFT", 0, 0, 960, 180), layout.AdSlots[0]);
        Assert.Equal(new PodcastAdvertisingRect("TOP_RIGHT", 960, 0, 960, 180), layout.AdSlots[1]);
        Assert.Equal(new PodcastAdvertisingRect("BOTTOM_LEFT", 0, 900, 960, 180), layout.AdSlots[2]);
        Assert.Equal(new PodcastAdvertisingRect("BOTTOM_RIGHT", 960, 900, 960, 180), layout.AdSlots[3]);
        Assert.Equal(new PodcastAdvertisingRect("CREATOR_SAFE", 0, 180, 1920, 720), layout.ProtectedCreatorRegion);
    }

    [Fact]
    public void Six_slot_layout_has_horizontal_and_side_ads_with_protected_center()
    {
        var layout = PodcastAdvertisingGeometry.Get(6);
        Assert.Equal(new PodcastAdvertisingRect("TOP_LEFT", 0, 0, 960, 180), layout.AdSlots[0]);
        Assert.Equal(new PodcastAdvertisingRect("TOP_RIGHT", 960, 0, 960, 180), layout.AdSlots[1]);
        Assert.Equal(new PodcastAdvertisingRect("LEFT_SIDE", 0, 270, 270, 540), layout.AdSlots[2]);
        Assert.Equal(new PodcastAdvertisingRect("RIGHT_SIDE", 1650, 270, 270, 540), layout.AdSlots[3]);
        Assert.Equal(new PodcastAdvertisingRect("BOTTOM_LEFT", 0, 900, 960, 180), layout.AdSlots[4]);
        Assert.Equal(new PodcastAdvertisingRect("BOTTOM_RIGHT", 960, 900, 960, 180), layout.AdSlots[5]);
        Assert.Equal(new PodcastAdvertisingRect("CREATOR_SAFE", 270, 180, 1380, 720), layout.ProtectedCreatorRegion);
    }

    [Theory]
    [InlineData(2, "BOTTOM_LEFT", 960, 270)]
    [InlineData(4, "TOP_RIGHT", 960, 180)]
    [InlineData(6, "LEFT_SIDE", 270, 540)]
    [InlineData(6, "RIGHT_SIDE", 270, 540)]
    [InlineData(6, "BOTTOM_RIGHT", 960, 180)]
    public void Advertiser_template_requires_exact_slot_pixels(
        int rectangleCount, string slot, int expectedWidth, int expectedHeight)
    {
        var result = PodcastAdvertisingGeometry.ValidateAdvertiserTemplate(
            rectangleCount, slot, expectedWidth, expectedHeight);
        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);

        Assert.Throws<InvalidOperationException>(() =>
            PodcastAdvertisingGeometry.ValidateAdvertiserTemplate(rectangleCount, slot, expectedWidth - 1, expectedHeight));
        Assert.Throws<InvalidOperationException>(() =>
            PodcastAdvertisingGeometry.ValidateAdvertiserTemplate(rectangleCount, slot, expectedWidth, expectedHeight + 1));
    }

    [Fact]
    public void Rejects_unknown_layout_slots_and_nonstandard_video_dimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PodcastAdvertisingGeometry.Get(3));
        Assert.Throws<ArgumentException>(() =>
            PodcastAdvertisingGeometry.ValidateAdvertiserTemplate(2, "LEFT_SIDE", 270, 540));
        PodcastAdvertisingGeometry.ValidateRenderedCanvas(1920, 1080);
        Assert.Throws<InvalidOperationException>(() => PodcastAdvertisingGeometry.ValidateRenderedCanvas(1280, 720));
        Assert.Throws<InvalidOperationException>(() => PodcastAdvertisingGeometry.ValidateRenderedCanvas(1672, 941));
    }

    private static bool Overlaps(PodcastAdvertisingRect a, PodcastAdvertisingRect b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}
