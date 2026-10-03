using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class SubscriptionLedgerApiTests
{
    [Fact]
    public async Task The_register_starts_unpriced_and_a_reached_ceiling_degrades_one_scope()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/ledger");
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.Equal("NOT_SENT", before.GetProperty("delivery").GetString());
        Assert.Contains("None was invented", before.GetProperty("notice").GetString());
        Assert.Contains("Nothing is purchased", before.GetProperty("budgetNotice").GetString());
        Assert.Equal(10, before.GetProperty("services").GetArrayLength());
        Assert.Equal(4, before.GetProperty("budgets").GetArrayLength());
        var postgres = Service(before, "POSTGRESQL");
        Assert.False(postgres.TryGetProperty("classification", out _));
        Assert.False(postgres.TryGetProperty("monthlyAmount", out _));
        Assert.Contains("Cost is not recorded", postgres.GetProperty("costLine").GetString());
        Assert.All(before.GetProperty("budgets").EnumerateArray(), scope =>
        {
            Assert.False(scope.GetProperty("degraded").GetBoolean());
            Assert.False(scope.TryGetProperty("ceilingAmount", out _));
        });

        var degraded = await client.PostAsJsonAsync("/api/operations/ledger/budget", new
        {
            scope = "DAILY",
            ceilingAmount = 25m,
            ceilingCurrency = "USD",
            recordedSpend = 25m,
            reason = "Operator supplied the ceiling and the spend"
        });
        Assert.Equal(HttpStatusCode.OK, degraded.StatusCode);
        var board = await degraded.Content.ReadFromJsonAsync<JsonElement>();
        var daily = Budget(board, "DAILY");
        var monthly = Budget(board, "MONTHLY");
        Assert.True(daily.GetProperty("degraded").GetBoolean());
        Assert.Contains("ceiling is reached", daily.GetProperty("notice").GetString());
        Assert.Contains("Other scopes are unchanged", daily.GetProperty("notice").GetString());
        Assert.Contains("Nothing is purchased", daily.GetProperty("notice").GetString());
        Assert.False(monthly.GetProperty("degraded").GetBoolean());
        Assert.False(monthly.TryGetProperty("ceilingAmount", out _));
        Assert.Equal(1, board.GetProperty("budgetAudits").GetArrayLength());

        var refused = await client.PostAsJsonAsync("/api/operations/ledger/budget", new
        {
            scope = "MONTHLY",
            ceilingAmount = 10m,
            recordedSpend = 1m,
            reason = "Missing currency"
        });
        var error = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("None is invented", error!.GetProperty("error").GetString());
        Assert.Equal("NOT_SENT", error.GetProperty("delivery").GetString());
        Assert.False(error.GetProperty("greenMeansSend").GetBoolean());

        var incomplete = await client.PostAsJsonAsync("/api/operations/ledger/review", new
        {
            serviceName = "Enrichment suite",
            classification = "NEW_PAID_SUBSCRIPTION"
        });
        var reviewError = await incomplete.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("Nothing was purchased", reviewError!.GetProperty("error").GetString());
        var afterRefusal = await client.GetFromJsonAsync<JsonElement>("/api/operations/ledger");
        Assert.Equal(0, afterRefusal.GetProperty("audits").GetArrayLength());
        Assert.Equal(10, afterRefusal.GetProperty("services").GetArrayLength());

        var accepted = await client.PostAsJsonAsync("/api/operations/ledger/review", new
        {
            serviceName = "Contact graph",
            provider = "Example Provider",
            capability = "A function Alpha does not have",
            classification = "NEW_PAID_SUBSCRIPTION",
            engineeringContract = "Subscription ledger contract",
            monthlyAmount = 20m,
            monthlyCurrency = "USD",
            usageCharges = "No usage charge is known",
            alternatives = "PostgreSQL and the existing register",
            buildAlternative = "Keep the row unrecorded until a bill exists",
            whyAlphaIsInsufficient = "The register cannot store this function today",
            estimatedAmount = 20m,
            estimatedCurrency = "USD",
            requiredDate = "When a later contract needs it",
            requiredOrOptional = "OPTIONAL"
        });
        var reviewed = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(10, reviewed.GetProperty("services").GetArrayLength());
        var audit = reviewed.GetProperty("audits")[0];
        Assert.Equal("REVIEW", audit.GetProperty("action").GetString());
        Assert.Equal("NOT_PROPOSED", audit.GetProperty("status").GetString());
        Assert.Contains("not a purchase", audit.GetProperty("notice").GetString());

        var cost = await client.PostAsJsonAsync("/api/operations/ledger/cost", new
        {
            serviceKey = "BLISS_CHAPEL",
            amount = 1m,
            currency = "USD",
            source = "Operator supplied a development figure"
        });
        var priced = await cost.Content.ReadFromJsonAsync<JsonElement>();
        var bliss = Service(priced, "BLISS_CHAPEL");
        Assert.Equal("FREE_SELF_HOSTED", bliss.GetProperty("classification").GetString());
        Assert.Equal(1m, bliss.GetProperty("monthlyAmount").GetDecimal());
        Assert.Contains("Nothing was purchased", bliss.GetProperty("notice").GetString());
        Assert.False(Service(priced, "POSTGRESQL").TryGetProperty("monthlyAmount", out _));

        var prospect = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/mesa-norte");
        Assert.Equal("NOT_SENT", prospect.GetProperty("delivery").GetString());
    }

    private static JsonElement Service(JsonElement board, string key) =>
        board.GetProperty("services").EnumerateArray().Single(item => item.GetProperty("serviceKey").GetString() == key);

    private static JsonElement Budget(JsonElement board, string scope) =>
        board.GetProperty("budgets").EnumerateArray().Single(item => item.GetProperty("scope").GetString() == scope);
}
