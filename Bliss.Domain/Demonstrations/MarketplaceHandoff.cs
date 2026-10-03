using Bliss.Domain.Entities;
using Bliss.Domain.Rules;

namespace Bliss.Domain.Demonstrations;

public sealed record HandoffAdvertiser(
    string? TenantKey,
    string? BusinessName,
    string? PublicSourceUrl,
    string? Country,
    string? Language,
    string? Category,
    int Score,
    string? ProspectState,
    bool Suppressed);

public sealed record HandoffCreator(
    Guid Id,
    string? Name,
    string? CountryCode,
    string? PrimaryLanguage,
    int? AudienceSize);

public sealed record HandoffRecord(string TenantKey, Guid CreatorId, string SourceUrl);

public sealed record HandoffReading(
    string Notice,
    bool GreenMeansSend,
    string Delivery,
    bool EvaluatorInvoked,
    bool Duplicate,
    bool Qualified,
    bool WinClaimed,
    string MatchStatus,
    decimal? OverallScore,
    string BusinessName,
    string CreatorName,
    string SourceUrl,
    string TenantKey);

/// <summary>
/// Hands one qualified advertiser and one stored creator to the accepted evaluator.
/// It does not score the pair itself and it does not open a match certificate.
/// </summary>
public static class MarketplaceHandoff
{
    public const string Notice =
        "A qualified advertiser and a stored creator are handed to DeterministicRuleEvaluator. The handoff does not open a match certificate. A preserved advertiser is not handed off. A duplicate pair is not a second row. Another tenant's handoff is not shown. This is not a win. Green does not send. Delivery remains NOT_SENT.";

    public const string WithheldNotice =
        "This advertiser is not qualified. The accepted evaluator was not called. None was invented. This is not a win. Delivery remains NOT_SENT.";

    public const string DuplicateNotice =
        "That handoff is already recorded. The accepted evaluator was not called again. This is not a win. Delivery remains NOT_SENT.";

    public static HandoffReading Hand(
        HandoffAdvertiser? advertiser,
        HandoffCreator? creator,
        RuleDocument? rules,
        IReadOnlyList<HandoffRecord>? existing)
    {
        if (advertiser is null || creator is null || existing is null)
        {
            throw new InvalidOperationException(
                "A handoff needs the stored advertiser and the stored creator. None is invented.");
        }

        var tenant = (advertiser.TenantKey ?? string.Empty).Trim();
        var name = (advertiser.BusinessName ?? string.Empty).Trim();
        var source = SourceKey(advertiser.PublicSourceUrl);
        if (tenant.Length < 3 || name.Length < 3 || source.Length < 8)
        {
            return Withheld(name, tenant, source);
        }

        var preserved = string.Equals((advertiser.ProspectState ?? string.Empty).Trim(), "PRESERVED", StringComparison.Ordinal);
        if (advertiser.Score < 100 || preserved || advertiser.Suppressed)
        {
            return Withheld(name, tenant, source);
        }

        var creatorName = (creator.Name ?? string.Empty).Trim();
        if (creator.Id == Guid.Empty || creatorName.Length < 3)
        {
            throw new InvalidOperationException("A stored creator is required. None is invented.");
        }

        if (existing.Any(item => SamePair(item, tenant, creator.Id, source)))
        {
            return new HandoffReading(
                DuplicateNotice,
                false,
                "NOT_SENT",
                false,
                true,
                true,
                false,
                string.Empty,
                null,
                name,
                creatorName,
                source,
                tenant);
        }

        if (rules is null)
        {
            throw new InvalidOperationException("An active rule version is required. None was invented.");
        }

        var country = (advertiser.Country ?? string.Empty).Trim();
        var opportunity = new AdvertiserOpportunity
        {
            Name = name,
            Status = "ACTIVE",
            Category = string.IsNullOrWhiteSpace(advertiser.Category) ? null : advertiser.Category.Trim(),
            Language = string.IsNullOrWhiteSpace(advertiser.Language) ? null : advertiser.Language.Trim(),
            MarketCountryCode = country.Length >= 2 ? country : null
        };
        var storedCreator = new Creator
        {
            Id = creator.Id,
            Name = creatorName,
            CountryCode = string.IsNullOrWhiteSpace(creator.CountryCode) ? null : creator.CountryCode.Trim(),
            PrimaryLanguage = string.IsNullOrWhiteSpace(creator.PrimaryLanguage) ? null : creator.PrimaryLanguage.Trim(),
            AudienceSize = creator.AudienceSize
        };
        var result = DeterministicRuleEvaluator.Evaluate(storedCreator, opportunity, rules);
        var countryNote = country.Length == 2
            ? string.Empty
            : " The stored country was not converted into a code.";
        return new HandoffReading(
            "DeterministicRuleEvaluator returned " + result.MatchStatus + ". This handoff is not a win. A match certificate was not opened." + countryNote + " Delivery remains NOT_SENT.",
            false,
            "NOT_SENT",
            true,
            false,
            true,
            false,
            result.MatchStatus,
            result.OverallScore,
            name,
            creatorName,
            source,
            tenant);
    }

    public static IReadOnlyList<HandoffRecord> Visible(string? tenant, IReadOnlyList<HandoffRecord>? rows)
    {
        if (rows is null)
        {
            throw new InvalidOperationException("A tenant reading needs the stored handoffs. None is invented.");
        }

        var key = (tenant ?? string.Empty).Trim();
        if (key.Length < 3)
        {
            throw new InvalidOperationException("A tenant is required. Another advertiser's handoff is not shown.");
        }

        return rows.Where(item => string.Equals(item.TenantKey, key, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static HandoffReading Withheld(string name, string tenant, string source) => new(
        WithheldNotice,
        false,
        "NOT_SENT",
        false,
        false,
        false,
        false,
        string.Empty,
        null,
        name,
        string.Empty,
        source,
        tenant);

    private static bool SamePair(HandoffRecord item, string tenant, Guid creatorId, string source) =>
        item.CreatorId == creatorId
        && string.Equals(item.TenantKey, tenant, StringComparison.OrdinalIgnoreCase)
        && string.Equals(SourceKey(item.SourceUrl), source, StringComparison.OrdinalIgnoreCase);

    private static string SourceKey(string? url)
    {
        var value = (url ?? string.Empty).Trim();
        while (value.EndsWith('/'))
        {
            value = value[..^1];
        }

        return value;
    }
}
