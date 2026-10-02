namespace Bliss.Domain.Demonstrations;

public sealed record MarketDefinition(string City, string Country, string Language);

public static class InitialMarkets
{
    public static readonly MarketDefinition[] All =
    [
        new("Santo Domingo", "Dominican Republic", "es"),
        new("Panama City", "Panama", "es"),
        new("Medellín", "Colombia", "es"),
        new("Manila", "Philippines", "en"),
        new("Kuala Lumpur", "Malaysia", "en"),
        new("Jakarta", "Indonesia", "en")
    ];

    public static MarketDefinition? Find(string city) =>
        All.FirstOrDefault(x => string.Equals(x.City, (city ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record OpportunityResult(
    int Score,
    bool PassesInitialScreen,
    string Language,
    string Country,
    IReadOnlyList<string> Reasons,
    bool PreservesBusiness);

public static class OpportunityScreen
{
    public static OpportunityResult Evaluate(string niche, string market, string businessName, string sourceUrl)
    {
        var reasons = new List<string>();
        var score = 0;
        var knownNiche = BuyingRoleCatalog.Niches.Contains((niche ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);
        if (knownNiche)
        {
            score += 25;
        }
        else
        {
            reasons.Add("The niche is outside the configured local-business catalog.");
        }

        var city = InitialMarkets.Find(market);
        if (city is not null)
        {
            score += 25;
        }
        else
        {
            reasons.Add("The market is outside the initial six cities.");
        }

        var name = (businessName ?? string.Empty).Trim();
        if (name.Length >= 3)
        {
            score += 25;
        }
        else
        {
            reasons.Add("A business name from a public source is required. Alpha will not invent one.");
        }

        if (IsPublicSource(sourceUrl))
        {
            score += 25;
        }
        else
        {
            reasons.Add("A public http or https source URL is required before a demonstration is allowed.");
        }

        return new OpportunityResult(
            score,
            score == 100,
            city?.Language ?? "en",
            city?.Country ?? string.Empty,
            reasons,
            name.Length >= 3 && IsPublicSource(sourceUrl));
    }

    public static bool IsPublicSource(string sourceUrl)
    {
        if (!Uri.TryCreate((sourceUrl ?? string.Empty).Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https" && !string.IsNullOrWhiteSpace(uri.Host);
    }

    public static string Slug(string businessName)
    {
        var buffer = new System.Text.StringBuilder();
        var pendingDash = false;
        foreach (var character in businessName.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingDash && buffer.Length > 0)
                {
                    buffer.Append('-');
                }

                pendingDash = false;
                buffer.Append(character);
            }
            else
            {
                pendingDash = true;
            }
        }

        return buffer.ToString();
    }
}
