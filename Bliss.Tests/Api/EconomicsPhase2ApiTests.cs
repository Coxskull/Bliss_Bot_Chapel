using System.Net;
using System.Text;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase2ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase2ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Snapshot_endpoints_return_versioned_metrics_and_provenance()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new EconomicsDataSeeder(db).SeedAsync();
            await new EconomicsPhase2DataSeeder(db).SeedAsync();
        }

        var client = _factory.CreateClient();
        var audience = await client.GetAsync(
            $"/api/economics/audience-snapshots?creatorId={Phase1DataSeeder.CreatorId}");
        var performance = await client.GetAsync(
            $"/api/economics/performance-snapshots?creatorId={Phase1DataSeeder.CreatorId}");

        Assert.Equal(HttpStatusCode.OK, audience.StatusCode);
        Assert.Equal(HttpStatusCode.OK, performance.StatusCode);
        using var audienceJson = JsonDocument.Parse(await audience.Content.ReadAsStringAsync());
        using var performanceJson = JsonDocument.Parse(await performance.Content.ReadAsStringAsync());
        Assert.Equal(2, audienceJson.RootElement.GetArrayLength());
        Assert.Equal(2, performanceJson.RootElement.GetArrayLength());

        var audienceBody = await audience.Content.ReadAsStringAsync();
        var performanceBody = await performance.Content.ReadAsStringAsync();
        Assert.Contains("\"subscribers\":42000", audienceBody);
        Assert.Contains("\"femalePercentage\":68", audienceBody);
        Assert.DoesNotContain("\"malePercentage\":0", audienceBody);
        Assert.Contains("TEST Synthetic Market Fixture", audienceBody);
        Assert.Contains("\"averageViews\":42000", performanceBody);
        Assert.Contains("\"retentionRate\":0.58", performanceBody);
        Assert.Contains("\"verificationStatus\":\"ESTIMATED\"", performanceBody);
    }

    [Fact]
    public async Task Snapshot_routes_are_read_only()
    {
        var client = _factory.CreateClient();
        using var body = new StringContent("{}", Encoding.UTF8, "application/json");

        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await client.PostAsync("/api/economics/audience-snapshots", body)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await client.PostAsync("/api/economics/performance-snapshots", body)).StatusCode);
    }
}
