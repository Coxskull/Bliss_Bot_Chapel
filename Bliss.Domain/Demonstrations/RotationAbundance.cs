using System.Globalization;

namespace Bliss.Domain.Demonstrations;

public sealed record RotationBoard(
    string Notice,
    string Period,
    string Counts,
    string Abundance,
    string Result,
    int? TheoreticalSlots,
    int? PlacedAdvertisers,
    int? OpenSlots,
    int SlotCount,
    bool Accepted,
    bool GreenMeansSend,
    string Delivery,
    bool SlotsChanged,
    string Skipped,
    IReadOnlyList<string> Advertisers);

/// <summary>
/// One rotation pass. A theoretical slot may stay open.
/// One advertiser is not required for every slot. Green is not a send.
/// </summary>
public static class RotationAbundance
{
    public static readonly int[] Pairs = [2, 4, 6];

    public const string Notice =
        "A rotation may leave a theoretical slot open. One advertiser is not required for every slot. Creator approval governs the rotation. This pass is one rotation. Green does not send. Delivery remains NOT_SENT.";

    public const string Period =
        "This pass is one rotation. A later period is not configured. None was invented.";

    public const string Abundance =
        "One advertiser is not required for every theoretical slot. None was invented.";

    public const string Unread = "A rotation has not been read.";

    public static RotationBoard Preview(IReadOnlyList<string>? advertiserNames, int slotCount)
    {
        var names = Prepare(advertiserNames, slotCount, out var skipped);
        return Board(Unread, null, names.Count, null, false, slotCount, skipped, names);
    }

    public static RotationBoard Read(int? pair, IReadOnlyList<string>? advertiserNames, bool? creatorApproved, int slotCount)
    {
        if (pair is not (2 or 4 or 6))
        {
            throw new InvalidOperationException("A rotation uses a balanced pair of 2, 4, or 6. None is invented.");
        }

        var names = Prepare(advertiserNames, slotCount, out var skipped);
        if (creatorApproved is null)
        {
            throw new InvalidOperationException("Creator approval is required. None is invented.");
        }

        if (names.Count > pair.Value)
        {
            throw new InvalidOperationException("A rotation cannot place more stored advertisers than the balanced pair. None is invented.");
        }

        var open = pair.Value - names.Count;
        var result = creatorApproved.Value
            ? open > 0
                ? "The rotation is accepted. Open slots remain. Creator approval governs the rotation."
                : "The rotation is accepted. Open slots were allowed. Creator approval governs the rotation."
            : "The rotation stays withheld. Creator approval governs the rotation.";
        return Board(result, pair, names.Count, open, creatorApproved.Value, slotCount, skipped, names);
    }

    private static IReadOnlyList<string> Prepare(IReadOnlyList<string>? advertiserNames, int slotCount, out string skipped)
    {
        if (slotCount < 0)
        {
            throw new InvalidOperationException("A stored slot count must be zero or greater. None is invented.");
        }

        if (advertiserNames is null)
        {
            throw new InvalidOperationException("Stored advertisers are required before a rotation is read. None is invented.");
        }

        var blanks = 0;
        var repeats = 0;
        var kept = new List<string>();
        foreach (var name in advertiserNames)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                blanks++;
                continue;
            }

            if (kept.Contains(trimmed, StringComparer.Ordinal))
            {
                repeats++;
                continue;
            }

            kept.Add(trimmed);
        }

        kept.Sort(StringComparer.Ordinal);
        skipped = blanks > 0
            ? "A blank stored name was skipped. None was invented."
            : repeats > 0
                ? "A repeated stored name is kept once. None was invented."
                : string.Empty;
        if (blanks > 0 && repeats > 0)
        {
            skipped = "A blank stored name was skipped. A repeated stored name is kept once. None was invented.";
        }

        return kept;
    }

    private static RotationBoard Board(
        string result,
        int? theoretical,
        int placed,
        int? open,
        bool accepted,
        int slotCount,
        string skipped,
        IReadOnlyList<string> names)
    {
        var counts = theoretical is null
            ? "Stored advertisers: " + placed.ToString(CultureInfo.InvariantCulture)
                + ". Stored slots: " + slotCount.ToString(CultureInfo.InvariantCulture) + "."
            : "Theoretical slots: " + theoretical.Value.ToString(CultureInfo.InvariantCulture)
                + ". Placed advertisers: " + placed.ToString(CultureInfo.InvariantCulture)
                + ". Open slots: " + open!.Value.ToString(CultureInfo.InvariantCulture) + ".";
        return new RotationBoard(
            Notice,
            Period,
            counts,
            Abundance,
            result,
            theoretical,
            placed,
            open,
            slotCount,
            accepted,
            false,
            "NOT_SENT",
            false,
            skipped,
            names);
    }
}
