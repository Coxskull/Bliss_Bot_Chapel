using Bliss.Domain.Demonstrations;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;

namespace Bliss.Tests.Demonstrations;

public sealed class MarketplaceHandoffTests
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
    public void A_qualified_pair_keeps_the_evaluator_result_and_does_not_claim_a_win()
    {
        var creatorId = Guid.Parse("11111111-1111-1111-1111-111111111113");
        var reading = MarketplaceHandoff.Hand(
            Advertiser("casa-verde", "Casa Verde", "https://example.com/casa-verde/", "Panama", "es", 100, "DEMONSTRATION_PREPARED"),
            new HandoffCreator(creatorId, "Test Creator Brazil", "BR", "Portuguese", 80000),
            Rules,
            []);
        var expected = DeterministicRuleEvaluator.Evaluate(
            new Creator
            {
                Id = creatorId,
                Name = "Test Creator Brazil",
                CountryCode = "BR",
                PrimaryLanguage = "Portuguese",
                AudienceSize = 80000
            },
            new AdvertiserOpportunity
            {
                Name = "Casa Verde",
                Status = "ACTIVE",
                Category = "restaurant",
                Language = "es",
                MarketCountryCode = "Panama"
            },
            Rules);

        Assert.True(reading.EvaluatorInvoked);
        Assert.False(reading.Duplicate);
        Assert.True(reading.Qualified);
        Assert.False(reading.WinClaimed);
        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal(expected.MatchStatus, reading.MatchStatus);
        Assert.Equal(expected.OverallScore, reading.OverallScore);
        Assert.Equal("INELIGIBLE", reading.MatchStatus);
        Assert.Equal("https://example.com/casa-verde", reading.SourceUrl);
        Assert.Contains("not a win", reading.Notice);
        Assert.Contains("not converted into a code", reading.Notice);
        Assert.Contains("not opened", reading.Notice);
    }

    [Fact]
    public void An_approved_evaluator_result_is_copied_and_still_is_not_a_win()
    {
        var creatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var reading = MarketplaceHandoff.Hand(
            Advertiser("mesa-norte", "Mesa Norte", "https://example.com/mesa-norte", "PA", "es", 100, "DEMONSTRATION_PREPARED"),
            new HandoffCreator(creatorId, "Stored Creator", "PA", "es", 20000),
            Rules,
            []);
        var expected = DeterministicRuleEvaluator.Evaluate(
            new Creator { Id = creatorId, Name = "Stored Creator", CountryCode = "PA", PrimaryLanguage = "es", AudienceSize = 20000 },
            new AdvertiserOpportunity { Name = "Mesa Norte", Status = "ACTIVE", Category = "restaurant", Language = "es", MarketCountryCode = "PA" },
            Rules);

        Assert.Equal(expected.MatchStatus, reading.MatchStatus);
        Assert.Equal(expected.OverallScore, reading.OverallScore);
        Assert.Equal("APPROVED", reading.MatchStatus);
        Assert.False(reading.WinClaimed);
        Assert.DoesNotContain("not converted", reading.Notice);
        Assert.Equal("NOT_SENT", reading.Delivery);
    }

    [Fact]
    public void A_preserved_advertiser_and_a_duplicate_pair_do_not_call_the_evaluator()
    {
        var creator = new HandoffCreator(Guid.Parse("11111111-1111-1111-1111-111111111113"), "Test Creator Brazil", "BR", "Portuguese", 80000);
        var preserved = MarketplaceHandoff.Hand(
            Advertiser("puerto-azul", "Puerto Azul", "https://example.com/puerto-azul", "Ecuador", "en", 75, "PRESERVED"),
            creator,
            Rules,
            []);
        Assert.False(preserved.Qualified);
        Assert.False(preserved.EvaluatorInvoked);
        Assert.Equal(MarketplaceHandoff.WithheldNotice, preserved.Notice);
        Assert.Equal("NOT_SENT", preserved.Delivery);

        var low = MarketplaceHandoff.Hand(
            Advertiser("abc-pharmacy", "ABC Pharmacy", "https://example.com/abc", "Panama", "es", 0, ""),
            creator,
            null,
            []);
        Assert.False(low.EvaluatorInvoked);

        var duplicate = MarketplaceHandoff.Hand(
            Advertiser("casa-verde", "Casa Verde", "https://example.com/casa-verde", "Panama", "es", 100, "DEMONSTRATION_PREPARED"),
            creator,
            null,
            [new HandoffRecord("casa-verde", creator.Id, "https://example.com/casa-verde/")]);
        Assert.True(duplicate.Duplicate);
        Assert.False(duplicate.EvaluatorInvoked);
        Assert.Equal(MarketplaceHandoff.DuplicateNotice, duplicate.Notice);
        Assert.False(duplicate.WinClaimed);
    }

    [Fact]
    public void A_tenant_reading_hides_the_other_advertiser()
    {
        var rows = new[]
        {
            new HandoffRecord("casa-verde", Guid.NewGuid(), "https://example.com/casa-verde"),
            new HandoffRecord("mesa-norte", Guid.NewGuid(), "https://example.com/mesa-norte")
        };
        var visible = MarketplaceHandoff.Visible("mesa-norte", rows);
        Assert.Single(visible);
        Assert.Equal("mesa-norte", visible[0].TenantKey);
        var missing = Assert.Throws<InvalidOperationException>(() => MarketplaceHandoff.Visible("no", rows));
        Assert.Contains("not shown", missing.Message);
        var invented = Assert.Throws<InvalidOperationException>(() => MarketplaceHandoff.Hand(null, null, Rules, null));
        Assert.Contains("None is invented", invented.Message);
    }

    [Fact]
    public void Handoff_source_uses_the_accepted_evaluator_and_does_not_score()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "MarketplaceHandoff.cs"));
        Assert.Contains("DeterministicRuleEvaluator.Evaluate", source);
        Assert.Contains("not a win", source);
        Assert.Contains("not shown", source);
        Assert.DoesNotContain("Weights", source);
        Assert.DoesNotContain("BlissMatch", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }

    private static HandoffAdvertiser Advertiser(
        string tenant,
        string name,
        string source,
        string country,
        string language,
        int score,
        string state) =>
        new(tenant, name, source, country, language, "restaurant", score, state, false);
}
