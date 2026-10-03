using System.Globalization;

namespace Bliss.Domain.Demonstrations;

public sealed record BalanceBoard(
    string Notice,
    string Counts,
    string Pressure,
    string Revenue,
    string Inventory,
    string Skipped,
    int? AdvertiserCount,
    int? CreatorCount,
    bool CensusClaimed,
    bool GreenMeansSend,
    string Delivery,
    IReadOnlyList<string> Advertisers,
    IReadOnlyList<string> Creators);

/// <summary>
/// Advertiser pressure and creator pressure are counts of stored rows.
/// A missing count stays unrecorded. None is invented. Green is not a send.
/// </summary>
public static class MarketplaceBalance
{
    public const string Notice =
        "Advertiser pressure and creator pressure are counts of stored rows. They are not a market census. None was invented. Green does not send. Delivery remains NOT_SENT.";

    public const string RevenueUnrecorded = "Revenue is not recorded. None was invented.";
    public const string InventoryUnrecorded = "Inventory is not recorded. None was invented.";

    public static BalanceBoard Read(IReadOnlyList<string>? advertiserNames, IReadOnlyList<string>? creatorNames)
    {
        var advertisers = Names(advertiserNames, out var advertiserCount, out var advertiserBlanks);
        var creators = Names(creatorNames, out var creatorCount, out var creatorBlanks);
        var skipped = advertiserBlanks + creatorBlanks > 0
            ? "A blank stored name was skipped. None was invented."
            : string.Empty;
        return new BalanceBoard(
            Notice,
            Counts(advertiserCount, creatorCount),
            Pressure(advertiserCount, creatorCount),
            RevenueUnrecorded,
            InventoryUnrecorded,
            skipped,
            advertiserCount,
            creatorCount,
            false,
            false,
            "NOT_SENT",
            advertisers,
            creators);
    }

    private static IReadOnlyList<string> Names(IReadOnlyList<string>? names, out int? count, out int blanks)
    {
        blanks = 0;
        if (names is null)
        {
            count = null;
            return [];
        }

        var kept = new List<string>();
        foreach (var name in names)
        {
            var trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                blanks++;
                continue;
            }

            kept.Add(trimmed);
        }

        kept.Sort(StringComparer.Ordinal);
        count = kept.Count;
        return kept;
    }

    private static string Counts(int? advertisers, int? creators)
    {
        var advertiserLine = advertisers is null
            ? "Stored advertisers: not recorded."
            : "Stored advertisers: " + advertisers.Value.ToString(CultureInfo.InvariantCulture) + ".";
        var creatorLine = creators is null
            ? "Stored creators: not recorded."
            : "Stored creators: " + creators.Value.ToString(CultureInfo.InvariantCulture) + ".";
        return advertiserLine + " " + creatorLine;
    }

    private static string Pressure(int? advertisers, int? creators)
    {
        if (advertisers is null || creators is null)
        {
            return "The balance is not claimed. None was invented.";
        }

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
