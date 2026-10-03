namespace Bliss.Domain.Demonstrations;

public sealed record WeekRecord(
    string WeekKey,
    int QualifiedSlices,
    int MarketCount,
    string FuelStatus);

public sealed record WeekReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string WeekKey,
    int QualifiedSlices,
    int MarketCount,
    string FuelStatus,
    bool CensusClaimed,
    bool SlicesChanged,
    bool Duplicate,
    IReadOnlyList<string> Markets);

/// <summary>
/// Stores one coverage week from the measured slices.
/// A missing market is not added.
/// </summary>
public static class CoverageWeek
{
    public const string Notice =
        "A calendar week stores the measured coverage. It does not add a market. A missing market stays unlisted. This is not a census. The fuel gauge is unchanged. Green does not send. Delivery remains NOT_SENT.";

    public const string DuplicateNotice =
        "That coverage week is already stored. No market was added. Delivery remains NOT_SENT.";

    public static WeekReading Store(
        string? weekKey,
        int qualifiedSlices,
        string? fuelStatus,
        IReadOnlyList<string>? markets,
        bool addMissingMarket,
        IReadOnlyList<WeekRecord>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("The coverage week history is required. None is invented.");
        }

        if (addMissingMarket)
        {
            throw new InvalidOperationException("A missing market is not added. None was invented.");
        }

        if (markets is null)
        {
            throw new InvalidOperationException("Stored markets are required before a coverage week is stored. None is invented.");
        }

        if (qualifiedSlices < 0)
        {
            throw new InvalidOperationException("A qualified slice count must be zero or greater. None is invented.");
        }

        var status = (fuelStatus ?? string.Empty).Trim();
        if (status.Length == 0)
        {
            throw new InvalidOperationException("The fuel reading is required. None is invented.");
        }

        var names = new List<string>();
        foreach (var market in markets)
        {
            var trimmed = (market ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                throw new InvalidOperationException("A blank market is not counted. None is invented.");
            }

            if (!names.Contains(trimmed, StringComparer.Ordinal))
            {
                names.Add(trimmed);
            }
        }

        names.Sort(StringComparer.Ordinal);
        if (names.Count == 0 && qualifiedSlices != 0)
        {
            throw new InvalidOperationException("A qualified slice needs its stored market. None is invented.");
        }

        if (qualifiedSlices < names.Count)
        {
            throw new InvalidOperationException("A qualified slice needs its stored market. None is invented.");
        }

        RequireKey(weekKey);
        var key = weekKey!.Trim();
        var prior = existing.FirstOrDefault(item => string.Equals(item.WeekKey, key, StringComparison.Ordinal));
        if (prior is not null)
        {
            return new WeekReading(
                DuplicateNotice,
                false,
                "NOT_SENT",
                key,
                prior.QualifiedSlices,
                prior.MarketCount,
                prior.FuelStatus,
                false,
                false,
                true,
                names);
        }

        return new WeekReading(
            Notice,
            false,
            "NOT_SENT",
            key,
            qualifiedSlices,
            names.Count,
            status,
            false,
            false,
            false,
            names);
    }

    public static void RequireKey(string? weekKey)
    {
        var key = (weekKey ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("A week key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                throw new InvalidOperationException("A week key is required. None is invented.");
            }
        }
    }
}
