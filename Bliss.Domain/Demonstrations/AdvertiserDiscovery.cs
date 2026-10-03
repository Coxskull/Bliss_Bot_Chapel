using System.Globalization;

namespace Bliss.Domain.Demonstrations;

public sealed record StoredProspect(
    string? Slug,
    string? BusinessName,
    string? Market,
    string? PublicSourceUrl,
    int Score,
    string? ProspectState);

public sealed record DiscoveryRow(
    string BusinessName,
    string Market,
    string SourceUrl,
    int Score,
    string State,
    string Notice);

public sealed record WithheldDiscovery(string Reason, int Count);

public sealed record DiscoveryReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    bool CensusClaimed,
    int Stored,
    int Scored,
    int Preserved,
    IReadOnlyList<DiscoveryRow> Prospects,
    IReadOnlyList<WithheldDiscovery> Withheld);

public sealed record DiscoveryDecision(
    bool Accepted,
    bool Duplicate,
    bool Written,
    string State,
    string Notice,
    string MatchedSlug,
    bool GreenMeansSend,
    string Delivery);

/// <summary>
/// Counts stored public-source prospects. A duplicate source is not a
/// second row. A crawler does not run. Green is not a send.
/// </summary>
public static class AdvertiserDiscovery
{
    public const string Notice =
        "Discovery counts stored prospects that have a public source URL. A missing business is not listed. A duplicate source is not a second prospect. This is not a census. A crawler did not run. None was invented. Green does not send. Delivery remains NOT_SENT.";

    public const string BlankName =
        "A business name from a public source is required. None was invented.";

    public const string UnusableName =
        "The business name does not produce a usable prospect id. None was invented.";

    public const string MissingSource =
        "A public http or https source URL is required. None was invented.";

    public const string DuplicateSource =
        "That public source is already stored. A second prospect was not written.";

    public const string DuplicateName =
        "That business is already stored. A second prospect was not written.";

    public const string PreservedState = "PRESERVED";
    public const string ScoredState = "OPPORTUNITY_SCORED";

    public static DiscoveryReading Read(IReadOnlyList<StoredProspect>? prospects)
    {
        if (prospects is null)
        {
            throw new InvalidOperationException("A discovery reading needs the stored prospects. None is invented.");
        }

        var rows = new List<DiscoveryRow>();
        var blank = 0;
        var missing = 0;
        foreach (var item in prospects)
        {
            var name = (item.BusinessName ?? string.Empty).Trim();
            if (name.Length < 3 || OpportunityScreen.Slug(name).Length < 3)
            {
                blank++;
                continue;
            }

            var source = CanonicalSource(item.PublicSourceUrl);
            if (source is null)
            {
                missing++;
                continue;
            }

            var preserved = string.Equals(item.ProspectState, PreservedState, StringComparison.Ordinal) || item.Score < 100;
            var state = preserved
                ? PreservedState
                : string.IsNullOrWhiteSpace(item.ProspectState) ? ScoredState : item.ProspectState.Trim();
            var market = string.IsNullOrWhiteSpace(item.Market) ? "Market is not recorded. None was invented." : item.Market.Trim();
            rows.Add(new DiscoveryRow(
                name,
                market,
                source,
                item.Score,
                state,
                name + ": score " + item.Score.ToString(CultureInfo.InvariantCulture)
                    + ". State " + state + ". This is one stored prospect. It is not a census. None was invented."));
        }

        rows.Sort((left, right) => string.Compare(left.BusinessName, right.BusinessName, StringComparison.Ordinal));
        var withheld = new List<WithheldDiscovery>();
        if (blank > 0)
        {
            withheld.Add(new WithheldDiscovery(BlankName, blank));
        }

        if (missing > 0)
        {
            withheld.Add(new WithheldDiscovery(MissingSource, missing));
        }

        var preservedCount = rows.Count(row => row.State == PreservedState);
        var scoredCount = rows.Count(row => row.State != PreservedState && row.Score == 100);
        return new DiscoveryReading(
            Notice,
            false,
            "NOT_SENT",
            false,
            rows.Count,
            scoredCount,
            preservedCount,
            rows,
            withheld);
    }

    public static DiscoveryDecision Decide(
        string? businessName,
        string? sourceUrl,
        IReadOnlyList<StoredProspect>? existing,
        bool passesInitialScreen)
    {
        if (existing is null)
        {
            throw new InvalidOperationException("A discovery decision needs the stored prospects. None is invented.");
        }

        var name = (businessName ?? string.Empty).Trim();
        if (name.Length < 3)
        {
            return Refuse(BlankName);
        }

        var slug = OpportunityScreen.Slug(name);
        if (slug.Length < 3)
        {
            return Refuse(UnusableName);
        }

        var source = CanonicalSource(sourceUrl);
        if (source is null)
        {
            return Refuse(MissingSource);
        }

        foreach (var item in existing)
        {
            var storedSource = CanonicalSource(item.PublicSourceUrl);
            if (storedSource is not null && string.Equals(storedSource, source, StringComparison.Ordinal))
            {
                return Duplicate(item.Slug, DuplicateSource);
            }
        }

        foreach (var item in existing)
        {
            var storedSlug = string.IsNullOrWhiteSpace(item.Slug)
                ? OpportunityScreen.Slug(item.BusinessName ?? string.Empty)
                : item.Slug.Trim();
            if (string.Equals(storedSlug, slug, StringComparison.Ordinal))
            {
                return Duplicate(storedSlug, DuplicateName);
            }
        }

        var state = passesInitialScreen ? ScoredState : PreservedState;
        var notice = state == PreservedState
            ? "The business is preserved. No demonstration was manufactured. A crawler did not run. None was invented. Green does not send. Delivery remains NOT_SENT."
            : "The business is stored from the public source. A crawler did not run. None was invented. Green does not send. Delivery remains NOT_SENT.";
        return new DiscoveryDecision(true, false, true, state, notice, string.Empty, false, "NOT_SENT");
    }

    public static string? CanonicalSource(string? sourceUrl)
    {
        if (!OpportunityScreen.IsPublicSource(sourceUrl ?? string.Empty))
        {
            return null;
        }

        var uri = new Uri((sourceUrl ?? string.Empty).Trim());
        var path = uri.AbsolutePath;
        if (path.Length > 1)
        {
            path = path.TrimEnd('/');
        }

        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        return uri.Scheme.ToLowerInvariant() + "://" + uri.Host.ToLowerInvariant() + port + path + uri.Query;
    }

    private static DiscoveryDecision Refuse(string notice) =>
        new(false, false, false, string.Empty, notice, string.Empty, false, "NOT_SENT");

    private static DiscoveryDecision Duplicate(string? slug, string notice) =>
        new(true, true, false, string.Empty, notice, (slug ?? string.Empty).Trim(), false, "NOT_SENT");
}
