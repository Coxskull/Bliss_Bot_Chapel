using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class ScaleProofTests
{
    [Fact]
    public void One_hundred_checks_pass_and_are_not_claimed_as_stored_prospects()
    {
        var rung = ScaleProof.Measure(100);

        Assert.Equal(100, rung.Target);
        Assert.Equal(100, rung.Measured);
        Assert.True(rung.Passed);
        Assert.False(rung.Claimed);
        Assert.Equal(string.Empty, rung.Failure);
        Assert.DoesNotMatch(@"\d", ScaleProof.Cost);
        Assert.Contains("not a claim", ScaleProof.Notice);
        Assert.Contains("NOT_SENT", ScaleProof.Notice);
        Assert.Contains("not claimed", ScaleProof.FactoryTarget);
    }

    [Fact]
    public void Ten_thousand_checks_are_measured_and_still_not_claimed()
    {
        var rung = ScaleProof.Measure(10000);

        Assert.Equal(10000, rung.Measured);
        Assert.True(rung.Passed);
        Assert.False(rung.Claimed);
        Assert.True(rung.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void A_rung_outside_the_ladder_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ScaleProof.Measure(50));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Scale_source_does_not_call_a_model_or_send()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "ScaleProof.cs");
        var source = File.ReadAllText(path);
        Assert.Contains("not a claim", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
