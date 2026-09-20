using System.Net;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class BlissApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                    || d.ServiceType == typeof(BlissDbContext))
                .ToList();
            foreach (var d in toRemove)
            {
                services.Remove(d);
            }

            services.AddDbContext<BlissDbContext>(options => options.UseInMemoryDatabase(_dbName));
        });
    }
}

public sealed class Phase2ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase2ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Campaigns_and_networks_return_phase2_records()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var campaigns = await client.GetAsync("/api/campaigns");
        Assert.Equal(HttpStatusCode.OK, campaigns.StatusCode);
        var body = await campaigns.Content.ReadAsStringAsync();
        Assert.Contains("Phase 2 Overlay Inventory Campaign", body);

        var networks = await client.GetAsync("/api/affiliate-networks");
        Assert.Equal(HttpStatusCode.OK, networks.StatusCode);
        var networkBody = await networks.Content.ReadAsStringAsync();
        Assert.Contains("Example Global Network", networkBody);

        var matches = await client.GetAsync("/api/bliss/matches");
        Assert.Equal(HttpStatusCode.OK, matches.StatusCode);
        var matchBody = await matches.Content.ReadAsStringAsync();
        Assert.Contains(Phase1DataSeeder.MatchAId.ToString(), matchBody);
        Assert.Contains(Phase2DataSeeder.MatchBrazilApprovedId.ToString(), matchBody);
    }

    [Fact]
    public async Task Evaluate_rules_returns_bad_request_for_phase1_rule_without_document()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();

        var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/bliss/matches/{Phase1DataSeeder.MatchAId}/evaluate-rules", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var stillA = await client.GetAsync($"/api/bliss/matches/{Phase1DataSeeder.MatchAId}");
        Assert.Equal(HttpStatusCode.OK, stillA.StatusCode);
    }
}
