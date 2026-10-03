using System.Globalization;

namespace Bliss.Domain.Demonstrations;

public sealed record StoredSlot(string? Title, string? SlotType);

public sealed record StoredContent(string Title, string Line);

public sealed record StoredInventory(
    string Notice,
    string Stored,
    string Rotation,
    int SlotCount,
    bool GreenMeansSend,
    string Delivery,
    bool SlotsChanged,
    IReadOnlyList<StoredContent> Contents);

public sealed record CreativeDecision(
    string Notice,
    string Result,
    int? Pair,
    bool Accepted,
    bool StackRefused,
    bool GreenMeansSend,
    string Delivery,
    bool SlotsChanged);

/// <summary>
/// A balanced pair is 2, 4, or 6. A two-over-four stack is not the default.
/// Creator approval governs density. The reading does not rewrite stored slots.
/// </summary>
public static class CreativeInventory
{
    public static readonly int[] Pairs = [2, 4, 6];

    public const string Notice =
        "A balanced pair is 2, 4, or 6. A two-over-four stack is not the default. Creator approval governs density. Rotation is not configured. Green does not send. Delivery remains NOT_SENT.";

    public const string Rotation = "Rotation is not configured. None was invented.";

    public static StoredInventory ReadStored(IEnumerable<StoredSlot>? slots)
    {
        var stored = (slots ?? []).ToList();
        var contents = stored
            .GroupBy(item => (item.Title ?? string.Empty).Trim(), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Line(group.Key, group.Select(item => item.SlotType)))
            .Where(item => item is not null)
            .Cast<StoredContent>()
            .ToList();
        return new StoredInventory(
            Notice,
            DescribeStored(stored.Count),
            Rotation,
            stored.Count,
            false,
            "NOT_SENT",
            false,
            contents);
    }

    public static string DescribeStored(int slotCount)
    {
        if (slotCount < 0)
        {
            throw new InvalidOperationException("A stored slot count must be zero or greater. None is invented.");
        }

        return "Stored slots: " + slotCount.ToString(CultureInfo.InvariantCulture)
            + ". This is a count of stored rows. It is not a balanced pair. None was invented.";
    }

    public static CreativeDecision Decide(int? pair, bool? creatorApproved, bool stack)
    {
        if (stack)
        {
            return new CreativeDecision(
                Notice,
                "The two-over-four stack is refused. It is not the default. None was invented.",
                null,
                false,
                true,
                false,
                "NOT_SENT",
                false);
        }

        if (pair is not (2 or 4 or 6))
        {
            throw new InvalidOperationException("A balanced pair is 2, 4, or 6. None is invented.");
        }

        if (creatorApproved is null)
        {
            throw new InvalidOperationException("Creator approval is required. None is invented.");
        }

        var count = pair.Value.ToString(CultureInfo.InvariantCulture);
        var result = creatorApproved.Value
            ? "The balanced pair of " + count + " is accepted. Creator approval governs this density."
            : "The balanced pair of " + count + " stays withheld. Creator approval governs this density.";
        return new CreativeDecision(Notice, result, pair, creatorApproved.Value, false, false, "NOT_SENT", false);
    }

    private static StoredContent? Line(string title, IEnumerable<string?> slotTypes)
    {
        if (title.Length == 0)
        {
            return new StoredContent(string.Empty, "A blank stored title was skipped. None was invented.");
        }

        var types = slotTypes
            .Select(item => (item ?? string.Empty).Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToList();
        var named = types.Count == 0
            ? title + " keeps no named slot type. None was invented."
            : title + " keeps " + string.Join(", ", types) + ".";
        return new StoredContent(title, named);
    }
}
