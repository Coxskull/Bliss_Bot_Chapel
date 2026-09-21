using System.Net;
using System.Net.Http.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase9RuntimeHardeningTests : IClassFixture<RuntimeHardeningApiFactory>
{
    private readonly RuntimeHardeningApiFactory _factory;

    public Phase9RuntimeHardeningTests(RuntimeHardeningApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Liveness_and_database_readiness_are_machine_readable()
    {
        var client = _factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", await live.Content.ReadAsStringAsync());
        Assert.Contains("\"database\"", await ready.Content.ReadAsStringAsync());
        Assert.Contains("\"status\":\"Healthy\"", await ready.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Security_headers_are_applied_without_server_disclosure()
    {
        var response = await _factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Controlled_writes_are_rate_limited_per_runtime_policy()
    {
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/api/bliss/matches", InvalidMatchRequest());
        var second = await client.PostAsJsonAsync("/api/bliss/matches", InvalidMatchRequest());
        var limited = await client.PostAsJsonAsync("/api/bliss/matches", InvalidMatchRequest());

        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Contains("Rate limit exceeded", await limited.Content.ReadAsStringAsync());
    }

    private static object InvalidMatchRequest() => new
    {
        sourceSystem = "RUNTIME_TEST",
        idempotencyKey = $"runtime-{Guid.NewGuid()}",
        creatorId = Guid.NewGuid(),
        advertiserOpportunityId = Guid.NewGuid(),
        ruleVersionId = Guid.NewGuid(),
        evaluateOnCreate = false
    };
}

public sealed class RuntimeHardeningApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Runtime:WriteRateLimitPermitLimit", "2");
        builder.UseSetting("Runtime:RateLimitWindowSeconds", "300");
        builder.ConfigureServices(services =>
        {
            var toRemove = services.Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<BlissDbContext>)
                    || descriptor.ServiceType == typeof(BlissDbContext))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<BlissDbContext>(
                options => options.UseInMemoryDatabase($"runtime-{Guid.NewGuid()}"));
        });
    }
}
