using System.Net;
using System.Net.Http.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase10ObservabilityTests : IClassFixture<ObservabilityApiFactory>
{
    private readonly ObservabilityApiFactory _factory;

    public Phase10ObservabilityTests(ObservabilityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Responses_include_generated_and_accepted_request_ids()
    {
        var client = _factory.CreateClient();
        var generated = await client.GetAsync("/api/runtime/status");
        Assert.True(generated.Headers.TryGetValues("X-Request-Id", out var generatedValues));
        Assert.True(Guid.TryParse(generatedValues.Single(), out _));

        var expected = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add("X-Request-Id", expected);
        var echoed = await client.GetAsync("/health/live");
        Assert.Equal(expected, echoed.Headers.GetValues("X-Request-Id").Single());
    }

    [Fact]
    public async Task Invalid_request_ids_are_replaced()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Request-Id", "not-a-guid<script>");
        var response = await client.GetAsync("/health/live");
        var requestId = response.Headers.GetValues("X-Request-Id").Single();
        Assert.NotEqual("not-a-guid<script>", requestId);
        Assert.True(Guid.TryParse(requestId, out _));
    }

    [Fact]
    public async Task Write_throttle_events_appear_on_runtime_status()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/runtime/throttle-check", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/runtime/throttle-check", null)).StatusCode);
        var limited = await client.PostAsync("/api/runtime/throttle-check", null);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("X-Request-Id"));

        var status = await client.GetFromJsonAsync<RuntimeStatusResponse>("/api/runtime/status");
        Assert.NotNull(status);
        Assert.Equal("Healthy", status.ProcessStatus);
        Assert.Contains(status.RecentEvents, item => item.Kind == "RateLimited" && item.Path == "/api/runtime/throttle-check");
    }
}

public sealed class ObservabilityApiFactory : WebApplicationFactory<Program>
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
                options => options.UseInMemoryDatabase($"observability-{Guid.NewGuid()}"));
        });
    }
}

public sealed record RuntimeStatusResponse(
    string Environment,
    bool AuthenticationEnabled,
    string ProcessStatus,
    string DatabaseStatus,
    string? LastRequestId,
    RuntimeEventResponse[] RecentEvents);

public sealed record RuntimeEventResponse(
    DateTime OccurredAt,
    string Kind,
    string Method,
    string Path,
    int StatusCode,
    string RequestId);
