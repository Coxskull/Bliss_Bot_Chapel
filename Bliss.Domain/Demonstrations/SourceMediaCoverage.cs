namespace Bliss.Domain.Demonstrations;

public sealed record StoredSlice(
    string? Market,
    string? Country,
    string? Status,
    int QuotaCredit,
    string? Sha256,
    string? FrameHash,
    string? Provenance);

public sealed record MarketCoverage(
    string Market,
    string CountryLine,
    int QualifiedSlices,
    string Notice);

public sealed record WithheldSlices(string Reason, int Count);

public sealed record CoverageReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    MediaFuelGauge Fuel,
    IReadOnlyList<MarketCoverage> Markets,
    IReadOnlyList<WithheldSlices> Withheld);

/// <summary>
/// Market coverage counts stored qualified slices that carry a fingerprint
/// and provenance. A missing market is not listed. Duplicates are not coverage.
/// </summary>
public static class SourceMediaCoverage
{
    public const string Notice =
        "Market coverage counts stored qualified slices that have a fingerprint and provenance. A missing market is not listed. Duplicates are not coverage. The fuel gauge counts stored clips. A calendar week is not configured. None was invented. This is not a census. Green does not send. Delivery remains NOT_SENT.";

    public const string MissingFingerprint =
        "A qualified slice is missing a fingerprint. It is not counted. None was invented.";

    public const string MissingProvenance =
        "Provenance is not recorded. The slice is not counted. None was invented.";

    public const string BlankMarket =
        "A blank market is not counted. None was invented.";

    public const string MissingCredit =
        "A qualified slice without quota credit is not coverage. None was invented.";

    public static CoverageReading Read(IEnumerable<StoredSlice>? slices)
    {
        if (slices is null)
        {
            throw new InvalidOperationException(
                "A coverage reading needs the stored slices. None is invented.");
        }

        var list = slices.ToList();
        var fuel = MediaFuelGauge.From(list.Select(item => ((item.Status ?? string.Empty).Trim(), item.QuotaCredit)));
        var withheld = new Dictionary<string, int>(StringComparer.Ordinal);
        var counted = new List<(string Market, string Country)>();
        foreach (var slice in list)
        {
            var status = (slice.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (status != SourceMediaStatus.Qualified)
            {
                continue;
            }

            if (slice.QuotaCredit != 1)
            {
                Add(withheld, MissingCredit);
                continue;
            }

            if (!HasFingerprint(slice))
            {
                Add(withheld, MissingFingerprint);
                continue;
            }

            if (string.IsNullOrWhiteSpace(slice.Provenance))
            {
                Add(withheld, MissingProvenance);
                continue;
            }

            var market = (slice.Market ?? string.Empty).Trim();
            if (market.Length == 0)
            {
                Add(withheld, BlankMarket);
                continue;
            }

            counted.Add((market, (slice.Country ?? string.Empty).Trim()));
        }

        var markets = counted
            .GroupBy(item => item.Market + "\n" + item.Country, StringComparer.Ordinal)
            .Select(group =>
            {
                var market = group.First().Market;
                var country = group.First().Country;
                var count = group.Count();
                var countryLine = country.Length == 0
                    ? "Country is not recorded. None was invented."
                    : country;
                var noun = count == 1 ? "qualified slice" : "qualified slices";
                return new MarketCoverage(
                    market,
                    countryLine,
                    count,
                    market + ": " + count + " " + noun + ". This is a count of stored rows. It is not a census. None was invented.");
            })
            .OrderBy(item => item.Market, StringComparer.Ordinal)
            .ThenBy(item => item.CountryLine, StringComparer.Ordinal)
            .ToList();

        var held = withheld
            .Select(item => new WithheldSlices(item.Key, item.Value))
            .OrderBy(item => item.Reason, StringComparer.Ordinal)
            .ToList();
        return new CoverageReading(Notice, false, "NOT_SENT", fuel, markets, held);
    }

    private static void Add(Dictionary<string, int> withheld, string reason)
    {
        withheld[reason] = withheld.TryGetValue(reason, out var count) ? count + 1 : 1;
    }

    private static bool HasFingerprint(StoredSlice slice)
    {
        var sha = (slice.Sha256 ?? string.Empty).Trim();
        var frame = (slice.FrameHash ?? string.Empty).Trim();
        return sha.Length == 64 && sha.All(IsHex) && frame.Length == 16 && frame.All(IsHex);
    }

    private static bool IsHex(char character) =>
        character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
