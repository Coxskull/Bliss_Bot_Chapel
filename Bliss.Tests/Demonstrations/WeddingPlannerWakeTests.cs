using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class WeddingPlannerWakeTests
{
    [Fact]
    public void A_discovered_business_without_an_accepted_result_stays_asleep()
    {
        var decision = WeddingPlannerWake.Decide("Mesa Norte", "Panama City", false, null, null, Guid.NewGuid());

        Assert.Equal(WeddingPlannerWake.Asleep, decision.Status);
        Assert.Equal(WeddingPlannerWake.AsleepNotice, decision.Notice);
        Assert.Contains("not planned", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
        Assert.Null(decision.WorkspaceId);
        Assert.DoesNotMatch(@"\d", decision.Notice);
        Assert.DoesNotContain("Mesa Norte", decision.Notice);
    }

    [Fact]
    public void Suppression_comes_before_a_campaign()
    {
        var accepted = new AcceptedEconomicsPrice("215", "PHP");
        var decision = WeddingPlannerWake.Decide("Mesa Norte", "Panama City", true, accepted, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(WeddingPlannerWake.Asleep, decision.Status);
        Assert.Equal(WeddingPlannerWake.SuppressedNotice, decision.Notice);
        Assert.Contains("Suppression comes before a campaign", decision.Notice);
        Assert.Null(decision.WorkspaceId);
        Assert.DoesNotContain("215", decision.Notice);
    }

    [Fact]
    public void An_accepted_result_without_an_advertiser_opens_no_campaign()
    {
        var quoteId = Guid.NewGuid();
        var decision = WeddingPlannerWake.Decide(
            "Harbor Table",
            "Panama City",
            false,
            new AcceptedEconomicsPrice("215", "PHP"),
            quoteId,
            null);

        Assert.Equal(WeddingPlannerWake.ContextOnly, decision.Status);
        Assert.Contains("Harbor Table", decision.Notice);
        Assert.Contains("Panama City", decision.Notice);
        Assert.Contains("215 PHP", decision.Notice);
        Assert.Contains("no campaign is opened", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
        Assert.Equal(quoteId, decision.QuoteId);
        Assert.Null(decision.AdvertiserId);
        Assert.Null(decision.WorkspaceId);
    }

    [Fact]
    public void An_accepted_result_and_an_advertiser_wake_the_planner()
    {
        var quoteId = Guid.NewGuid();
        var advertiserId = Guid.NewGuid();
        var decision = WeddingPlannerWake.Decide(
            "Harbor Table",
            "Panama City",
            false,
            new AcceptedEconomicsPrice("215", "PHP"),
            quoteId,
            advertiserId);

        Assert.Equal(WeddingPlannerWake.Awake, decision.Status);
        Assert.Contains("Wedding Planner is awake for Harbor Table in Panama City", decision.Notice);
        Assert.Contains("accepted Economics amount is 215 PHP", decision.Notice);
        Assert.Contains("No campaign is planned", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
        Assert.DoesNotContain("win", decision.Notice);
        Assert.Equal(advertiserId, decision.AdvertiserId);
        Assert.Null(decision.WorkspaceId);
    }

    [Fact]
    public void Wake_source_does_not_plan_with_a_model()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "WeddingPlannerWake.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("EconomicsPriceSpeech", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("ChatClient", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("$", source);
    }
}
