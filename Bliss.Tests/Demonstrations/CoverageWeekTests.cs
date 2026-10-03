using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class CoverageWeekTests
{
    [Fact]
    public void A_coverage_week_stores_the_measured_market_and_adds_none()
    {
        var reading = CoverageWeek.Store("coverage-week-1", 1, "SHORTAGE", ["Panama City"], false, []);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal(1, reading.QualifiedSlices);
        Assert.Equal(1, reading.MarketCount);
        Assert.Equal("SHORTAGE", reading.FuelStatus);
        Assert.False(reading.CensusClaimed);
        Assert.False(reading.SlicesChanged);
        Assert.False(reading.Duplicate);
        Assert.Equal("Panama City", Assert.Single(reading.Markets));
        Assert.Contains("not a census", reading.Notice);
        Assert.Contains("does not add a market", reading.Notice);
    }

    [Fact]
    public void An_empty_library_stores_zero_and_a_repeat_does_not_add_a_market()
    {
        var empty = CoverageWeek.Store("coverage-week-1", 0, "SHORTAGE", [], false, []);
        Assert.Equal(0, empty.QualifiedSlices);
        Assert.Equal(0, empty.MarketCount);

        var again = CoverageWeek.Store(
            "coverage-week-1",
            1,
            "SHORTAGE",
            ["Panama City"],
            false,
            [new WeekRecord("coverage-week-1", 0, 0, "SHORTAGE")]);
        Assert.True(again.Duplicate);
        Assert.Equal(0, again.QualifiedSlices);
        Assert.Equal(0, again.MarketCount);
        Assert.Equal(CoverageWeek.DuplicateNotice, again.Notice);
        Assert.False(again.SlicesChanged);
    }

    [Fact]
    public void A_missing_market_and_a_qualified_slice_without_a_market_are_refused()
    {
        var added = Assert.Throws<InvalidOperationException>(() =>
            CoverageWeek.Store("coverage-week-1", 1, "SHORTAGE", ["Panama City"], true, []));
        Assert.Contains("not added", added.Message);
        var orphan = Assert.Throws<InvalidOperationException>(() =>
            CoverageWeek.Store("coverage-week-1", 1, "SHORTAGE", [], false, []));
        Assert.Contains("None is invented", orphan.Message);
    }

    [Fact]
    public void Week_source_does_not_invent_a_market_or_a_census()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "CoverageWeek.cs"));
        Assert.Contains("not a census", source);
        Assert.Contains("not added", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
