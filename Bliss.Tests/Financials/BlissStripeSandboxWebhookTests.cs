using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Financials;

namespace Bliss.Tests.Financials;

public sealed class BlissStripeSandboxWebhookTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
    private const string Secret = "whsec_bliss_test_not_real";

    private static string Payload(string type, bool live = false, string status = "succeeded",
        string paymentStatus = "paid", bool connect = false, long received = 14900)
    {
        var payload = new Dictionary<string, object?>
        {
            ["id"] = "evt_bliss_sandbox_123",
            ["type"] = type,
            ["livemode"] = live,
            ["data"] = new
            {
                @object = new
                {
                    id = type.StartsWith("checkout.", StringComparison.Ordinal)
                        ? "cs_test_bliss_123" : "pi_bliss_test_123",
                    @object = type.StartsWith("checkout.", StringComparison.Ordinal)
                        ? "checkout.session" : "payment_intent",
                    payment_status = paymentStatus,
                    status,
                    amount = 14900,
                    amount_received = received,
                    currency = "usd"
                }
            }
        };
        if (connect) payload["account"] = "acct_connected_test";
        return JsonSerializer.Serialize(payload);
    }

    private static string Sign(string payload, long? epoch = null)
    {
        var timestamp = epoch ?? Now.ToUnixTimeSeconds();
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret),
            Encoding.UTF8.GetBytes(timestamp + "." + payload));
        return "t=" + timestamp + ",v1=" + Convert.ToHexString(digest);
    }

    private static BlissStripeVerifiedEvent Validate(string payload)
        => BlissStripeSandboxWebhookVerifier.Verify(payload, Sign(payload), Secret, Now);

    [Theory]
    [InlineData("payment_intent.succeeded", "RECONCILIATION_REQUIRED")]
    [InlineData("checkout.session.completed", "RECONCILIATION_REQUIRED")]
    [InlineData("checkout.session.async_payment_succeeded", "RECONCILIATION_REQUIRED")]
    [InlineData("checkout.session.expired", "NO_FULFILLMENT")]
    [InlineData("checkout.session.async_payment_failed", "NO_FULFILLMENT")]
    [InlineData("payment_intent.payment_failed", "NO_FULFILLMENT")]
    [InlineData("payment_intent.canceled", "NO_FULFILLMENT")]
    [InlineData("charge.failed", "NO_FULFILLMENT")]
    [InlineData("some.future.event", "REVIEW_ONLY")]
    public void Signed_events_are_journaled_but_never_automatically_fulfill(
        string type, string expected)
    {
        var item = Validate(Payload(type));
        Assert.Equal(expected, item.Disposition);
        Assert.False(item.AuthorizesFulfillment);
        Assert.Equal("evt_bliss_sandbox_123", item.EventId);
        Assert.Equal(64, item.PayloadSha256.Length);
    }

    [Fact]
    public void Completed_checkout_without_paid_status_is_not_fulfillable()
    {
        var item = Validate(Payload("checkout.session.completed", paymentStatus: "unpaid"));
        Assert.Equal("REVIEW_ONLY", item.Disposition);
        Assert.False(item.AuthorizesFulfillment);
    }

    [Fact]
    public void No_amount_received_is_not_financially_authoritative()
    {
        var item = Validate(Payload("payment_intent.succeeded", received: 0));
        Assert.Equal("REVIEW_ONLY", item.Disposition);
    }

    [Fact]
    public void Changed_body_and_wrong_secret_are_rejected()
    {
        var raw = Payload("payment_intent.succeeded");
        Assert.Throws<ArgumentException>(() => BlissStripeSandboxWebhookVerifier.Verify(
            raw.Replace("14900", "99999"), Sign(raw), Secret, Now));
        Assert.Throws<ArgumentException>(() => BlissStripeSandboxWebhookVerifier.Verify(
            raw, Sign(raw), "whsec_wrong_secret", Now));
    }

    [Fact]
    public void Live_and_connect_events_are_rejected_even_with_valid_signature()
    {
        Assert.Throws<ArgumentException>(() => Validate(Payload("checkout.session.completed", live: true)));
        Assert.Throws<ArgumentException>(() => Validate(Payload("checkout.session.completed", connect: true)));
    }

    [Fact]
    public void Stale_future_and_malformed_signatures_are_rejected()
    {
        var raw = Payload("checkout.session.expired");
        foreach (var seconds in new[] { Now.ToUnixTimeSeconds() - 301, Now.ToUnixTimeSeconds() + 301 })
            Assert.Throws<ArgumentException>(() => BlissStripeSandboxWebhookVerifier.Verify(
                raw, Sign(raw, seconds), Secret, Now));
        Assert.Throws<ArgumentException>(() => BlissStripeSandboxWebhookVerifier.Verify(
            raw, Sign(raw) + ",t=1800000000", Secret, Now));
        Assert.Throws<ArgumentException>(() => BlissStripeSandboxWebhookVerifier.Verify(
            raw, Sign(raw) + ",v0=bad", Secret, Now));
    }

    [Fact]
    public void Fingerprint_is_stable_for_retry_and_changes_with_body()
    {
        var one = Validate(Payload("checkout.session.expired"));
        var two = Validate(Payload("checkout.session.expired"));
        var changed = Validate(Payload("checkout.session.completed"));
        Assert.Equal(one.PayloadSha256, two.PayloadSha256);
        Assert.NotEqual(one.PayloadSha256, changed.PayloadSha256);
    }
}
