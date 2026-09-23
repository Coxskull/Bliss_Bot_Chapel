namespace Bliss.Tests.Architecture;

public sealed class EconomicsPhase9BoundaryTests
{
    [Fact]
    public void Historical_service_is_append_only_and_has_no_pricing_or_settlement_authority()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Bliss.Infrastructure",
            "Persistence",
            "HistoricalEconomicsService.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("RateRecommendationService", source);
        Assert.DoesNotContain("QuoteService", source);
        Assert.DoesNotContain("CampaignPlacementService", source);
        Assert.DoesNotContain("CompensationIllustrationService", source);
        Assert.DoesNotContain("OpenAI", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("n8n", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SettlementService", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Update(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Remove(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase9_contract_forbids_automatic_feedback_and_payment()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "docs", "economics", "PHASE-9-ENGINEERING-CONTRACT.md");
        var contract = File.ReadAllText(path);

        Assert.Contains("does not automatically", contract);
        Assert.Contains("No PUT, PATCH, or DELETE", contract);
        Assert.Contains("payable", contract, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("append", contract, StringComparison.OrdinalIgnoreCase);
    }
}
