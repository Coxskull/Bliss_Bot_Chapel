using System.Numerics;

namespace Bliss.Domain.Demonstrations;

public static class SourceMediaStatus
{
    public const string Qualified = "QUALIFIED";
    public const string Duplicate = "DUPLICATE";
    public const string NearDuplicate = "NEAR_DUPLICATE";
    public const string Rejected = "REJECTED";
}

public static class SourceMediaRules
{
    public const int DailyTarget = 20;
    public const int WeeklyTarget = 100;
    public const int NearDuplicateDistance = 8;

    public static bool DurationIsUsable(double seconds) => seconds is >= 8 and <= 20;

    public static string Classify(bool exactDuplicate, int frameDistance, bool usableDuration)
    {
        if (exactDuplicate)
        {
            return SourceMediaStatus.Duplicate;
        }

        if (frameDistance <= NearDuplicateDistance)
        {
            return SourceMediaStatus.NearDuplicate;
        }

        return usableDuration ? SourceMediaStatus.Qualified : SourceMediaStatus.Rejected;
    }

    public static int QuotaCredit(string status) =>
        status == SourceMediaStatus.Qualified ? 1 : 0;

    public static string AverageHash(ReadOnlySpan<byte> gray)
    {
        if (gray.Length < 64)
        {
            return string.Empty;
        }

        var sum = 0;
        for (var i = 0; i < 64; i++)
        {
            sum += gray[i];
        }

        var average = sum / 64;
        ulong bits = 0;
        for (var i = 0; i < 64; i++)
        {
            if (gray[i] >= average)
            {
                bits |= 1UL << i;
            }
        }

        return bits.ToString("x16");
    }

    public static int Hamming(string left, string right)
    {
        if (left.Length != 16 || right.Length != 16)
        {
            return int.MaxValue;
        }

        var a = Convert.ToUInt64(left, 16);
        var b = Convert.ToUInt64(right, 16);
        return BitOperations.PopCount(a ^ b);
    }

    public static int ClosestFrameDistance(string frameHash, IEnumerable<string> existing)
    {
        var best = int.MaxValue;
        foreach (var candidate in existing)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            best = Math.Min(best, Hamming(frameHash, candidate));
        }

        return best;
    }
}

public sealed record MediaFuelGauge(
    int DailyTarget,
    int Submitted,
    int QualifiedUnique,
    int Duplicates,
    int Rejected,
    int Pending,
    int ReplacementRequired,
    int DailyRemaining,
    int WeeklyTarget,
    int WeeklyQualified,
    int WeeklyRemaining,
    string FuelStatus)
{
    public static MediaFuelGauge From(IEnumerable<(string Status, int QuotaCredit)> clips)
    {
        var list = clips.ToList();
        var qualified = list.Sum(x => x.QuotaCredit);
        var duplicates = list.Count(x =>
            x.Status is SourceMediaStatus.Duplicate or SourceMediaStatus.NearDuplicate);
        var rejected = list.Count(x => x.Status == SourceMediaStatus.Rejected);
        var pending = list.Count(x => x.Status is not (
            SourceMediaStatus.Qualified
            or SourceMediaStatus.Duplicate
            or SourceMediaStatus.NearDuplicate
            or SourceMediaStatus.Rejected));
        var status = qualified >= SourceMediaRules.DailyTarget
            ? "HEALTHY"
            : qualified * 2 >= SourceMediaRules.DailyTarget
                ? "BELOW_EXPECTED"
                : "SHORTAGE";
        return new MediaFuelGauge(
            SourceMediaRules.DailyTarget,
            list.Count,
            qualified,
            duplicates,
            rejected,
            pending,
            duplicates + rejected,
            Math.Max(0, SourceMediaRules.DailyTarget - qualified),
            SourceMediaRules.WeeklyTarget,
            qualified,
            Math.Max(0, SourceMediaRules.WeeklyTarget - qualified),
            status);
    }
}
