using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase6ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase6ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Compensation_api_returns_versioned_non_settlement_illustration()
    {
        Guid quoteId;
        Guid versionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new Phase2DataSeeder(db).SeedAsync();
            await new EconomicsDataSeeder(db).SeedAsync();
            await new EconomicsPhase2DataSeeder(db).SeedAsync();
            await new EconomicsPhase3DataSeeder(db).SeedAsync();
            await new EconomicsPhase4DataSeeder(db).SeedAsync();
            await new EconomicsPhase6DataSeeder(db).SeedAsync();
            var key = Guid.NewGuid().ToString("N");
            var recommendation = await new RateRecommendationService(db).GenerateAsync(new(
                Phase1DataSeeder.CreatorId,
                Guid.Parse("99999999-9999-9999-9999-999999999992"),
                EconomicsDataSeeder.ManilaId,
                "CPM",
                60,
                null,
                null,
                "WOMENS_FOOTWEAR",
                "API compensation test",
                "TEST",
                $"phase6-api-rec-{key}"));
            var quotes = new QuoteService(db);
            var quote = await quotes.CreateAsync(new(
                null,
                "TEST_OPERATOR",
                "API compensation quote",
                [new(
                    recommendation.Recommendation.Id,
                    "TEST inventory",
                    1m,
                    215m)],
                "TEST",
                $"phase6-api-quote-{key}"));
            quoteId = quote.Quote.Id;
            versionId = quote.NewQuoteVersionId!.Value;
            await quotes.DecideApprovalAsync(new(
                quoteId,
                versionId,
                QuoteApprovalDecisions.Approved,
                "TEST_REVIEWER",
                "API approval",
                "TEST",
                $"phase6-api-approval-{key}"));
            await quotes.RecordOutcomeAsync(new(
                quoteId,
                versionId,
                QuoteOutcomeResponses.Accepted,
                "TEST_ADVERTISER",
                "API acceptance",
                null,
                "TEST",
                $"phase6-api-outcome-{key}"));
        }

        var client = _factory.CreateClient();
        var idempotencyKey = $"phase6-api-illustration-{Guid.NewGuid():N}";
        var request = new
        {
            quoteId,
            quoteVersionId = versionId,
            compensationRuleVersionId = EconomicsPhase6DataSeeder.RuleVersionId,
            sourceSystem = "TEST",
            idempotencyKey
        };
        var created = await client.PostAsJsonAsync(
            "/api/economics/compensation-illustrations", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var root = createdJson.RootElement;
        Assert.Equal(215m, root.GetProperty("grossAmount").GetDecimal());
        Assert.Equal("economics-compensation/1.0.0-test",
            root.GetProperty("compensationRuleVersion").GetString());
        Assert.Equal(3, root.GetProperty("lines").GetArrayLength());
        Assert.Equal(38.70m, root.GetProperty("lines")[0].GetProperty("amount").GetDecimal());
        Assert.Equal(154.80m, root.GetProperty("lines")[1].GetProperty("amount").GetDecimal());
        Assert.Equal(21.50m, root.GetProperty("lines")[2].GetProperty("amount").GetDecimal());
        Assert.False(root.GetProperty("isReplay").GetBoolean());
        var illustrationId = root.GetProperty("id").GetGuid();

        var replay = await client.PostAsJsonAsync(
            "/api/economics/compensation-illustrations", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        using var replayJson = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.Equal(illustrationId, replayJson.RootElement.GetProperty("id").GetGuid());
        Assert.True(replayJson.RootElement.GetProperty("isReplay").GetBoolean());

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync(
                $"/api/economics/compensation-illustrations/{illustrationId}")).StatusCode);
        var policies = await client.GetAsync(
            "/api/economics/compensation-rule-versions");
        Assert.Equal(HttpStatusCode.OK, policies.StatusCode);
        Assert.Contains("OTHER_AUTHORIZED", await policies.Content.ReadAsStringAsync());
    }
}
