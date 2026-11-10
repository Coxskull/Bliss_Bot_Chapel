using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Infrastructure.Financials;

namespace Bliss.Tests.Financials;

public sealed class StripePlanningSandboxWebhookTests
{
    internal const string Secret = "whsec_synthetic_for_offline_tests_only";
    private const string SessionId = "cs_test_a127dclwqC9jOEq2Fzt0Rg6R05GEwdbi2U73Lp4R2AlIpb4a0dDkeQvdXI";
    private const string PaymentIntentId = "pi_3UZmosH23maIy3rz1QI0U6Lo";
    private const string EventId = "evt_fixture_planning_0001";
    internal static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static byte[] Payload(
        string type = "checkout.session.completed", string paymentStatus = "paid",
        string link = StripePlanningSandboxWebhookVerifier.ExpectedPaymentLinkId,
        long amount = 14900, bool live = false,
        string id = EventId, string sessionId = SessionId, string intentId = PaymentIntentId,
        string currency = "usd", string policy = "DRAFT",
        string taxStatus = "complete", bool connect = false)
    {
        var session = new
        {
            id = sessionId, @object = "checkout.session", livemode = live,
            payment_link = link, payment_intent = intentId, mode = "payment",
            status = "complete", payment_status = paymentStatus,
            amount_total = amount, currency,
            metadata = new { environment = "SANDBOX_ONLY", payment = "TEST_MODE_ONLY",
                policy_status = policy, real_customer_use = "PROHIBITED" },
            automatic_tax = new { enabled = true, status = taxStatus },
            total_details = new { amount_tax = 0L }
        };
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            id, @object = "event", livemode = live,
            type, account = connect ? "acct_connected_forbidden" : null,
            data = new { @object = session }
        });
    }
    // The production verifier rejects the property if present; ordinary
    // platform Stripe events omit 'account'. Remove the synthetic null.
    internal static byte[] Body(
        string type = "checkout.session.completed", string paymentStatus = "paid",
        string link = StripePlanningSandboxWebhookVerifier.ExpectedPaymentLinkId,
        long amount = 14900, bool live = false,
        string id = EventId, string sessionId = SessionId, string intentId = PaymentIntentId,
        string currency = "usd", string policy = "DRAFT",
        string taxStatus = "complete", bool connect = false)
    {
        var serialized = Payload(type, paymentStatus, link, amount, live, id,
            sessionId, intentId, currency, policy, taxStatus, connect);
        if (connect) return serialized;
        using var parsed = JsonDocument.Parse(serialized);
        var root = parsed.RootElement;
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            id = root.GetProperty("id").GetString(),
            @object = "event", livemode = live, type,
            data = new { @object = root.GetProperty("data").GetProperty("object") }
        });
    }
    internal static string Signature(byte[] raw, long? at = null)
    {
        var time = at ?? Now.ToUnixTimeSeconds();
        var buffer = Encoding.UTF8.GetBytes(time + "." + Encoding.UTF8.GetString(raw));
        return "t=" + time + ",v1=" +
            Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), buffer));
    }
    private static VerifiedPlanningSandboxWebhook Verify(byte[] raw) =>
        StripePlanningSandboxWebhookVerifier.Verify(raw, Signature(raw), Secret, Now);

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"livemode\":\"false\"}")]
    [InlineData("{\"livemode\":false,\"object\":123}")]
    public void Signed_wrong_JSON_types_are_invalid_input_not_retryable_server_errors(string json)
    {
        Assert.Throws<ArgumentException>(() => Verify(Encoding.UTF8.GetBytes(json)));
    }

    [Theory]
    [InlineData("14901")]
    [InlineData("9223372036854775808")]
    [InlineData("0.5")]
    [InlineData("\"0\"")]
    [InlineData("null")]
    public void Inclusive_tax_must_be_an_integer_bounded_by_total(string amount)
    {
        var body = Encoding.UTF8.GetString(Body()).Replace("\"amount_tax\":0", "\"amount_tax\":" + amount);
        Assert.Throws<ArgumentException>(() => Verify(Encoding.UTF8.GetBytes(body)));
    }

    [Fact]
    public void Signed_completed_test_checkout_is_sandbox_receipt_only()
    {
        var v = Verify(Body());
        Assert.Equal(EventId, v.EventId);
        Assert.Equal(SessionId, v.SessionId);
        Assert.Equal(PaymentIntentId, v.PaymentIntentId);
        Assert.Equal(14900, v.AmountMinor);
        Assert.Equal("USD", v.Currency);
        Assert.Equal("PAID_TEST_REVIEW_ONLY", v.ReceiptState);
        Assert.Equal(64, v.FingerprintSha256.Length);
        var decision = PlanningSandboxReceiptReview.Inspect(v, []);
        Assert.Equal("RECORD_SANDBOX_TEST_EVIDENCE_ONLY", decision.Decision);
        Assert.True(decision.RecordPending);
        Assert.False(decision.FulfillmentAuthorized);
        Assert.False(decision.RealRevenueRecognized);
    }

    [Fact]
    public void Pending_and_declined_async_states_never_authorize_fulfillment()
    {
        Assert.Equal("PENDING_TEST_NO_FULFILLMENT",
            Verify(Body(paymentStatus: "unpaid")).ReceiptState);
        Assert.Equal("PAID_TEST_REVIEW_ONLY",
            Verify(Body(type: "checkout.session.async_payment_succeeded")).ReceiptState);
        Assert.Equal("FAILED_TEST_NO_FULFILLMENT",
            Verify(Body(type: "checkout.session.async_payment_failed",
                paymentStatus: "unpaid")).ReceiptState);
        Assert.Throws<ArgumentException>(() => Verify(Body(
            type: "checkout.session.async_payment_succeeded", paymentStatus: "unpaid")));
        Assert.Throws<ArgumentException>(() => Verify(Body(
            type: "checkout.session.async_payment_failed", paymentStatus: "paid")));
    }

    [Fact]
    public void Forged_or_stale_signatures_are_denied()
    {
        var raw = Body();
        Assert.Throws<ArgumentException>(() => StripePlanningSandboxWebhookVerifier.Verify(
            raw, "t=1800000000,v1=1234", Secret, Now));
        Assert.Throws<ArgumentException>(() => StripePlanningSandboxWebhookVerifier.Verify(
            Body(amount: 14901), Signature(raw), Secret, Now));
        Assert.Throws<ArgumentException>(() => StripePlanningSandboxWebhookVerifier.Verify(
            raw, Signature(raw, Now.ToUnixTimeSeconds() - 301), Secret, Now));
        Assert.Throws<ArgumentException>(() => StripePlanningSandboxWebhookVerifier.Verify(
            raw, Signature(raw, Now.ToUnixTimeSeconds() + 301), Secret, Now));
        Assert.Throws<ArgumentException>(() => StripePlanningSandboxWebhookVerifier.Verify(
            raw, Signature(raw), "invalid", Now));
    }

    [Fact]
    public void Incorrect_product_amount_session_or_metadata_is_denied()
    {
        var bad = new byte[][]
        {
            Body(link: "plink_unrelated"),
            Body(amount: 14901),
            Body(live: true),
            Body(sessionId: "cs_live_other"),
            Body(intentId: "not_a_payment_intent"),
            Body(currency: "php"),
            Body(policy: "APPROVED"),
            Body(taxStatus: "requires_location_inputs"),
            Body(type: "charge.succeeded"),
            Body(connect: true)
        };
        foreach (var raw in bad)
            Assert.ThrowsAny<Exception>(() => Verify(raw));
    }

    [Fact]
    public void Exact_replay_is_idempotent_but_conflicting_or_duplicate_paid_event_is_not_new_revenue()
    {
        var v = Verify(Body());
        var prior = new PlanningSandboxReceiptEvidence(v.EventId,v.SessionId,
            v.PaymentIntentId,v.ReceiptState,v.FingerprintSha256);
        var duplicate=PlanningSandboxReceiptReview.Inspect(v,[prior]);
        Assert.Equal("DUPLICATE_ALREADY_RECORDED", duplicate.Decision);
        Assert.False(duplicate.RecordPending);
        var conflicting=PlanningSandboxReceiptReview.Inspect(v,
            [prior with { FingerprintSha256 = new string('0',64) }]);
        Assert.Equal("CONFLICT_HOLD",conflicting.Decision);
        var second=Verify(Body(id:"evt_fixture_planning_0002"));
        var samePayment=PlanningSandboxReceiptReview.Inspect(second,[prior]);
        Assert.Equal("SAME_PAYMENT_NO_DOUBLE_RECEIPT", samePayment.Decision);
        Assert.False(samePayment.RecordPending);
        Assert.False(samePayment.RealRevenueRecognized);
        Assert.Equal("CONFLICT_HOLD", PlanningSandboxReceiptReview.Inspect(v,
            [prior with { EventId = "evt_other_fixture", PaymentIntentId = "pi_other_different" }]).Decision);
    }

    [Fact]
    public void Out_of_order_pending_never_downgrades_paid_test_receipt()
    {
        var paid = Verify(Body());
        var prior = new PlanningSandboxReceiptEvidence(paid.EventId,paid.SessionId,
            paid.PaymentIntentId,paid.ReceiptState,paid.FingerprintSha256);
        var laterPending=Verify(Body(id:"evt_fixture_pending_0002",paymentStatus:"unpaid"));
        Assert.Equal("STALE_PENDING_IGNORED",
            PlanningSandboxReceiptReview.Inspect(laterPending,[prior]).Decision);
        var pendingRecord=prior with { EventId="evt_older_pending",
            ReceiptState="PENDING_TEST_NO_FULFILLMENT" };
        Assert.Equal("STATE_TRANSITION_NEEDS_REVIEW",
            PlanningSandboxReceiptReview.Inspect(paid,[pendingRecord]).Decision);
        var failed=Verify(Body(id:"evt_fixture_fail_0003",
            type:"checkout.session.async_payment_failed",paymentStatus:"unpaid"));
        Assert.Equal("CONFLICT_HOLD",
            PlanningSandboxReceiptReview.Inspect(failed,[prior]).Decision);
    }
}
