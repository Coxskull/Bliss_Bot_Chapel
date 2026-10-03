using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class AskAlphaTests
{
    [Fact]
    public void An_explanation_answers_and_then_advances_in_one_voice()
    {
        var first = DemonstrationConversation.Reply(Facts(), "How does this work?");

        Assert.Equal(DemonstrationConversation.Voice, "Ask Alpha");
        Assert.StartsWith("Thank you.", first.Reply);
        Assert.Contains("has not commissioned", first.Reply);
        Assert.Contains("private look", first.Reply);
        Assert.Contains("ask what a human handoff requires", first.Reply);
        Assert.Equal("teaching", first.Gear);
        Assert.DoesNotContain("Sir", first.Reply);
        Assert.DoesNotContain("Madam", first.Reply);
        Assert.DoesNotContain("$", first.Reply);

        var repeat = DemonstrationConversation.Reply(Facts() with { LastSignal = "EXPLANATION" }, "How does Alpha work?");
        Assert.Contains("already answered", repeat.Reply);
        Assert.Contains("has not commissioned", repeat.Reply);
        Assert.Contains("human handoff when you want one", repeat.Reply);
        Assert.DoesNotContain("ask what a human handoff requires", repeat.Reply);
        Assert.Equal("teaching", repeat.Gear);
    }

    [Fact]
    public void A_price_question_advances_without_a_number()
    {
        var turn = DemonstrationConversation.Reply(Facts(), "How much does this cost?");

        Assert.Contains("cannot invent a price", turn.Reply);
        Assert.Contains("The next step is a human handoff.", turn.Reply);
        Assert.Equal("integrity", turn.Gear);
        Assert.DoesNotContain("$", turn.Reply);
        Assert.False(turn.HumanEscalation);
    }

    [Fact]
    public void A_verified_name_is_the_address_and_a_title_is_not_chosen()
    {
        var facts = Facts() with
        {
            DecisionMakerName = "Ana Ruiz",
            DecisionMakerRole = "Owner",
            Confidence = "MEDIUM",
            PersonalizationAllowed = true,
            Freshness = "CURRENT"
        };
        var turn = DemonstrationConversation.Reply(facts, "Who is the owner?");
        Assert.StartsWith("Ana Ruiz,", turn.Reply);
        Assert.DoesNotContain("Madam", turn.Reply);
        Assert.DoesNotContain("Señor", turn.Reply);
    }

    private static ProspectFacts Facts() => new(
        "Mesa Norte",
        "TIER_4",
        "No verified mailbox is on file.",
        BuyingRoleCatalog.RolesFor("restaurant"),
        ["Table Concept"]);
}
