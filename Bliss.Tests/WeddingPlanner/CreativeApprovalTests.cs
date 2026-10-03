using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class CreativeApprovalTests
{
    private static readonly Guid Workspace = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid Other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");

    [Fact]
    public void A_human_approval_is_not_campaign_ready_and_calls_no_model()
    {
        var decision = CreativeApproval.Decide(Workspace, "Table card", "APPROVE", "OPERATOR", "creative-table-card", []);

        Assert.Equal(CreativeApproval.HumanApproved, decision.Status);
        Assert.Equal("Table card", decision.Title);
        Assert.Equal("OPERATOR", decision.ActorType);
        Assert.False(decision.Duplicate);
        Assert.False(decision.CampaignReady);
        Assert.False(decision.MatchWritten);
        Assert.False(decision.PriceInvented);
        Assert.Equal(0, decision.ModelCalls);
        Assert.False(decision.GreenMeansSend);
        Assert.Equal("NOT_SENT", decision.Delivery);
        Assert.Contains("No price was invented", decision.Notice);
        Assert.Contains("Six roles were not called", decision.Notice);
        Assert.Contains("not written", decision.Notice);
    }

    [Fact]
    public void A_withhold_stays_withheld_and_a_repeat_does_not_ask_again()
    {
        var withheld = CreativeApproval.Decide(Workspace, "Table card", "withhold", "advertiser", "creative-withhold", []);
        Assert.Equal(CreativeApproval.Withheld, withheld.Status);
        Assert.Equal("ADVERTISER", withheld.ActorType);
        Assert.False(withheld.CampaignReady);

        var again = CreativeApproval.Decide(
            Workspace,
            "A different title",
            "APPROVE",
            "OPERATOR",
            "creative-withhold",
            [new CreativeRecord(Workspace, "creative-withhold", CreativeApproval.Withheld, "Table card")]);
        Assert.True(again.Duplicate);
        Assert.Equal(CreativeApproval.Withheld, again.Status);
        Assert.Equal("Table card", again.Title);
        Assert.Equal(CreativeApproval.DuplicateNotice, again.Notice);
        Assert.Equal(0, again.ModelCalls);
    }

    [Fact]
    public void A_model_a_campaign_and_a_discovered_business_are_refused()
    {
        var model = Assert.Throws<InvalidOperationException>(() =>
            CreativeApproval.Decide(Workspace, "Table card", "APPROVE", "SYSTEM", "creative-table-card", []));
        Assert.Contains("model was not called", model.Message);
        var campaign = Assert.Throws<InvalidOperationException>(() =>
            CreativeApproval.Decide(Workspace, "Table card", "CAMPAIGN_READY", "OPERATOR", "creative-table-card", []));
        Assert.Contains("Campaign ready is refused", campaign.Message);
        var discovered = Assert.Throws<InvalidOperationException>(() =>
            CreativeApproval.Decide(Guid.Empty, "Table card", "APPROVE", "OPERATOR", "creative-table-card", []));
        Assert.Contains("discovered business is not opened", discovered.Message);
        var generated = Assert.Throws<InvalidOperationException>(() =>
            CreativeApproval.Decide(Workspace, "  ", "APPROVE", "OPERATOR", "creative-table-card", []));
        Assert.Contains("None was generated", generated.Message);
    }

    [Fact]
    public void Another_workspace_decision_is_not_shown()
    {
        var rows = new[]
        {
            new CreativeRecord(Workspace, "creative-table-card", CreativeApproval.HumanApproved, "Table card"),
            new CreativeRecord(Other, "creative-other", CreativeApproval.Withheld, "Other card")
        };
        var visible = CreativeApproval.Visible(Workspace, rows);
        Assert.Single(visible);
        Assert.Equal("Table card", visible[0].Title);
        var missing = Assert.Throws<InvalidOperationException>(() => CreativeApproval.Visible(Guid.Empty, rows));
        Assert.Contains("not opened", missing.Message);
    }

    [Fact]
    public void Creative_source_does_not_call_a_matcher_or_a_model()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "WeddingPlanner", "CreativeApproval.cs"));
        Assert.Contains("Six roles are not called", source);
        Assert.Contains("discovered business is not opened", source);
        Assert.Contains("Campaign ready is refused", source);
        Assert.DoesNotContain("DeterministicRuleEvaluator", source);
        Assert.DoesNotContain("BlissMatch", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
