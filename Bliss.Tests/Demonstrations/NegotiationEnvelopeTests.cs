using System.Text.RegularExpressions;
using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class NegotiationEnvelopeTests
{
    private static readonly NegotiationLine Line = new(200m, 300m, "PHP");

    [Fact]
    public void A_price_question_is_not_a_negotiation()
    {
        Assert.False(NegotiationEnvelope.IsNegotiation("How much does this cost?"));
        var decision = NegotiationEnvelope.Decide("How much does this cost?", Line);
        Assert.Equal(NegotiationKind.NotNegotiation, decision.Kind);
    }

    [Fact]
    public void No_approved_quote_states_no_number()
    {
        var decision = NegotiationEnvelope.Decide("Can you take 100?", null);
        Assert.Equal(NegotiationKind.NoApprovedQuote, decision.Kind);
        Assert.Contains("will not negotiate without an approved Economics quote", decision.Reply);
        Assert.Contains("cannot invent a price", decision.Reply);
        Assert.Contains("NOT_SENT", decision.Reply);
        Assert.DoesNotMatch(new Regex(@"\d"), decision.Reply);
        Assert.DoesNotContain("100", decision.Reply);
        Assert.DoesNotContain("$", decision.Reply);
    }

    [Fact]
    public void Below_the_floor_names_the_floor_and_does_not_write_the_proposal()
    {
        var decision = NegotiationEnvelope.Decide("Can you take 150?", Line);
        Assert.Equal(NegotiationKind.BelowFloor, decision.Kind);
        Assert.True(decision.HumanEscalation);
        Assert.Contains("below the creator floor", decision.Reply);
        Assert.Contains("will not break", decision.Reply);
        Assert.Contains("200 PHP", decision.Reply);
        Assert.Contains("cannot invent a price", decision.Reply);
        Assert.Contains("not a win", decision.Reply);
        Assert.DoesNotContain("150", decision.Reply);
    }

    [Fact]
    public void Above_the_envelope_does_not_raise_the_price()
    {
        var decision = NegotiationEnvelope.Decide("Can you take 350?", Line);
        Assert.Equal(NegotiationKind.AboveEnvelope, decision.Kind);
        Assert.True(decision.HumanEscalation);
        Assert.Contains("outside the Economics envelope", decision.Reply);
        Assert.Contains("will not raise the price", decision.Reply);
        Assert.Contains("300 PHP", decision.Reply);
        Assert.Contains("not a win", decision.Reply);
        Assert.DoesNotContain("350", decision.Reply);
    }

    [Fact]
    public void An_in_range_proposal_waits_for_the_ledger_total()
    {
        var decision = NegotiationEnvelope.Decide("Can you take 220?", Line);
        Assert.Equal(NegotiationKind.Inside, decision.Kind);
        Assert.Equal(220m, decision.Proposal);
        Assert.Equal(NegotiationKind.Inside, NegotiationEnvelope.Decide("Can you take 200?", Line).Kind);
        Assert.Equal(NegotiationKind.Inside, NegotiationEnvelope.Decide("We can pay 300.", Line).Kind);
        var spoken = NegotiationEnvelope.InsideReply("220", "PHP");
        Assert.Contains("inside the Economics envelope", spoken);
        Assert.Contains("recorded a draft of 220 PHP", spoken);
        Assert.Contains("not accepted", spoken);
        Assert.Contains("cannot invent a price", spoken);
        Assert.Contains("not a win", spoken);
        Assert.DoesNotContain("Economics accepted", spoken);
    }

    [Fact]
    public void A_negotiation_without_one_amount_explains_the_envelope()
    {
        var explain = NegotiationEnvelope.Decide("Can you negotiate?", Line);
        Assert.Equal(NegotiationKind.Explain, explain.Kind);
        Assert.Contains("creator floor is 200 PHP", explain.Reply);
        Assert.Contains("envelope high is 300 PHP", explain.Reply);
        Assert.Contains("not accepted", explain.Reply);

        var many = NegotiationEnvelope.Decide("Can you take 180 or 240?", Line);
        Assert.Equal(NegotiationKind.Unreadable, many.Kind);
        Assert.Contains("cannot read more than one amount", many.Reply);
        Assert.DoesNotContain("180", many.Reply);
        Assert.DoesNotContain("240", many.Reply);
    }

    [Fact]
    public void A_human_request_stays_a_human_request()
    {
        Assert.False(NegotiationEnvelope.IsNegotiation("I want a human to negotiate"));
    }
}
