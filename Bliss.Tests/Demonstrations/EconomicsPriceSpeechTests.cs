using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class EconomicsPriceSpeechTests
{
    [Fact]
    public void A_missing_result_states_no_number()
    {
        var reply = EconomicsPriceSpeech.PricingReply(null, null);

        Assert.Contains("cannot invent a price", reply);
        Assert.DoesNotMatch(@"\d", reply);
        Assert.DoesNotContain("$", reply);
        Assert.Null(EconomicsPriceSpeech.Speak("215", "US"));
        Assert.Null(EconomicsPriceSpeech.Speak("$215", "USD"));
        Assert.Null(EconomicsPriceSpeech.Speak("100-300", "PHP"));
    }

    [Fact]
    public void An_accepted_result_is_the_only_number()
    {
        var spoken = EconomicsPriceSpeech.Speak("215", "php");
        var reply = EconomicsPriceSpeech.PricingReply("215", "PHP");

        Assert.Equal("215 PHP", spoken);
        Assert.Contains("Economics accepted 215 PHP", reply);
        Assert.Contains("only number", reply);
        Assert.Contains("cannot invent a price", reply);
        Assert.Contains("not a win", reply);
        Assert.DoesNotContain("$", reply);
        Assert.DoesNotContain("216", reply);
        Assert.Equal("215", EconomicsPriceSpeech.FormatAmount(215m));
    }

    [Fact]
    public void Ask_Alpha_speaks_the_accepted_amount_and_refuses_without_one()
    {
        var facts = new ProspectFacts(
            "Mesa Norte",
            "TIER_4",
            "No verified mailbox is on file.",
            BuyingRoleCatalog.RolesFor("restaurant"),
            ["Table Concept"]);
        var refused = DemonstrationConversation.Reply(facts, "How much does this cost?");
        Assert.Contains("cannot invent a price", refused.Reply);
        Assert.DoesNotMatch(@"\d", refused.Reply);
        Assert.Equal("integrity", refused.Gear);

        var accepted = DemonstrationConversation.Reply(
            facts with { AcceptedEconomicsAmount = "215", AcceptedEconomicsCurrency = "PHP" },
            "How much does this cost?");
        Assert.Contains("Economics accepted 215 PHP", accepted.Reply);
        Assert.Contains("cannot invent a price", accepted.Reply);
        Assert.DoesNotContain("$", accepted.Reply);
        Assert.StartsWith("Thank you.", accepted.Reply);
    }
}
