using System.Globalization;

namespace Bliss.Domain.Demonstrations;

public sealed record MetricRecord(
    string MetricKey,
    int AdvertiserCount,
    int CreatorCount,
    int SlotCount,
    int RevenueRowCount);

public sealed record MetricSnapshot(
    int AdvertiserCount,
    int CreatorCount,
    int SlotCount,
    int RevenueRowCount,
    string Pressure,
    string RevenueLine,
    IReadOnlyList<string> Advertisers,
    IReadOnlyList<string> Creators);

public sealed record MetricReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string MetricKey,
    int AdvertiserCount,
    int CreatorCount,
    int SlotCount,
    int RevenueRowCount,
    string Pressure,
    string RevenueLine,
    bool CensusClaimed,
    bool RevenueRecorded,
    bool SlotsChanged,
    bool Duplicate,
    IReadOnlyList<string> Advertisers,
    IReadOnlyList<string> Creators);

/// <summary>
/// Stores one marketplace reading from the rows already on file.
/// A revenue amount is not copied. A stored slot is not added.
/// </summary>
public static class MarketplaceMetrics
{
    public const string Notice =
        "A marketplace reading stores the measured advertiser count, the measured creator count, and the stored slot count. A revenue amount is not on file. This is not a census. The balance page is unchanged. Green does not send. Delivery remains NOT_SENT.";

    public const string DuplicateNotice =
        "That marketplace reading is already stored. No revenue row was added. Delivery remains NOT_SENT.";

    public const string RevenueUnrecorded =
        "No revenue row is on file. None was invented. Economics remains the only price authority.";

    public static MetricSnapshot Read(
        IReadOnlyList<string>? advertiserNames,
        IReadOnlyList<string>? creatorNames,
        int slotCount,
        int revenueRowCount)
    {
        var advertisers = Prepare(advertiserNames, "Stored advertisers are required. None is invented.");
        var creators = Prepare(creatorNames, "Stored creators are required. None is invented.");
        RequireCounts(slotCount, revenueRowCount);
        return new MetricSnapshot(
            advertisers.Count,
            creators.Count,
            slotCount,
            revenueRowCount,
            Pressure(advertisers.Count, creators.Count),
            DescribeRevenue(revenueRowCount),
            advertisers,
            creators);
    }

    public static MetricReading Store(
        string? metricKey,
        IReadOnlyList<string>? advertiserNames,
        IReadOnlyList<string>? creatorNames,
        int slotCount,
        int revenueRowCount,
        decimal? revenueAmount,
        bool addSlot,
        IReadOnlyList<MetricRecord>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("The marketplace history is required. None is invented.");
        }

        if (addSlot)
        {
            throw new InvalidOperationException("A stored slot is not added. None was invented.");
        }

        if (revenueAmount is not null)
        {
            throw new InvalidOperationException("A revenue amount is not on file. None was invented.");
        }

        var snapshot = Read(advertiserNames, creatorNames, slotCount, revenueRowCount);
        RequireKey(metricKey);
        var key = metricKey!.Trim();
        var prior = existing.FirstOrDefault(item => string.Equals(item.MetricKey, key, StringComparison.Ordinal));
        if (prior is not null)
        {
            return new MetricReading(
                DuplicateNotice,
                false,
                "NOT_SENT",
                key,
                prior.AdvertiserCount,
                prior.CreatorCount,
                prior.SlotCount,
                prior.RevenueRowCount,
                Pressure(prior.AdvertiserCount, prior.CreatorCount),
                DescribeRevenue(prior.RevenueRowCount),
                false,
                false,
                false,
                true,
                snapshot.Advertisers,
                snapshot.Creators);
        }

        return new MetricReading(
            Notice,
            false,
            "NOT_SENT",
            key,
            snapshot.AdvertiserCount,
            snapshot.CreatorCount,
            snapshot.SlotCount,
            snapshot.RevenueRowCount,
            snapshot.Pressure,
            snapshot.RevenueLine,
            false,
            false,
            false,
            false,
            snapshot.Advertisers,
            snapshot.Creators);
    }

    public static string DescribeRevenue(int revenueRowCount)
    {
        if (revenueRowCount == 0)
        {
            return RevenueUnrecorded;
        }

        return "Stored revenue rows: " + revenueRowCount.ToString(CultureInfo.InvariantCulture)
            + ". This reading does not copy an amount. Economics remains the only price authority.";
    }

    public static void RequireKey(string? metricKey)
    {
        var key = (metricKey ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("A reading key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                throw new InvalidOperationException("A reading key is required. None is invented.");
            }
        }
    }

    private static void RequireCounts(int slotCount, int revenueRowCount)
    {
        if (slotCount < 0)
        {
            throw new InvalidOperationException("A stored slot count must be zero or greater. None is invented.");
        }

        if (revenueRowCount < 0)
        {
            throw new InvalidOperationException("A revenue row count must be zero or greater. None is invented.");
        }
    }

    private static IReadOnlyList<string> Prepare(IReadOnlyList<string>? names, string missing)
    {
        if (names is null)
        {
            throw new InvalidOperationException(missing);
        }

        var kept = new List<string>();
        foreach (var name in names)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            kept.Add(trimmed);
        }

        kept.Sort(StringComparer.Ordinal);
        return kept;
    }

    private static string Pressure(int advertisers, int creators)
    {
        if (advertisers > creators)
        {
            return "Stored advertiser pressure is ahead of stored creator pressure.";
        }

        if (creators > advertisers)
        {
            return "Stored creator pressure is ahead of stored advertiser pressure.";
        }

        return "Stored advertiser pressure and stored creator pressure are level.";
    }
}
