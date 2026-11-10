using System.Security.Cryptography;
using System.Text.Json;

namespace Bliss.Infrastructure.Financials;

// Existing ALPHA-346 $149 planning-only SANDBOX Payment Link, not a live offer.
// No customer activation, contract acceptance, capture journal or fulfillment.
public sealed record VerifiedPlanningSandboxWebhook(
    string EventId, string SessionId, string PaymentIntentId,
    string ReceiptState, long AmountMinor, string Currency, string FingerprintSha256);

public static class StripePlanningSandboxWebhookVerifier
{
    public const string ExpectedPaymentLinkId = "plink_1UZkkuH23maIy3rzRSbuvCtT";
    public const long ExpectedAmountMinor = 14900;
    public const string ExpectedCurrency = "USD";

    public static VerifiedPlanningSandboxWebhook Verify(
        byte[] rawBody, string signature, string signingSecret, DateTimeOffset now)
    {
        try
        {
            return VerifyPayload(rawBody, signature, signingSecret, now);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or FormatException or OverflowException or KeyNotFoundException or JsonException)
        {
            // JSON shape/type failures are permanent bad input, not DB failures.
            // Keep this conversion inside verification so persistence exceptions
            // still return 503 and are retried by Stripe. Never echo the payload.
            throw new ArgumentException("Invalid sandbox checkout payload.");
        }
    }

    private static VerifiedPlanningSandboxWebhook VerifyPayload(
        byte[] rawBody, string signature, string signingSecret, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(signingSecret) ||
            !signingSecret.StartsWith("whsec_", StringComparison.Ordinal) ||
            signingSecret.Length < 16 || signingSecret.Any(char.IsWhiteSpace))
            throw new ArgumentException("Sandbox webhook signing secret is required.");
        using var document = StripeSandboxVerifier.VerifyEnvelope(
            rawBody, signature, signingSecret, now);
        var root = document.RootElement;
        if (root.GetProperty("object").GetString() != "event")
            throw new ArgumentException("Unexpected Stripe event object.");
        var eventId = root.GetProperty("id").GetString();
        var type = root.GetProperty("type").GetString();
        if (!Identifier(eventId, "evt_") ||
            type is not ("checkout.session.completed"
                or "checkout.session.async_payment_succeeded"
                or "checkout.session.async_payment_failed"))
            throw new ArgumentException("Unsupported webhook event identity or type.");

        var session = root.GetProperty("data").GetProperty("object");
        var sessionId = session.GetProperty("id").GetString();
        var intentId = session.GetProperty("payment_intent").GetString();
        if (session.GetProperty("object").GetString() != "checkout.session" ||
            session.GetProperty("livemode").GetBoolean() ||
            !Identifier(sessionId, "cs_test_") || !Identifier(intentId, "pi_") ||
            session.GetProperty("payment_link").GetString() != ExpectedPaymentLinkId ||
            session.GetProperty("mode").GetString() != "payment" ||
            session.GetProperty("status").GetString() != "complete" ||
            session.GetProperty("amount_total").GetInt64() != ExpectedAmountMinor ||
            session.GetProperty("currency").GetString() != "usd")
            throw new ArgumentException("Wrong sandbox checkout, amount or payment identity.");

        var metadata = session.GetProperty("metadata");
        if (metadata.GetProperty("environment").GetString() != "SANDBOX_ONLY" ||
            metadata.GetProperty("payment").GetString() != "TEST_MODE_ONLY" ||
            metadata.GetProperty("policy_status").GetString() != "DRAFT" ||
            metadata.GetProperty("real_customer_use").GetString() != "PROHIBITED")
            throw new ArgumentException("Checkout was not marked as test-only.");

        var tax = session.GetProperty("automatic_tax");
        var totals = session.GetProperty("total_details");
        if (!tax.GetProperty("enabled").GetBoolean() ||
            tax.GetProperty("status").GetString() != "complete" ||
            totals.GetProperty("amount_tax").GetInt64() is < 0 or > ExpectedAmountMinor)
            throw new ArgumentException("Incomplete sandbox checkout/tax evidence.");

        var paid = session.GetProperty("payment_status").GetString();
        var receiptState = (type, paid) switch
        {
            ("checkout.session.completed", "paid") => "PAID_TEST_REVIEW_ONLY",
            ("checkout.session.completed", "unpaid") => "PENDING_TEST_NO_FULFILLMENT",
            ("checkout.session.async_payment_succeeded", "paid") => "PAID_TEST_REVIEW_ONLY",
            ("checkout.session.async_payment_failed", "unpaid") => "FAILED_TEST_NO_FULFILLMENT",
            _ => throw new ArgumentException("Event type and payment status conflict.")
        };
        return new VerifiedPlanningSandboxWebhook(
            eventId!, sessionId!, intentId!, receiptState, ExpectedAmountMinor,
            ExpectedCurrency, Convert.ToHexString(SHA256.HashData(rawBody)).ToLowerInvariant());
    }

    private static bool Identifier(string? id, string prefix) =>
        id is not null && id.StartsWith(prefix, StringComparison.Ordinal) &&
        id.Length > prefix.Length + 6 && id.Length <= 150 &&
        id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');
}

// Pure, offline review decision. A real HTTP adapter MUST atomically persist
// event ID, content fingerprint and intent/session uniqueness before 2xx ACK.
// In-memory lists, event payloads and browser redirects are NOT durable receipts.
public sealed record PlanningSandboxReceiptEvidence(
    string EventId, string SessionId, string PaymentIntentId,
    string ReceiptState, string FingerprintSha256);
public sealed record PlanningSandboxReceiptDecision(
    string Decision, bool RecordPending,
    bool FulfillmentAuthorized = false, bool RealRevenueRecognized = false);

public static class PlanningSandboxReceiptReview
{
    public static PlanningSandboxReceiptDecision Inspect(
        VerifiedPlanningSandboxWebhook current,
        IReadOnlyCollection<PlanningSandboxReceiptEvidence> persistedReceipts)
    {
        if (current is null || persistedReceipts is null)
            throw new ArgumentException("Verified event and authoritative prior receipts required.");
        foreach (var prior in persistedReceipts)
        {
            if (prior.EventId == current.EventId)
                return new(prior.FingerprintSha256 == current.FingerprintSha256 &&
                    prior.SessionId == current.SessionId &&
                    prior.PaymentIntentId == current.PaymentIntentId &&
                    prior.ReceiptState == current.ReceiptState
                    ? "DUPLICATE_ALREADY_RECORDED" : "CONFLICT_HOLD", false);
            if (prior.SessionId != current.SessionId &&
                prior.PaymentIntentId != current.PaymentIntentId) continue;
            if (prior.SessionId != current.SessionId ||
                prior.PaymentIntentId != current.PaymentIntentId)
                return new("CONFLICT_HOLD", false);
            if (prior.ReceiptState == current.ReceiptState)
                return new("SAME_PAYMENT_NO_DOUBLE_RECEIPT", false);
            if (prior.ReceiptState == "PAID_TEST_REVIEW_ONLY" &&
                current.ReceiptState == "PENDING_TEST_NO_FULFILLMENT")
                return new("STALE_PENDING_IGNORED", false);
            if (prior.ReceiptState == "PENDING_TEST_NO_FULFILLMENT")
                return new("STATE_TRANSITION_NEEDS_REVIEW", false);
            return new("CONFLICT_HOLD", false);
        }
        return new("RECORD_SANDBOX_TEST_EVIDENCE_ONLY", true);
    }
}
