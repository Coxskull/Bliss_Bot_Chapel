using System.Net;
using System.Text;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase3ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase3ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Phase3_endpoints_return_versioned_sourced_context()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new EconomicsDataSeeder(db).SeedAsync();
            await new EconomicsPhase3DataSeeder(db).SeedAsync();
        }

        var client = _factory.CreateClient();
        var market = await client.GetAsync("/api/economics/market-profiles");
        var industry = await client.GetAsync("/api/economics/industry-profiles");
        var inventory = await client.GetAsync("/api/economics/inventory-benchmarks");
        var fx = await client.GetAsync("/api/economics/exchange-rates?baseCurrency=USD&quoteCurrency=PHP");

        Assert.All(new[] { market, industry, inventory, fx },
            response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        using var marketJson = JsonDocument.Parse(await market.Content.ReadAsStringAsync());
        using var inventoryJson = JsonDocument.Parse(await inventory.Content.ReadAsStringAsync());
        using var fxJson = JsonDocument.Parse(await fx.Content.ReadAsStringAsync());
        Assert.Equal(2, marketJson.RootElement.GetArrayLength());
        Assert.Equal(2, inventoryJson.RootElement.GetArrayLength());
        Assert.Equal(2, fxJson.RootElement.GetArrayLength());

        var inventoryBody = await inventory.Content.ReadAsStringAsync();
        Assert.Contains("\"inventorySlotType\":\"MID_ROLL\"", inventoryBody);
        Assert.Contains("\"inventorySlotType\":\"SPONSORED_SEGMENT\"", inventoryBody);
        Assert.Contains("\"pricingModelCode\":\"CPM\"", inventoryBody);
        Assert.Contains("\"durationSecondsLow\":30", inventoryBody);
        Assert.Contains("\"durationSecondsLow\":300", inventoryBody);
        Assert.Contains("TEST Synthetic Market Fixture", inventoryBody);
        Assert.Contains("WOMENS_FOOTWEAR", await industry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Phase3_routes_are_read_only()
    {
        var client = _factory.CreateClient();
        foreach (var route in new[]
        {
            "market-profiles", "industry-profiles", "inventory-benchmarks", "exchange-rates"
        })
        {
            using var body = new StringContent("{}", Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.MethodNotAllowed,
                (await client.PostAsync($"/api/economics/{route}", body)).StatusCode);
        }
    }
}
