namespace Bliss.Domain.Demonstrations;

public sealed record PeriodRecord(
    string PeriodKey,
    int OpenSlots,
    int SlotCount,
    bool CreatorApproved);

public sealed record PeriodReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    string PeriodKey,
    int TheoreticalSlots,
    int PlacedAdvertisers,
    int OpenSlots,
    int SlotCount,
    bool CreatorApproved,
    bool Accepted,
    string RevenueLine,
    bool CensusClaimed,
    bool SlotsChanged,
    bool Duplicate,
    IReadOnlyList<string> Advertisers);

/// <summary>
/// Stores one later rotation period from the measured pass.
/// Open slots stay open. No revenue is invented.
/// </summary>
public static class RotationPeriod
{
    public const string Notice =
        "A later period stores the measured open slots and the creator decision. It does not fill a theoretical slot. No revenue row is on file. This is not a census. Economics remains the only price authority. Green does not send. Delivery remains NOT_SENT.";

    public const string RevenueUnrecorded =
        "No revenue row is on file. None was invented. Economics remains the only price authority.";

    public const string DuplicateNotice =
        "That later period is already stored. The open slots were not filled. Delivery remains NOT_SENT.";

    public static PeriodReading Store(
        int? pair,
        IReadOnlyList<string>? advertiserNames,
        bool? creatorApproved,
        int slotCount,
        string? periodKey,
        bool fillOpenSlots,
        decimal? revenueAmount,
        IReadOnlyList<PeriodRecord>? existing)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("A later period needs the stored history. None is invented.");
        }

        if (fillOpenSlots)
        {
            throw new InvalidOperationException("A theoretical slot is not filled. None was invented.");
        }

        if (revenueAmount is not null)
        {
            throw new InvalidOperationException("A revenue amount is not on file. None was invented.");
        }

        RequireKey(periodKey);
        var key = periodKey!.Trim();
        var board = RotationAbundance.Read(pair, advertiserNames, creatorApproved, slotCount);
        var prior = existing.FirstOrDefault(item => string.Equals(item.PeriodKey, key, StringComparison.Ordinal));
        if (prior is not null)
        {
            return new PeriodReading(
                DuplicateNotice,
                false,
                "NOT_SENT",
                key,
                pair!.Value,
                board.PlacedAdvertisers ?? 0,
                prior.OpenSlots,
                prior.SlotCount,
                prior.CreatorApproved,
                prior.CreatorApproved,
                RevenueUnrecorded,
                false,
                false,
                true,
                board.Advertisers);
        }

        return new PeriodReading(
            Notice,
            false,
            "NOT_SENT",
            key,
            pair!.Value,
            board.PlacedAdvertisers ?? 0,
            board.OpenSlots ?? 0,
            board.SlotCount,
            creatorApproved!.Value,
            creatorApproved.Value,
            RevenueUnrecorded,
            false,
            false,
            false,
            board.Advertisers);
    }

    public static void RequireKey(string? periodKey)
    {
        var key = (periodKey ?? string.Empty).Trim();
        if (key.Length < 8 || key.Length > 80)
        {
            throw new InvalidOperationException("A period key is required. None is invented.");
        }

        foreach (var character in key)
        {
            var letter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
            if (!letter)
            {
                throw new InvalidOperationException("A period key is required. None is invented.");
            }
        }
    }
}
