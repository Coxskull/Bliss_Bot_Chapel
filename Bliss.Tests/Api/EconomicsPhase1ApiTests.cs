using System.Net;
using System.Text;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase1ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase1ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reference_endpoints_return_structured_provenance()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<EconomicsDataSeeder>();
            await seeder.SeedAsync();
        }

        var client = _factory.CreateClient();
        var markets = await client.GetAsync("/api/economics/markets");
        var models = await client.GetAsync("/api/economics/pricing-models");
        var sources = await client.GetAsync("/api/economics/research-sources");
        var observations = await client.GetAsync("/api/economics/observations");

        Assert.Equal(HttpStatusCode.OK, markets.StatusCode);
        Assert.Equal(HttpStatusCode.OK, models.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sources.StatusCode);
        Assert.Equal(HttpStatusCode.OK, observations.StatusCode);

        using var marketJson = JsonDocument.Parse(await markets.Content.ReadAsStringAsync());
        using var modelJson = JsonDocument.Parse(await models.Content.ReadAsStringAsync());
        var observationBody = await observations.Content.ReadAsStringAsync();
        Assert.Equal(6, marketJson.RootElement.GetArrayLength());
        Assert.Equal(10, modelJson.RootElement.GetArrayLength());
        Assert.Contains("PH-MNL", await markets.Content.ReadAsStringAsync());
        Assert.Contains("TEST Synthetic Market Fixture", observationBody);
        Assert.Contains("\"verificationStatus\":\"ESTIMATED\"", observationBody);
        Assert.Contains("\"confidenceLevel\":\"LOW\"", observationBody);
        Assert.Contains("Not an Alpha rate", observationBody);
    }

    [Fact]
    public async Task Economics_reference_routes_do_not_expose_generic_writes_or_quotes()
    {
        var client = _factory.CreateClient();
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");

        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await client.PostAsync("/api/economics/observations", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync("/api/economics/calculations", body)).StatusCode);
    }
}
