using Bliss.Domain.Operations;

namespace Bliss.Tests.Operations;

public sealed class FactoryBudgetTests
{
    [Fact]
    public void Four_scopes_start_without_an_invented_ceiling()
    {
        var reading = FactoryBudget.Read(FactoryBudget.Scopes.Select(scope => new StoredBudget(scope, null, null, null)));
        Assert.Equal(4, reading.Scopes.Count);
        Assert.False(reading.GreenMeansSend);
        Assert.Equal("NOT_SENT", reading.Delivery);
        Assert.All(reading.Scopes, scope =>
        {
            Assert.Contains("no ceiling recorded", scope.Notice);
            Assert.Contains("None was invented", scope.Notice);
            Assert.Contains("Nothing is purchased", scope.Notice);
            Assert.DoesNotMatch(@"\d", scope.Notice);
        });
    }

    [Fact]
    public void A_missing_scope_is_not_invented()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            FactoryBudget.Read([new StoredBudget(FactoryBudget.Daily, null, null, null)]));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void A_ceiling_without_spend_is_not_compared()
    {
        var decision = FactoryBudget.Apply("daily", 25m, "usd", null, "Operator supplied the ceiling");
        Assert.False(decision.Degraded);
        Assert.Contains("25 USD", decision.Notice);
        Assert.Contains("not on file", decision.Notice);
        Assert.Contains("not compared", decision.Notice);
        Assert.Contains("Nothing is purchased", decision.Notice);
        Assert.Contains("not an Economics price", decision.Notice);
    }

    [Fact]
    public void Spend_at_the_ceiling_degrades_only_that_scope()
    {
        var decision = FactoryBudget.Apply(FactoryBudget.Daily, 25m, "USD", 25m, "Spend meets the ceiling");
        Assert.True(decision.Degraded);
        Assert.Contains("ceiling is reached", decision.Notice);
        Assert.Contains("Other scopes are unchanged", decision.Notice);
        Assert.Contains("Nothing is purchased", decision.Notice);
        Assert.Contains("NOT_SENT", decision.Notice);
    }

    [Fact]
    public void Spend_under_the_ceiling_stays_open()
    {
        var decision = FactoryBudget.Apply(FactoryBudget.Provider, 40m, "USD", 10m, "Spend is under the ceiling");
        Assert.False(decision.Degraded);
        Assert.Contains("ceiling is open", decision.Notice);
        Assert.Contains("Other scopes are unchanged", decision.Notice);
    }

    [Fact]
    public void A_partial_ceiling_is_refused()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            FactoryBudget.Apply(FactoryBudget.Monthly, 25m, null, null, "Missing currency"));
        Assert.Contains("None is invented", error.Message);
    }

    [Fact]
    public void Budget_source_does_not_send()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Bliss.Domain", "Operations", "FactoryBudget.cs"));
        Assert.Contains("Nothing is purchased", source);
        Assert.DoesNotContain("Smtp", source);
        Assert.DoesNotContain("HttpClient", source);
        Assert.DoesNotContain("OpenAI", source);
        Assert.DoesNotContain("$", source);
    }
}
