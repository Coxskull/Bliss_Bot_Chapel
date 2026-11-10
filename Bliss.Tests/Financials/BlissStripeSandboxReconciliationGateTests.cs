using System.Text.Json;
using Bliss.Api.Financials;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;

namespace Bliss.Tests.Financials;

public sealed class BlissStripeSandboxReconciliationGateTests
{
    private static readonly Guid Merchant = Guid.Parse("55f53552-6b56-4bd9-b1f6-94ac4009db02");
    private static readonly Guid Campaign = Guid.Parse("7c53cb78-289e-4bc0-bd5c-aa44f02bc8f4");
    private static readonly Guid Quote = Guid.Parse("8f6a58f4-d77f-45d7-b801-7609500e726c");
    private const string Intent = "pi_bliss_authorized_test_001";

    private static PreparedPaymentOperation Operation => new(
        Guid.NewGuid(), Quote, new(Merchant, FinancialModule.BlissBot, Campaign),
        Guid.NewGuid(), 14900, "USD", "agm-payment-synthetic");

    private static BlissStripeVerifiedEvent Receipt => new(
        "evt_bliss_test_001", "payment_intent.succeeded",
        Intent, new string('A', 64), "RECONCILIATION_REQUIRED");

    private static string Provider(string status = "succeeded", long amount = 14900,
        long received = 14900, string currency = "usd", bool live = false,
        string? intent = null, Guid? merchant = null, Guid? campaign = null,
        string module = "BlissBot", string? funding = null)
        => JsonSerializer.Serialize(new
        {
            id = intent ?? Intent,
            @object = "payment_intent",
            livemode = live,
            status, amount, amount_received = received, currency,
            metadata = new
            {
                merchant_id = (merchant ?? Merchant).ToString(),
                campaign_id = (campaign ?? Campaign).ToString(),
                module,
                funding_reference = funding ?? Quote.ToString("N")
            }
        });

    private static BlissStripeReconciliationReading Check(
        string provider, BlissStripeVerifiedEvent? receipt = null,
        PreparedPaymentOperation? operation = null, string? funding = null)
        => BlissStripeSandboxReconciliationGate.Evaluate(
            receipt ?? Receipt, operation ?? Operation,
            funding ?? Quote.ToString("N"), Intent, provider);

    [Fact]
    public void Matching_success_remains_manual_review_and_never_fulfills()
    {
        var result = Check(Provider());
        Assert.Equal("EVIDENCE_MATCHES_MANUAL_REVIEW_REQUIRED", result.Disposition);
        Assert.False(result.AuthorizesFulfillment);
    }

    [Theory]
    [InlineData("requires_payment_method", 14900, 14900, "usd", false)]
    [InlineData("processing", 14900, 0, "usd", false)]
    [InlineData("succeeded", 14901, 14901, "usd", false)]
    [InlineData("succeeded", 14900, 0, "usd", false)]
    [InlineData("succeeded", 14900, 14900, "php", false)]
    [InlineData("succeeded", 14900, 14900, "usd", true)]
    public void No_nonmatching_provider_status_or_amount_could_authorize(
        string status, long amount, long received, string currency, bool live)
    {
        var result = Check(Provider(status, amount, received, currency, live));
        Assert.Equal("PROVIDER_PAYMENT_MISMATCH", result.Disposition);
        Assert.False(result.AuthorizesFulfillment);
    }

    [Fact]
    public void Bound_intent_and_merchant_campaign_module_and_funding_must_match()
    {
        string[] bad =
        [
            Provider(intent: "pi_different"),
            Provider(merchant: Guid.NewGuid()),
            Provider(campaign: Guid.NewGuid()),
            Provider(module: "WeddingPlanner"),
            Provider(funding: Guid.NewGuid().ToString("N"))
        ];
        foreach (var provider in bad)
        {
            Assert.False(Check(provider).AuthorizesFulfillment);
            Assert.Contains(Check(provider).Disposition,
                new[] { "PROVIDER_PAYMENT_MISMATCH", "IMMUTABLE_BINDING_MISMATCH" });
        }
    }

    [Fact]
    public void Failed_expired_or_conflicting_receipt_never_reconciles()
    {
        foreach (var eventType in new[] { "payment_intent.payment_failed", "checkout.session.expired" })
        {
            var result = Check(Provider(), Receipt with { EventType = eventType,
                Disposition = "NO_FULFILLMENT" });
            Assert.Equal("EVENT_NOT_AUTHORITATIVE", result.Disposition);
            Assert.False(result.AuthorizesFulfillment);
        }
        Assert.Equal("EVENT_NOT_AUTHORITATIVE",
            Check(Provider(), Receipt with { ObjectId = "pi_unbound" }).Disposition);
    }

    [Fact]
    public void Invalid_quote_authority_or_malformed_provider_fails_closed()
    {
        Assert.Equal("INVALID_QUOTE_SCOPE",
            Check(Provider(), operation: Operation with { AmountMinor = 0 }).Disposition);
        Assert.Equal("MALFORMED_PROVIDER_EVIDENCE", Check("{").Disposition);
        Assert.Equal("MISSING_AUTHORITY",
            BlissStripeSandboxReconciliationGate.Evaluate(Receipt, Operation,
                Quote.ToString("N"), Intent, "").Disposition);
    }
}
