using System.Text.Json;
using System.Text.Json.Serialization;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;

namespace Bliss.Domain.Rules;

public sealed class RuleDocument
{
    public int? MinAudienceSize { get; set; }

    public bool RequireOpportunityMarketCountry { get; set; } = true;

    public bool RequireLanguageOverlap { get; set; } = true;

    public IReadOnlyList<string> ProhibitedCategories { get; set; } = Array.Empty<string>();

    public RuleWeights Weights { get; set; } = new();
}

public sealed class RuleWeights
{
    public decimal Geography { get; set; } = 0.4m;

    public decimal AudienceSize { get; set; } = 0.3m;

    public decimal Language { get; set; } = 0.3m;
}

public sealed record EligibilityLine(
    string CheckType,
    string Result,
    string? ReasonCode,
    string Explanation);

public sealed record ScoreLine(
    string ComponentName,
    decimal Score,
    decimal Weight,
    string Explanation);

public sealed record RuleEvaluationResult(
    IReadOnlyList<EligibilityLine> Checks,
    IReadOnlyList<ScoreLine> Scores,
    decimal OverallScore,
    decimal ConfidenceScore,
    string MatchStatus);

/// <summary>
/// Deterministic evaluator. AI is not used. UNKNOWN inputs are never treated as zero.
/// </summary>
public static class DeterministicRuleEvaluator
{
    public static RuleDocument ParseDocument(string? documentJson)
    {
        if (string.IsNullOrWhiteSpace(documentJson))
        {
            throw new InvalidOperationException("RuleVersion has no DocumentJson.");
        }

        var document = JsonSerializer.Deserialize<RuleDocument>(documentJson, JsonOptions);
        return document ?? throw new InvalidOperationException("RuleVersion DocumentJson is empty.");
    }

    public static RuleEvaluationResult Evaluate(
        Creator creator,
        AdvertiserOpportunity opportunity,
        RuleDocument rules)
    {
        var checks = new List<EligibilityLine>
        {
            EvaluateOpportunityStatus(opportunity),
            EvaluateCategory(opportunity, rules),
            EvaluateGeography(creator, opportunity, rules),
            EvaluateAudience(creator, rules),
            EvaluateLanguage(creator, opportunity, rules)
        };

        var geographyScore = ScoreFromResult(checks.Single(c => c.CheckType == "GEOGRAPHY").Result);
        var audienceScore = ScoreFromResult(checks.Single(c => c.CheckType == "AUDIENCE_SIZE").Result);
        var languageScore = ScoreFromResult(checks.Single(c => c.CheckType == "LANGUAGE").Result);

        var scores = new List<ScoreLine>
        {
            new("GEOGRAPHY", geographyScore, rules.Weights.Geography, "Deterministic geography fit from RuleVersion document."),
            new("AUDIENCE_SIZE", audienceScore, rules.Weights.AudienceSize, "Deterministic audience fit. UNKNOWN audience is not scored as zero."),
            new("LANGUAGE", languageScore, rules.Weights.Language, "Deterministic language overlap from RuleVersion document.")
        };

        var overall =
            geographyScore * rules.Weights.Geography
            + audienceScore * rules.Weights.AudienceSize
            + languageScore * rules.Weights.Language;

        var hasIneligible = checks.Any(c => c.Result == EntityStatuses.Ineligible);
        var hasReview = checks.Any(c => c.Result == EntityStatuses.ReviewRequired);
        var matchStatus = hasIneligible
            ? EntityStatuses.Ineligible
            : hasReview
                ? EntityStatuses.ReviewRequired
                : EntityStatuses.Approved;

        var confidence = hasIneligible ? 0.9m : hasReview ? 0.4m : 0.85m;

        return new RuleEvaluationResult(checks, scores, decimal.Round(overall, 4), confidence, matchStatus);
    }

    private static EligibilityLine EvaluateOpportunityStatus(AdvertiserOpportunity opportunity)
    {
        if (!string.Equals(opportunity.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            return new EligibilityLine(
                "OPPORTUNITY_STATUS",
                EntityStatuses.Ineligible,
                EligibilityReasonCodes.OpportunityInactive,
                "Opportunity status is not ACTIVE.");
        }

        return new EligibilityLine("OPPORTUNITY_STATUS", EntityStatuses.Approved, null, "Opportunity is ACTIVE.");
    }

    private static EligibilityLine EvaluateCategory(AdvertiserOpportunity opportunity, RuleDocument rules)
    {
        if (string.IsNullOrWhiteSpace(opportunity.Category) || rules.ProhibitedCategories.Count == 0)
        {
            return new EligibilityLine("CATEGORY", EntityStatuses.Approved, null, "No prohibited category matched.");
        }

        var prohibited = rules.ProhibitedCategories.Any(p =>
            string.Equals(p, opportunity.Category, StringComparison.OrdinalIgnoreCase));
        if (prohibited)
        {
            return new EligibilityLine(
                "CATEGORY",
                EntityStatuses.Ineligible,
                EligibilityReasonCodes.CategoryProhibited,
                $"Category '{opportunity.Category}' is prohibited by the rule document.");
        }

        return new EligibilityLine("CATEGORY", EntityStatuses.Approved, null, "Category is allowed.");
    }

    private static EligibilityLine EvaluateGeography(
        Creator creator,
        AdvertiserOpportunity opportunity,
        RuleDocument rules)
    {
        if (!rules.RequireOpportunityMarketCountry || string.IsNullOrWhiteSpace(opportunity.MarketCountryCode))
        {
            return new EligibilityLine("GEOGRAPHY", EntityStatuses.Approved, null, "Market country is not required.");
        }

        if (string.IsNullOrWhiteSpace(creator.CountryCode))
        {
            return new EligibilityLine(
                "GEOGRAPHY",
                EntityStatuses.ReviewRequired,
                EligibilityReasonCodes.GeoUnknown,
                "Creator country is UNKNOWN; not treated as a failed zero match.");
        }

        if (!string.Equals(creator.CountryCode, opportunity.MarketCountryCode, StringComparison.OrdinalIgnoreCase))
        {
            return new EligibilityLine(
                "GEOGRAPHY",
                EntityStatuses.Ineligible,
                EligibilityReasonCodes.GeoNotEligible,
                $"Creator country {creator.CountryCode} is not {opportunity.MarketCountryCode}.");
        }

        return new EligibilityLine("GEOGRAPHY", EntityStatuses.Approved, null, "Creator country matches opportunity market.");
    }

    private static EligibilityLine EvaluateAudience(Creator creator, RuleDocument rules)
    {
        if (rules.MinAudienceSize is null)
        {
            return new EligibilityLine("AUDIENCE_SIZE", EntityStatuses.Approved, null, "No minimum audience is configured.");
        }

        if (creator.AudienceSize is null)
        {
            return new EligibilityLine(
                "AUDIENCE_SIZE",
                EntityStatuses.ReviewRequired,
                EligibilityReasonCodes.MinimumAudienceUnknown,
                "Audience size is UNKNOWN; not treated as zero.");
        }

        if (creator.AudienceSize.Value < rules.MinAudienceSize.Value)
        {
            return new EligibilityLine(
                "AUDIENCE_SIZE",
                EntityStatuses.Ineligible,
                EligibilityReasonCodes.MinimumAudienceNotMet,
                $"Audience {creator.AudienceSize} is below minimum {rules.MinAudienceSize}.");
        }

        return new EligibilityLine("AUDIENCE_SIZE", EntityStatuses.Approved, null, "Audience meets the configured minimum.");
    }

    private static EligibilityLine EvaluateLanguage(
        Creator creator,
        AdvertiserOpportunity opportunity,
        RuleDocument rules)
    {
        if (!rules.RequireLanguageOverlap || string.IsNullOrWhiteSpace(opportunity.Language))
        {
            return new EligibilityLine("LANGUAGE", EntityStatuses.Approved, null, "Language overlap is not required.");
        }

        if (string.IsNullOrWhiteSpace(creator.PrimaryLanguage))
        {
            return new EligibilityLine(
                "LANGUAGE",
                EntityStatuses.ReviewRequired,
                EligibilityReasonCodes.LanguageUnknown,
                "Creator language is UNKNOWN; not treated as zero compatibility.");
        }

        var creatorTokens = SplitTokens(creator.PrimaryLanguage);
        var opportunityTokens = SplitTokens(opportunity.Language);
        if (!creatorTokens.Overlaps(opportunityTokens))
        {
            return new EligibilityLine(
                "LANGUAGE",
                EntityStatuses.Ineligible,
                EligibilityReasonCodes.LanguageNotEligible,
                "Creator language tokens do not overlap the opportunity language.");
        }

        return new EligibilityLine("LANGUAGE", EntityStatuses.Approved, null, "Language tokens overlap.");
    }

    private static decimal ScoreFromResult(string result)
    {
        if (result == EntityStatuses.Approved)
        {
            return 1.0m;
        }

        if (result == EntityStatuses.ReviewRequired)
        {
            return 0.5m;
        }

        return 0.0m;
    }

    private static HashSet<string> SplitTokens(string value)
    {
        return value
            .Split(new[] { '/', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToUpperInvariant())
            .ToHashSet();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
