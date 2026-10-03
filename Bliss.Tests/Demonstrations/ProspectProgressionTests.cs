using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class ProspectProgressionTests
{
    [Fact]
    public void Red_stops_the_prohibited_action_and_keeps_the_record()
    {
        var decision = ProspectProgression.Decide(
            "Puerto Azul",
            "PRESERVED",
            true,
            "The business asked Alpha to stop",
            true,
            true);

        Assert.Equal(ProspectProgression.Red, decision.Signal);
        Assert.Contains("prospect record is preserved", decision.Notice);
        Assert.Contains("The business asked Alpha to stop", decision.Notice);
        Assert.Contains("Green does not send", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
        Assert.Contains("keep the suppression", decision.NextAction);
        Assert.False(decision.GreenMeansSend);
        Assert.False(decision.Erased);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Fact]
    public void Yellow_keeps_a_preserved_business()
    {
        var decision = ProspectProgression.Decide("Calle Sur", "PRESERVED", false, null, false, false);

        Assert.Equal(ProspectProgression.Yellow, decision.Signal);
        Assert.Contains("Yellow keeps the record", decision.Notice);
        Assert.Contains("demonstration stays withheld", decision.Notice);
        Assert.Contains("recheck the road", decision.NextAction);
        Assert.Contains("Green does not send", decision.Notice);
        Assert.False(decision.Erased);
        Assert.DoesNotMatch(@"\d", decision.Notice);
    }

    [Fact]
    public void Green_moves_to_the_next_authorized_action_and_does_not_send()
    {
        var finding = ProspectProgression.Decide("Mesa Norte", "OPPORTUNITY_SCORED", false, null, false, false);
        var policy = ProspectProgression.Decide("Mesa Norte", "OPPORTUNITY_SCORED", false, null, true, false);
        var queue = ProspectProgression.Decide("Mesa Norte", "DEMONSTRATION_PREPARED", false, null, true, true);

        Assert.Equal(ProspectProgression.Green, finding.Signal);
        Assert.Contains("road finding", finding.Notice);
        Assert.Contains("find a public road", finding.NextAction);
        Assert.Contains("policy check", policy.Notice);
        Assert.Contains("not permission to send", policy.Notice);
        Assert.Contains("approved queue", queue.Notice);
        Assert.Contains("prepares the preview", queue.Notice);
        Assert.All(new[] { finding, policy, queue }, item =>
        {
            Assert.False(item.GreenMeansSend);
            Assert.False(item.Erased);
            Assert.Contains("Green does not send", item.Notice);
            Assert.Equal("NOT_SENT", item.Delivery);
        });
    }

    [Fact]
    public void Progression_source_does_not_send()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "ProspectProgression.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("Green does not send", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
