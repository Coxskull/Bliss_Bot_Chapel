using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class RotationPeriodTests
{
    private static readonly string[] Names =
    [
        "Harbor Audio Labs",
        "Sunrise Wellness Co.",
        "TEST Dental Manila",
        "TEST Restaurant Santo Domingo"
    ];

    [Fact]
    public void A_later_period_keeps_the_open_slots_and_records_no_revenue()
    {
        var reading = RotationPeriod.Store(6, Names, true, 8, "later-period-1", false, null, []);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal(6, reading.TheoreticalSlots);
        Assert.Equal(4, reading.PlacedAdvertisers);
        Assert.Equal(2, reading.OpenSlots);
        Assert.Equal(8, reading.SlotCount);
        Assert.True(reading.CreatorApproved);
        Assert.True(reading.Accepted);
        Assert.False(reading.Duplicate);
        Assert.False(reading.CensusClaimed);
        Assert.False(reading.SlotsChanged);
        Assert.Equal(RotationPeriod.RevenueUnrecorded, reading.RevenueLine);
        Assert.Contains("not a census", reading.Notice);
        Assert.Contains("not fill a theoretical slot", reading.Notice);
        Assert.Equal(4, reading.Advertisers.Count);
        Assert.DoesNotContain(reading.Advertisers, name => name.Contains("invented"));
    }

    [Fact]
    public void A_withheld_decision_keeps_the_open_count_and_a_repeat_does_not_fill()
    {
        var withheld = RotationPeriod.Store(6, Names, false, 8, "later-period-withheld", false, null, []);
        Assert.False(withheld.CreatorApproved);
        Assert.False(withheld.Accepted);
        Assert.Equal(2, withheld.OpenSlots);
        Assert.False(withheld.SlotsChanged);

        var again = RotationPeriod.Store(
            6,
            Names,
            true,
            8,
            "later-period-1",
            false,
            null,
            [new PeriodRecord("later-period-1", 2, 8, true)]);
        Assert.True(again.Duplicate);
        Assert.Equal(RotationPeriod.DuplicateNotice, again.Notice);
        Assert.Equal(2, again.OpenSlots);
        Assert.True(again.CreatorApproved);
        Assert.Equal(4, again.Advertisers.Count);
    }

    [Fact]
    public void A_filled_slot_and_a_revenue_amount_are_refused()
    {
        var filled = Assert.Throws<InvalidOperationException>(() =>
            RotationPeriod.Store(6, Names, true, 8, "later-period-1", true, null, []));
        Assert.Contains("not filled", filled.Message);
        var revenue = Assert.Throws<InvalidOperationException>(() =>
            RotationPeriod.Store(6, Names, true, 8, "later-period-1", false, 12m, []));
        Assert.Contains("None was invented", revenue.Message);
        var missing = Assert.Throws<InvalidOperationException>(() =>
            RotationPeriod.Store(6, null, true, 8, "later-period-1", false, null, []));
        Assert.Contains("None is invented", missing.Message);
    }

    [Fact]
    public void Period_source_does_not_invent_a_census_or_a_price()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "RotationPeriod.cs"));
        Assert.Contains("not a census", source);
        Assert.Contains("No revenue row is on file", source);
        Assert.Contains("not filled", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
