using Bliss.Domain.Common;
using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;

namespace Bliss.Tests.Demonstrations;

public sealed class BlissRematchTests
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
    public void An_approved_alternate_is_the_evaluator_result()
    {
        var current = Creator("Current Harbor", "PH", "English", 20000);
        var approved = Creator("Luz Canal", "PA", "English", 25000);
        var far = Creator("Far Coast", "US", "English", 30000);
        var opportunity = Opportunity("PA", "English", EntityStatuses.Active);
        var expected = DeterministicRuleEvaluator.Evaluate(approved, opportunity, Rules);

        var decision = BlissRematch.Select(current.Id, opportunity, Rules, [current, far, approved]);

        Assert.Equal(approved.Id, decision.CreatorId);
        Assert.Equal("Luz Canal", decision.CreatorName);
        Assert.Equal(EntityStatuses.Approved, decision.MatchStatus);
        Assert.Equal(expected.OverallScore, decision.OverallScore);
        Assert.Equal(EntityStatuses.Approved, expected.MatchStatus);
        Assert.Contains("Bliss approved Luz Canal through the accepted evaluator", decision.Notice);
        Assert.Contains("not a win", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
        Assert.DoesNotContain("Current Harbor", decision.Notice);
        Assert.DoesNotContain("Far Coast", decision.Notice);
        Assert.DoesNotContain("$", decision.Notice);
        Assert.DoesNotContain(decision.OverallScore!.Value.ToString(), decision.Notice);
    }

    [Fact]
    public void Equal_scores_break_the_tie_by_name()
    {
        var first = Creator("Luz Canal", "PA", "English", 20000);
        var second = Creator("Harbor Voice", "PA", "English", 20000);
        var opportunity = Opportunity("PA", "English", EntityStatuses.Active);

        var decision = BlissRematch.Select(null, opportunity, Rules, [first, second]);

        Assert.Equal("Harbor Voice", decision.CreatorName);
        Assert.Equal(1.0m, decision.OverallScore);
    }

    [Fact]
    public void No_approved_alternate_keeps_the_advertiser()
    {
        var current = Creator("Current Harbor", "PH", "English", 20000);
        var far = Creator("Far Coast", "US", "English", 30000);
        var decision = BlissRematch.Select(
            current.Id,
            Opportunity("PA", "English", EntityStatuses.Active),
            Rules,
            [current, far]);

        Assert.Null(decision.CreatorId);
        Assert.Equal(string.Empty, decision.CreatorName);
        Assert.Equal(BlissRematch.NoMatchNotice, decision.Notice);
        Assert.Contains("advertiser is kept", decision.Notice);
        Assert.DoesNotContain("Luz Canal", decision.Notice);
        Assert.DoesNotContain("Far Coast", decision.Notice);
        Assert.DoesNotContain("$", decision.Notice);
    }

    [Fact]
    public void A_missing_or_inactive_opportunity_names_nobody()
    {
        var approved = Creator("Luz Canal", "PA", "English", 25000);
        var missing = BlissRematch.Select(null, null, Rules, [approved]);
        var inactive = BlissRematch.Select(null, Opportunity("PA", "English", "PAUSED"), Rules, [approved]);
        var noRules = BlissRematch.Select(null, Opportunity("PA", "English", EntityStatuses.Active), null, [approved]);

        Assert.Equal(BlissRematch.NoOpportunityNotice, missing.Notice);
        Assert.Equal(BlissRematch.NoOpportunityNotice, inactive.Notice);
        Assert.Equal(BlissRematch.NoOpportunityNotice, noRules.Notice);
        Assert.DoesNotContain("Luz Canal", missing.Notice + inactive.Notice + noRules.Notice);
        Assert.Null(missing.OverallScore);
    }

    [Fact]
    public void Rematch_source_calls_the_accepted_evaluator()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "BlissRematch.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("DeterministicRuleEvaluator", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("WhatsApp", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$", source);
        Assert.DoesNotContain("WeddingPlanner", source);
        Assert.DoesNotContain("MatchRuleEvaluationService", source);
    }

    private static Creator Creator(string name, string country, string language, int audience) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        CountryCode = country,
        PrimaryLanguage = language,
        AudienceSize = audience,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static AdvertiserOpportunity Opportunity(string country, string language, string status) => new()
    {
        Id = Guid.NewGuid(),
        AdvertiserProgramId = Guid.NewGuid(),
        Name = "Fixture opportunity",
        Category = "Creator Tools",
        MarketCountryCode = country,
        Language = language,
        Status = status
    };
}
