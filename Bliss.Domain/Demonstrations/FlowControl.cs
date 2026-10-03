namespace Bliss.Domain.Demonstrations;

public sealed record FlowProspect(string Slug, string BusinessName, bool Suppressed);

public sealed record FlowAssignment(string Slug, string BusinessName, string Lane, string Label);

public sealed record FlowBoard(
    int Preserved,
    int Released,
    int Held,
    int Withheld,
    int Discarded,
    int Capacity,
    string Notice,
    string CapacityNotice,
    bool GreenMeansSend,
    string Delivery,
    IReadOnlyList<FlowAssignment> Assignments);

/// <summary>
/// A slower downstream lane regulates flow.
/// Legitimate prospects stay queued. None are discarded.
/// </summary>
public static class FlowControl
{
    public const string Released = "RELEASED";
    public const string Held = "HELD";
    public const string Withheld = "WITHHELD";

    public const string Notice =
        "Legitimate volume is preserved. The queue holds the excess. Flow is regulated. None was discarded. Green does not send. Delivery remains NOT_SENT.";

    public static FlowBoard Regulate(IEnumerable<FlowProspect>? prospects, int? capacity)
    {
        if (capacity is null or < 1)
        {
            throw new InvalidOperationException("A downstream capacity must be greater than zero. None is invented.");
        }

        var ordered = (prospects ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.BusinessName))
            .OrderBy(item => item.BusinessName, StringComparer.Ordinal)
            .ThenBy(item => item.Slug, StringComparer.Ordinal)
            .ToList();
        var queue = ordered.Where(item => !item.Suppressed).ToList();
        var withheld = ordered.Where(item => item.Suppressed).ToList();
        var released = queue.Take(capacity.Value).ToList();
        var held = queue.Skip(capacity.Value).ToList();
        var assignments = released.Select(item => Assign(item, Released, "Released this pass"))
            .Concat(held.Select(item => Assign(item, Held, "Held in the queue")))
            .Concat(withheld.Select(item => Assign(item, Withheld, "Withheld. The record is preserved.")))
            .ToList();
        return new FlowBoard(
            ordered.Count,
            released.Count,
            held.Count,
            withheld.Count,
            0,
            capacity.Value,
            Notice,
            "The downstream opening count is " + capacity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ". This is a flow limit, not an Economics price.",
            false,
            "NOT_SENT",
            assignments);
    }

    private static FlowAssignment Assign(FlowProspect prospect, string lane, string label) =>
        new(prospect.Slug, prospect.BusinessName.Trim(), lane, label);
}
