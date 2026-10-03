namespace Bliss.Domain.Operations;

public sealed record FleetLane(string Lane, string Tempo);

public sealed record FleetLanePlace(string Lane, string DisplayName, string Tempo, string Place);

public sealed record FleetReading(
    string Fleet,
    string DisplayName,
    bool Moving,
    string Notice,
    IReadOnlyList<FleetLanePlace> Lanes);

public sealed record FleetBoard(
    string Notice,
    string Ocean,
    string Bliss,
    bool OceanMoving,
    bool GreenMeansSend,
    string Delivery,
    IReadOnlyList<FleetReading> Fleets);

/// <summary>
/// Two fleets on the existing operations console. A broken lane does not
/// stop the ocean. Bliss Chapel stays the matching middle. Green is not a send.
/// </summary>
public static class FleetLanes
{
    public const string Fishing = "FISHING";
    public const string Creator = "CREATOR";

    public const string Notice =
        "Two fleets share one operations console. A broken lane does not stop the ocean. Bliss Chapel stays the matching middle. Green does not send. Delivery remains NOT_SENT.";

    public const string BlissMiddle = "Bliss Chapel stays the matching middle.";

    public static readonly string[] FishingLanes =
    [
        "DISCOVERY",
        "VERIFICATION",
        "ROAD_FINDING",
        "POLICY",
        "OUTREACH"
    ];

    public static readonly string[] CreatorLanes = ["CREATOR_DISCOVERY"];

    public static FleetBoard Read(IEnumerable<FleetLane>? lanes)
    {
        var stored = (lanes ?? []).ToList();
        if (stored.Count != LaneTempo.Lanes.Length
            || LaneTempo.Lanes.Any(name => stored.Count(item => item.Lane == name) != 1))
        {
            throw new InvalidOperationException("A fleet reading needs the six stored lanes. None is invented.");
        }

        var tempos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in stored)
        {
            var tempo = (item.Tempo ?? string.Empty).Trim().ToUpperInvariant();
            if (tempo is not (LaneTempo.Full or LaneTempo.Reduced or LaneTempo.Stopped))
            {
                throw new InvalidOperationException("A lane tempo must be FULL, REDUCED, or STOPPED. None is invented.");
            }

            tempos[item.Lane] = tempo;
        }

        var fishing = ReadFleet(Fishing, "Fishing Fleet", FishingLanes, tempos);
        var creator = ReadFleet(Creator, "Creator Fleet", CreatorLanes, tempos);
        var oceanMoving = fishing.Moving || creator.Moving;
        var ocean = oceanMoving
            ? "The ocean keeps moving."
            : "Every recorded lane is stopped. The ocean is not claimed to be moving. None was invented.";
        return new FleetBoard(Notice, ocean, BlissMiddle, oceanMoving, false, "NOT_SENT", [fishing, creator]);
    }

    private static FleetReading ReadFleet(
        string fleet,
        string displayName,
        IReadOnlyList<string> lanes,
        IReadOnlyDictionary<string, string> tempos)
    {
        var places = lanes.Select(lane =>
        {
            var tempo = tempos[lane];
            var place = tempo == LaneTempo.Stopped
                ? "Broken. This lane is stopped."
                : "Moving. This lane keeps its own tempo.";
            return new FleetLanePlace(lane, LaneTempo.Display(lane), tempo, place);
        }).ToList();
        var moving = places.Any(item => item.Tempo != LaneTempo.Stopped);
        var notice = moving
            ? "The " + displayName + " keeps moving."
            : "The " + displayName + " is stopped. The other fleet is not stopped by this break.";
        return new FleetReading(fleet, displayName, moving, notice, places);
    }
}
