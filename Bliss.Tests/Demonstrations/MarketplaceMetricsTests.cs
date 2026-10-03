using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class MarketplaceMetricsTests
{
    [Fact]
    public void A_marketplace_reading_stores_measured_counts_and_leaves_revenue_unrecorded()
    {
        var reading = MarketplaceMetrics.Store(
            "metrics-reading-1",
            ["Harbor Audio Labs", "Sunrise Wellness Co.", "TEST Dental Manila", "TEST Restaurant Santo Domingo"],
            ["Test Creator", "Test Creator Brazil", "Unknown Demographics Creator"],
            8,
            0,
            null,
            false,
            []);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal(4, reading.AdvertiserCount);
        Assert.Equal(3, reading.CreatorCount);
        Assert.Equal(8, reading.SlotCount);
        Assert.Equal(0, reading.RevenueRowCount);
        Assert.Equal("Stored advertiser pressure is ahead of stored creator pressure.", reading.Pressure);
        Assert.Equal(MarketplaceMetrics.RevenueUnrecorded, reading.RevenueLine);
        Assert.False(reading.CensusClaimed);
        Assert.False(reading.RevenueRecorded);
        Assert.False(reading.SlotsChanged);
        Assert.False(reading.Duplicate);
        Assert.Contains("not a census", reading.Notice);
        Assert.Contains("not on file", reading.Notice);
    }

    [Fact]
    public void An_empty_marketplace_stores_zero_and_a_repeat_does_not_raise_the_count()
    {
        var empty = MarketplaceMetrics.Store("metrics-reading-1", [], [], 0, 0, null, false, []);
        Assert.Equal(0, empty.AdvertiserCount);
        Assert.Equal(0, empty.CreatorCount);
        Assert.Equal(0, empty.SlotCount);

        var again = MarketplaceMetrics.Store(
            "metrics-reading-1",
            ["Harbor Audio Labs"],
            ["Test Creator"],
            8,
            0,
            null,
            false,
            [new MetricRecord("metrics-reading-1", 0, 0, 0, 0)]);
        Assert.True(again.Duplicate);
        Assert.Equal(0, again.AdvertiserCount);
        Assert.Equal(0, again.SlotCount);
        Assert.Equal(MarketplaceMetrics.DuplicateNotice, again.Notice);
        Assert.False(again.RevenueRecorded);
        Assert.False(again.SlotsChanged);
    }

    [Fact]
    public void A_revenue_amount_and_an_added_slot_are_refused()
    {
        var revenue = Assert.Throws<InvalidOperationException>(() =>
            MarketplaceMetrics.Store("metrics-reading-1", ["Harbor Audio Labs"], ["Test Creator"], 8, 0, 215m, false, []));
        Assert.Contains("not on file", revenue.Message);
        var added = Assert.Throws<InvalidOperationException>(() =>
            MarketplaceMetrics.Store("metrics-reading-1", ["Harbor Audio Labs"], ["Test Creator"], 8, 0, null, true, []));
        Assert.Contains("not added", added.Message);
        var missing = Assert.Throws<InvalidOperationException>(() =>
            MarketplaceMetrics.Store("metrics-reading-1", null, [], 0, 0, null, false, []));
        Assert.Contains("None is invented", missing.Message);
    }

    [Fact]
    public void A_stored_revenue_row_is_counted_without_copying_an_amount()
    {
        var reading = MarketplaceMetrics.Store(
            "metrics-reading-1",
            ["Harbor Audio Labs"],
            ["Test Creator", "Test Creator Brazil"],
            8,
            1,
            null,
            false,
            []);
        Assert.Equal(1, reading.RevenueRowCount);
        Assert.Contains("does not copy an amount", reading.RevenueLine);
        Assert.False(reading.RevenueRecorded);
        Assert.Equal("Stored creator pressure is ahead of stored advertiser pressure.", reading.Pressure);
    }

    [Fact]
    public void Metrics_source_does_not_invent_revenue_or_a_census()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "MarketplaceMetrics.cs"));
        Assert.Contains("not a census", source);
        Assert.Contains("not on file", source);
        Assert.Contains("not added", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
