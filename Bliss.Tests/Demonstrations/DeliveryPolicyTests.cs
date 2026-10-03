using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class DeliveryPolicyTests
{
    [Fact]
    public void A_missing_road_is_withheld()
    {
        var decision = DeliveryPolicy.Decide(false, false, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.False(decision.Eligible);
        Assert.Equal("WITHHELD", decision.Eligibility);
        Assert.Contains("stored contact road", decision.Notice);
        Assert.Contains("will not invent one", decision.Notice);
    }

    [Fact]
    public void Suppression_comes_before_the_adapter()
    {
        var decision = DeliveryPolicy.Decide(true, true, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.False(decision.Eligible);
        Assert.Contains("Suppression comes before the adapter", decision.Notice);
        Assert.Contains("Nothing is sent", decision.Notice);
    }

    [Fact]
    public void An_unapproved_policy_or_adapter_is_withheld()
    {
        var policy = DeliveryPolicy.Decide(false, true, "live-send", DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.Contains("policy is not approved", policy.Notice);
        var adapter = DeliveryPolicy.Decide(false, true, DeliveryPolicy.PreviewPolicy, "smtp", DeliveryPolicy.PreviewAuthorization);
        Assert.Contains("adapter is not approved", adapter.Notice);
        var words = DeliveryPolicy.Decide(false, true, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, "Send it now");
        Assert.Contains("does not authorize the preview adapter", words.Notice);
        Assert.False(words.Eligible);
    }

    [Fact]
    public void The_preview_adapter_prepares_a_copy_and_does_not_transmit()
    {
        var decision = DeliveryPolicy.Decide(false, true, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.True(decision.Eligible);
        Assert.Equal("ELIGIBLE", decision.Eligibility);
        Assert.Contains("Transmission remains NOT_SENT", decision.Notice);
        var copy = PreviewDeliveryAdapter.Prepare("Mesa Norte");
        Assert.Equal("NOT_SENT", copy.Transmission);
        Assert.Contains("did not transmit", copy.Text);
        Assert.Contains("Mesa Norte", copy.Text);
        Assert.DoesNotContain("@", copy.Text);
    }

    [Fact]
    public void Preview_adapter_source_has_no_transmitter()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Domain",
            "Demonstrations",
            "PreviewDeliveryAdapter.cs");
        var source = File.ReadAllText(path);
        Assert.DoesNotContain("SmtpClient", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("WhatsApp", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MailMessage", source);
    }
}
