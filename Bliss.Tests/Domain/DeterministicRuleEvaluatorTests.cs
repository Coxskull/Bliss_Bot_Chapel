using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;

namespace Bliss.Tests.Domain;

public sealed class DeterministicRuleEvaluatorTests
{
    private static readonly RuleDocument Rules = DeterministicRuleEvaluator.ParseDocument(Phase2Json);

    private const string Phase2Json = """
        {
          "minAudienceSize": 10000,
          "requireOpportunityMarketCountry": true,
          "requireLanguageOverlap": true,
          "prohibitedCategories": [ "Alcohol" ],
          "weights": { "geography": 0.4, "audienceSize": 0.3, "language": 0.3 }
        }
        """;

    [Fact]
    public void Matching_country_and_language_is_approved()
    {
        var result = DeterministicRuleEvaluator.Evaluate(
            Creator("BR", "Portuguese", 80000),
            Opportunity("BR", "Portuguese", "Creator Tools"),
            Rules);

        Assert.Equal(EntityStatuses.Approved, result.MatchStatus);
        Assert.Equal(1.0m, result.OverallScore);
        Assert.DoesNotContain(result.Checks, c => c.Result == EntityStatuses.Ineligible);
    }

    [Fact]
    public void Country_mismatch_is_geo_not_eligible_and_does_not_use_ai()
    {
        var result = DeterministicRuleEvaluator.Evaluate(
            Creator("PH", "English / Tagalog", 100000),
            Opportunity("BR", "Portuguese", "Creator Tools"),
            Rules);

        Assert.Equal(EntityStatuses.Ineligible, result.MatchStatus);
        var geo = result.Checks.Single(c => c.CheckType == "GEOGRAPHY");
        Assert.Equal(EligibilityReasonCodes.GeoNotEligible, geo.ReasonCode);
        Assert.Equal(0.0m, result.Scores.Single(s => s.ComponentName == "GEOGRAPHY").Score);
    }

    [Fact]
    public void Unknown_audience_is_review_required_not_zero()
    {
        var creator = Creator("PH", "English", audienceSize: null);
        Assert.Null(creator.AudienceSize);

        var result = DeterministicRuleEvaluator.Evaluate(
            creator,
            Opportunity("PH", "English", "Creator Tools"),
            Rules);

        Assert.Equal(EntityStatuses.ReviewRequired, result.MatchStatus);
        var audience = result.Checks.Single(c => c.CheckType == "AUDIENCE_SIZE");
        Assert.Equal(EligibilityReasonCodes.MinimumAudienceUnknown, audience.ReasonCode);
        Assert.Equal(0.5m, result.Scores.Single(s => s.ComponentName == "AUDIENCE_SIZE").Score);
        Assert.NotEqual(0m, result.Scores.Single(s => s.ComponentName == "AUDIENCE_SIZE").Score);
    }

    [Fact]
    public void Missing_document_throws()
    {
        Assert.Throws<InvalidOperationException>(() => DeterministicRuleEvaluator.ParseDocument(null));
    }

    private static Creator Creator(string? country, string? language, int? audienceSize) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Eval",
        CountryCode = country,
        PrimaryLanguage = language,
        AudienceSize = audienceSize,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static AdvertiserOpportunity Opportunity(string market, string language, string category) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Opp",
        MarketCountryCode = market,
        Language = language,
        Category = category,
        Status = EntityStatuses.Active
    };
}
