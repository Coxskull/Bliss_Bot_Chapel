using Bliss.Domain.Operations;

namespace Bliss.Tests.Operations;

public sealed class LaneTempoTests
{
    [Fact]
    public void Six_lanes_start_without_an_invented_ceiling()
    {
        Assert.Equal(6, LaneTempo.Lanes.Length);
        var notice = LaneTempo.Describe("DISCOVERY", LaneTempo.Full, null, null);
        Assert.Contains("full tempo", notice);
        Assert.Contains("Green does not send", notice);
        Assert.Contains("None was invented", notice);
        Assert.Contains("NOT_SENT", notice);
        Assert.DoesNotMatch(@"\d", notice);
    }

    [Fact]
    public void Stopping_one_lane_keeps_the_record_and_names_no_price()
    {
        var decision = LaneTempo.Apply("outreach", "stopped", null, null, "Deliverability on this road");
        Assert.Equal("OUTREACH", decision.Lane);
        Assert.Equal(LaneTempo.Stopped, decision.Tempo);
        Assert.Null(decision.CeilingAmount);
        Assert.Contains("Other lanes keep moving", decision.Notice);
        Assert.Contains("prospect record is preserved", decision.Notice);
        Assert.Contains("Green does not send", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
        Assert.DoesNotMatch(@"\d", decision.Notice);
    }

    [Fact]
    public void A_recorded_ceiling_is_an_operational_limit()
    {
        var decision = LaneTempo.Apply("discovery", "REDUCED", 40m, "usd", "Provider pressure on this lane");
        Assert.Equal(40m, decision.CeilingAmount);
        Assert.Equal("USD", decision.CeilingCurrency);
        Assert.Contains("40 USD", decision.Notice);
        Assert.Contains("not an Economics price", decision.Notice);
        Assert.Contains("Other lanes are unchanged", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
    }

    [Fact]
    public void A_partial_ceiling_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            LaneTempo.Apply("POLICY", LaneTempo.Full, 10m, null, "Missing currency"));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Tempo_source_does_not_send()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Operations",
            "LaneTempo.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("Green does not send", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
    }
}
