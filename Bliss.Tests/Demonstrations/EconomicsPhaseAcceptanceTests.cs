using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class EconomicsPhaseAcceptanceTests
{
    [Fact]
    public void An_empty_history_is_accepted_and_stays_unrecorded()
    {
        var reading = EconomicsPhaseAcceptance.Accept(9, false, false, 0, 0, null, null, []);

        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Equal(EconomicsPhaseAcceptance.PhaseKey, reading.PhaseKey);
        Assert.Equal(9, reading.Phase);
        Assert.True(reading.Accepted);
        Assert.False(reading.Duplicate);
        Assert.Equal(EconomicsPhaseAcceptance.Unrecorded, reading.HistoryLine);
        Assert.False(reading.RepricingAuthorized);
        Assert.False(reading.SettlementAuthorized);
        Assert.False(reading.RecommendationRewritten);
        Assert.Contains("accepted by the owner", reading.Notice);
    }

    [Fact]
    public void A_stored_actual_is_cited_and_a_repeat_does_not_reprice()
    {
        var cited = EconomicsPhaseAcceptance.Accept(9, false, false, 1, 1, 215m, "PHP", []);
        Assert.Contains("Contracted 215 PHP", cited.HistoryLine);
        Assert.Contains("was not changed", cited.HistoryLine);
        Assert.False(cited.RecommendationRewritten);

        var again = EconomicsPhaseAcceptance.Accept(
            9,
            false,
            false,
            1,
            1,
            215m,
            "PHP",
            [new AcceptanceRecord(EconomicsPhaseAcceptance.PhaseKey)]);
        Assert.True(again.Duplicate);
        Assert.Equal(EconomicsPhaseAcceptance.DuplicateNotice, again.Notice);
        Assert.False(again.RepricingAuthorized);
    }

    [Fact]
    public void Repricing_settlement_and_an_invented_amount_are_refused()
    {
        var reprice = Assert.Throws<InvalidOperationException>(() =>
            EconomicsPhaseAcceptance.Accept(9, true, false, 0, 0, null, null, []));
        Assert.Contains("not changed", reprice.Message);
        var settle = Assert.Throws<InvalidOperationException>(() =>
            EconomicsPhaseAcceptance.Accept(9, false, true, 0, 0, null, null, []));
        Assert.Contains("not created", settle.Message);
        var invented = Assert.Throws<InvalidOperationException>(() =>
            EconomicsPhaseAcceptance.Accept(9, false, false, 0, 0, 12m, "PHP", []));
        Assert.Contains("None was invented", invented.Message);
        var other = Assert.Throws<InvalidOperationException>(() =>
            EconomicsPhaseAcceptance.Accept(10, false, false, 0, 0, null, null, []));
        Assert.Contains("Phase 9", other.Message);
    }

    [Fact]
    public void Acceptance_source_does_not_reprice_or_settle()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Demonstrations", "EconomicsPhaseAcceptance.cs"));
        Assert.Contains("not changed", source);
        Assert.Contains("not created", source);
        Assert.Contains("None was invented", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("$", source);
    }
}
