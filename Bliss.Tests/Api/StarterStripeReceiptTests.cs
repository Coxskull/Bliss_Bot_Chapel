using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Operations;

namespace Bliss.Tests.Api;

public sealed class StarterStripeReceiptTests
{
    const string Secret = "whsec_fixture_not_a_real_secret";
    static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1795582000);
    static byte[] Payload(bool live = true, string payment = "paid", long amount = 14900,
        string link = StarterStripeReceipt.PaymentLinkId, string reference = "ALPHA-FIXTURE-001") =>
        JsonSerializer.SerializeToUtf8Bytes(new { id = "evt_fixture1", @object = "event", livemode = live,
            type = "checkout.session.completed", data = new { @object = new { @object = "checkout.session",
                id = "cs_live_fixture1", livemode = live, mode = "payment", status = "complete", payment_status = payment,
                payment_link = link, payment_intent = "pi_fixture1", currency = "usd", amount_total = amount, client_reference_id = reference } } });
    static string Sign(byte[] body, DateTimeOffset time)
    {
        var timestamp = time.ToUnixTimeSeconds();
        var raw = Encoding.UTF8.GetBytes(timestamp + ".").Concat(body).ToArray();
        return "t=" + timestamp + ",v1=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), raw));
    }
    [Fact]
    public void Raw_body_signature_and_timestamp_are_required()
    {
        var bytes = Payload();
        Assert.True(StarterStripeReceipt.Verify(bytes, Sign(bytes, Now), Secret, Now));
        Assert.False(StarterStripeReceipt.Verify(Payload(amount: 14899), Sign(bytes, Now), Secret, Now));
        Assert.False(StarterStripeReceipt.Verify(bytes, Sign(bytes, Now.AddSeconds(-301)), Secret, Now));
        Assert.False(StarterStripeReceipt.Verify(bytes, Sign(bytes, Now.AddSeconds(301)), Secret, Now));
        Assert.False(StarterStripeReceipt.Verify(bytes, Sign(bytes, Now), "whsec_wrong_fixture_key", Now));
    }
    [Fact]
    public void Only_matching_live_paid_starter_notifications_are_parsed()
    {
        var receipt = Assert.IsType<StarterStripeReceipt>(StarterStripeReceipt.Parse(Payload()));
        Assert.Equal("ALPHA-FIXTURE-001", receipt.QuoteReference);
        Assert.Equal(14900L, receipt.AmountCents);
        Assert.Null(StarterStripeReceipt.Parse(Payload(live: false)));
        Assert.Null(StarterStripeReceipt.Parse(Payload(payment: "unpaid")));
        Assert.Null(StarterStripeReceipt.Parse(Payload(amount: 14899)));
        Assert.Null(StarterStripeReceipt.Parse(Payload(link: "plink_wrong")));
        Assert.Null(StarterStripeReceipt.Parse(Payload(reference: "email@example.invalid"))!.QuoteReference);
    }
}
