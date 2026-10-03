using Bliss.Domain.Operations;

namespace Bliss.Tests.Operations;

public sealed class FleetLanesTests
{
    [Fact]
    public void A_broken_outreach_lane_does_not_stop_the_ocean()
    {
        var board = FleetLanes.Read(Stored("OUTREACH", LaneTempo.Stopped));
        var fishing = board.Fleets.Single(item => item.Fleet == FleetLanes.Fishing);
        var creator = board.Fleets.Single(item => item.Fleet == FleetLanes.Creator);

        Assert.True(board.OceanMoving);
        Assert.False(board.GreenMeansSend);
        Assert.Equal("NOT_SENT", board.Delivery);
        Assert.Contains("A broken lane does not stop the ocean", board.Notice);
        Assert.Contains("Bliss Chapel stays the matching middle", board.Bliss);
        Assert.Contains("Green does not send", board.Notice);
        Assert.Contains("NOT_SENT", board.Notice);
        Assert.Equal("The ocean keeps moving.", board.Ocean);
        Assert.True(fishing.Moving);
        Assert.True(creator.Moving);
        Assert.Equal("The Fishing Fleet keeps moving.", fishing.Notice);
        Assert.Equal("The Creator Fleet keeps moving.", creator.Notice);
        Assert.Equal("Broken. This lane is stopped.", Place(fishing, "OUTREACH"));
        Assert.Equal("Moving. This lane keeps its own tempo.", Place(fishing, "DISCOVERY"));
        Assert.Equal("Moving. This lane keeps its own tempo.", Place(creator, "CREATOR_DISCOVERY"));
        Assert.DoesNotMatch(@"\d", board.Notice);
        Assert.DoesNotContain("$", board.Notice);
    }

    [Fact]
    public void A_stopped_creator_fleet_leaves_the_fishing_fleet_moving()
    {
        var board = FleetLanes.Read(Stored("CREATOR_DISCOVERY", LaneTempo.Stopped));
        var fishing = board.Fleets.Single(item => item.Fleet == FleetLanes.Fishing);
        var creator = board.Fleets.Single(item => item.Fleet == FleetLanes.Creator);

        Assert.True(fishing.Moving);
        Assert.False(creator.Moving);
        Assert.True(board.OceanMoving);
        Assert.Equal("The Creator Fleet is stopped. The other fleet is not stopped by this break.", creator.Notice);
        Assert.Equal("The Fishing Fleet keeps moving.", fishing.Notice);
        Assert.Equal("The ocean keeps moving.", board.Ocean);
        Assert.False(board.GreenMeansSend);
        Assert.Equal("NOT_SENT", board.Delivery);
    }

    [Fact]
    public void Every_stopped_lane_does_not_claim_the_ocean_is_moving()
    {
        var board = FleetLanes.Read(LaneTempo.Lanes.Select(lane => new FleetLane(lane, LaneTempo.Stopped)));
        Assert.False(board.OceanMoving);
        Assert.Contains("not claimed to be moving", board.Ocean);
        Assert.Contains("None was invented", board.Ocean);
        Assert.Contains("A broken lane does not stop the ocean", board.Notice);
        Assert.Equal("NOT_SENT", board.Delivery);
    }

    [Fact]
    public void A_missing_lane_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            FleetLanes.Read([new FleetLane("DISCOVERY", LaneTempo.Full)]));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Fleet_source_does_not_send_or_invent_a_price()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Operations",
            "FleetLanes.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("A broken lane does not stop the ocean", source);
        Assert.Contains("None is invented", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }

    private static string Place(FleetReading fleet, string lane) =>
        fleet.Lanes.Single(item => item.Lane == lane).Place;

    private static IEnumerable<FleetLane> Stored(string broken, string tempo) =>
        LaneTempo.Lanes.Select(lane => new FleetLane(lane, lane == broken ? tempo : LaneTempo.Full));
}
