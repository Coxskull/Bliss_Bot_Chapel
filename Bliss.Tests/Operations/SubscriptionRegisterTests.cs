using Bliss.Domain.Operations;

namespace Bliss.Tests.Operations;

public sealed class SubscriptionRegisterTests
{
    [Fact]
    public void Stored_rows_keep_a_missing_cost_unrecorded()
    {
        var reading = SubscriptionRegister.Read(Stored());
        Assert.Equal(10, reading.Services.Count);
        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.Contains("None was invented", reading.Notice);
        Assert.Contains("Nothing is purchased", reading.Notice);
        var postgres = reading.Services.Single(item => item.Key == "POSTGRESQL");
        Assert.Contains("Classification is not recorded", postgres.ClassificationLine);
        Assert.Contains("Cost is not recorded", postgres.CostLine);
        Assert.DoesNotMatch(@"\d", postgres.CostLine);
        var bliss = reading.Services.Single(item => item.Key == "BLISS_CHAPEL");
        Assert.Equal("FREE_SELF_HOSTED", bliss.ClassificationLine);
        Assert.Contains("Cost is not recorded", bliss.CostLine);
    }

    [Fact]
    public void A_missing_service_is_not_invented()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SubscriptionRegister.Read(Stored().Take(9)));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void An_operator_cost_does_not_become_a_purchase()
    {
        var decision = SubscriptionRegister.RecordCost("postgresql", 18m, "usd", "Operator invoice 2026-10");
        Assert.Equal("POSTGRESQL", decision.ServiceKey);
        Assert.Equal(18m, decision.Amount);
        Assert.Equal("USD", decision.Currency);
        Assert.Contains("18 USD", decision.Notice);
        Assert.Contains("not an Economics price", decision.Notice);
        Assert.Contains("Nothing was purchased", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
    }

    [Fact]
    public void A_partial_cost_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SubscriptionRegister.RecordCost("POSTGRESQL", 18m, null, "Operator invoice"));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void An_incomplete_paid_review_purchases_nothing()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            SubscriptionRegister.Review(new GapProposal(
                "Enrichment suite", null, null, "NEW_PAID_SUBSCRIPTION", null,
                null, null, null, null, null, null, null, null, null, null)));
        Assert.Contains("complete capability-gap review", error.Message);
        Assert.Contains("Nothing was purchased", error.Message);
    }

    [Fact]
    public void A_complete_review_stays_not_proposed()
    {
        var decision = SubscriptionRegister.Review(Complete("Contact graph"));
        Assert.Equal(SubscriptionRegister.NotProposed, decision.Status);
        Assert.Equal(SubscriptionRegister.NewPaid, decision.Classification);
        Assert.Contains("not a purchase", decision.Notice);
        Assert.Contains("Nothing was bought", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
    }

    [Fact]
    public void A_rejected_duplicate_is_not_bought()
    {
        var decision = SubscriptionRegister.Review(new GapProposal(
            "Second CRM", null, null, "duplicative_rejected", null,
            null, null, null, null, null, null, null, null, null, null));
        Assert.Equal(SubscriptionRegister.Rejected, decision.Status);
        Assert.Null(decision.MonthlyAmount);
        Assert.Contains("Do not buy it", decision.Notice);
        Assert.Contains("Nothing was purchased", decision.Notice);
    }

    [Fact]
    public void Register_source_does_not_send_or_price_with_a_symbol()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Operations", "SubscriptionRegister.cs"));
        Assert.Contains("Nothing is purchased", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }

    internal static GapProposal Complete(string service) => new(
        service,
        "Example Provider",
        "A function Alpha does not have",
        "NEW_PAID_SUBSCRIPTION",
        "Subscription ledger contract",
        20m,
        "USD",
        "No usage charge is known",
        "PostgreSQL and the existing register",
        "Keep the row unrecorded until a bill exists",
        "The register cannot store this function today",
        20m,
        "USD",
        "When a later contract needs it",
        "OPTIONAL");

    private static IReadOnlyList<StoredService> Stored() =>
        SubscriptionRegister.Known.Select(item =>
            new StoredService(item.Key, item.Classification, null, null, item.Status)).ToList();
}
