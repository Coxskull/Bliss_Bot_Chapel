using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase7ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase7ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Research_api_stages_then_human_promotes_public_evidence()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new EconomicsDataSeeder(db).SeedAsync();
        }
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");

        var queued = await client.PostAsJsonAsync("/api/economics/research-runs", new
        {
            geographicMarketId = EconomicsDataSeeder.ManilaId,
            metric = "GDP_PER_CAPITA_CURRENT_USD",
            industryCategory = "WOMENS_FOOTWEAR",
            platform = "DIGITAL",
            researchQuestion = "Find public economic context for Manila.",
            requestedBy = "TEST_OPERATOR",
            sourceSystem = "TEST",
            idempotencyKey = $"phase7-api-run-{suffix}"
        });
        Assert.Equal(HttpStatusCode.Created, queued.StatusCode);
        using var queuedJson = JsonDocument.Parse(await queued.Content.ReadAsStringAsync());
        var runId = queuedJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("QUEUED", queuedJson.RootElement.GetProperty("status").GetString());

        var staged = await client.PostAsJsonAsync(
            $"/api/economics/research-runs/{runId}/candidates",
            new
            {
                numericValue = 4025.25m,
                currencyCode = "USD",
                sourceName = "World Bank Open Data API",
                sourceUrl = "https://api.worldbank.org/v2/country/PHL/indicator/NY.GDP.PCAP.CD",
                sourceType = "PUBLIC_API",
                publicationDate = "2024-12-31",
                retrievedAt = DateTime.UtcNow,
                confidenceLevel = "MEDIUM",
                verificationStatus = "ESTIMATED",
                extractionModel = "TEST_STRUCTURED_EXTRACTION_V1",
                rawPayloadJson = """{"value":4025.25,"aiRole":"extraction only"}""",
                sourceSystem = "N8N_RESEARCH",
                idempotencyKey = $"phase7-api-candidate-{suffix}"
            });
        Assert.Equal(HttpStatusCode.OK, staged.StatusCode);
        using var stagedJson = JsonDocument.Parse(await staged.Content.ReadAsStringAsync());
        Assert.Equal("AWAITING_REVIEW",
            stagedJson.RootElement.GetProperty("status").GetString());
        var candidate = stagedJson.RootElement.GetProperty("candidates")[0];
        var candidateId = candidate.GetProperty("id").GetGuid();
        Assert.Equal("STAGED", candidate.GetProperty("status").GetString());
        Assert.False(candidate.TryGetProperty("promotedObservationId", out _));

        var reviewed = await client.PostAsJsonAsync(
            $"/api/economics/research-candidates/{candidateId}/review",
            new
            {
                decision = "ACCEPT",
                reviewerLabel = "TEST_REVIEWER",
                rationale = "Public source and extracted value reviewed",
                sourceSystem = "TEST",
                idempotencyKey = $"phase7-api-review-{suffix}"
            });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        using var reviewedJson = JsonDocument.Parse(await reviewed.Content.ReadAsStringAsync());
        Assert.Equal("COMPLETED",
            reviewedJson.RootElement.GetProperty("status").GetString());
        var promoted = reviewedJson.RootElement.GetProperty("candidates")[0];
        Assert.Equal("PROMOTED", promoted.GetProperty("status").GetString());
        Assert.NotEqual(Guid.Empty,
            promoted.GetProperty("promotedObservationId").GetGuid());
        Assert.Equal("ESTIMATED",
            promoted.GetProperty("verificationStatus").GetString());

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/economics/research-runs/{runId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/economics/research-runs")).StatusCode);
    }

    [Fact]
    public async Task Research_api_rejects_ai_verified_claim()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new EconomicsDataSeeder(db).SeedAsync();
        }
        var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var queued = await client.PostAsJsonAsync("/api/economics/research-runs", new
        {
            geographicMarketId = EconomicsDataSeeder.ManilaId,
            metric = "TEST_METRIC",
            researchQuestion = "TEST validation boundary",
            requestedBy = "TEST_OPERATOR",
            sourceSystem = "TEST",
            idempotencyKey = $"phase7-api-invalid-run-{suffix}"
        });
        using var queuedJson = JsonDocument.Parse(await queued.Content.ReadAsStringAsync());
        var runId = queuedJson.RootElement.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"/api/economics/research-runs/{runId}/candidates",
            new
            {
                numericValue = 1m,
                sourceName = "TEST source",
                sourceUrl = "https://example.test/source",
                sourceType = "PUBLIC_WEB",
                retrievedAt = DateTime.UtcNow,
                confidenceLevel = "HIGH",
                verificationStatus = "VERIFIED",
                extractionModel = "TEST_MODEL",
                rawPayloadJson = "{}",
                sourceSystem = "N8N_RESEARCH",
                idempotencyKey = $"phase7-api-invalid-candidate-{suffix}"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("confidence", await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }
}
