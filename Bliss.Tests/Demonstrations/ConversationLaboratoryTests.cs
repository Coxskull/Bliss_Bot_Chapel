using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class ConversationLaboratoryTests
{
    [Fact]
    public void Every_persona_scenario_passes_without_a_behavior_change()
    {
        var report = ConversationLaboratory.Run();

        Assert.Equal(10, report.ScenarioCount);
        Assert.Equal(10, report.PassedCount);
        Assert.True(report.Passed);
        Assert.Equal(ConversationLaboratory.Notice, report.Notice);
        Assert.Equal("Ask Alpha", report.Voice);
        Assert.Contains("Production conversation was not changed", report.Notice);
        Assert.Contains("NOT_SENT", report.Notice);
        Assert.Equal(10, report.Results.Select(item => item.Id).Distinct().Count());
        Assert.All(report.Results, item => Assert.True(item.Passed, item.Id + " " + string.Join("; ", item.Missing)));
    }

    [Fact]
    public void The_unverified_price_scenario_states_no_number()
    {
        var price = ConversationLaboratory.Run().Results.Single(item => item.Id == "unverified-price");
        Assert.Contains("cannot invent a price", price.Reply);
        Assert.DoesNotMatch(@"\d", price.Reply);
        Assert.DoesNotContain("$", price.Reply);
    }

    [Fact]
    public void A_stale_fixture_name_is_not_spoken_and_the_economics_fixture_is_the_only_number()
    {
        var report = ConversationLaboratory.Run();
        var stale = report.Results.Single(item => item.Id == "stale-name");
        var economics = report.Results.Single(item => item.Id == "economics-fixture");
        Assert.DoesNotContain("Ana Ruiz", stale.Reply);
        Assert.Contains("will not address anyone by a personal name", stale.Reply);
        Assert.Contains("215 PHP", economics.Reply);
        Assert.DoesNotContain("200", economics.Reply);
        Assert.Contains("not a win", economics.Reply);
    }

    [Fact]
    public void Laboratory_source_calls_the_production_replies()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "ConversationLaboratory.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("DemonstrationConversation.Reply", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("ProspectDemonstrationStore", source);
    }
}
