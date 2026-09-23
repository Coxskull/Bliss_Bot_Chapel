using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class EconomicsPhase5ApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EconomicsPhase5ApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Quote_api_requires_explicit_human_approval_before_acceptance()
    {
        Guid recommendationId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new Phase2DataSeeder(db).SeedAsync();
            await new EconomicsDataSeeder(db).SeedAsync();
            await new EconomicsPhase2DataSeeder(db).SeedAsync();
            await new EconomicsPhase3DataSeeder(db).SeedAsync();
            await new EconomicsPhase4DataSeeder(db).SeedAsync();
            var result = await new RateRecommendationService(db).GenerateAsync(new(
                Phase1DataSeeder.CreatorId,
                Guid.Parse("99999999-9999-9999-9999-999999999992"),
                EconomicsDataSeeder.ManilaId,
                "CPM",
                60,
                Phase1DataSeeder.OpportunityAId,
                Phase1DataSeeder.MatchAId,
                "WOMENS_FOOTWEAR",
                "API quote test",
                "TEST",
                $"phase5-api-rec-{Guid.NewGuid():N}"));
            recommendationId = result.Recommendation.Id;
        }

        var client = _factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/economics/quotes", new
        {
            advertiserOpportunityId = Phase1DataSeeder.OpportunityAId,
            requestedBy = "TEST_OPERATOR",
            revisionReason = "Explicit API quote",
            lineItems = new[]
            {
                new
                {
                    rateRecommendationId = recommendationId,
                    description = "60-second Manila mid-roll",
                    quantity = 1m,
                    unitAmount = 225m
                }
            },
            sourceSystem = "TEST",
            idempotencyKey = $"phase5-api-quote-{Guid.NewGuid():N}"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var quoteId = createdJson.RootElement.GetProperty("id").GetGuid();
        var version = createdJson.RootElement.GetProperty("versions")[0];
        var versionId = version.GetProperty("id").GetGuid();
        Assert.Equal("DRAFT", createdJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(225m, version.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(242m, version.GetProperty("lineItems")[0]
            .GetProperty("recommendationTarget").GetDecimal());

        var earlyAccept = await client.PostAsJsonAsync(
            $"/api/economics/quotes/{quoteId}/outcomes",
            new
            {
                quoteVersionId = versionId,
                response = "ACCEPTED",
                actorLabel = "TEST_ADVERTISER",
                rationale = "Too early",
                sourceSystem = "TEST",
                idempotencyKey = $"phase5-api-early-{Guid.NewGuid():N}"
            });
        Assert.Equal(HttpStatusCode.BadRequest, earlyAccept.StatusCode);

        var approve = await client.PostAsJsonAsync(
            $"/api/economics/quotes/{quoteId}/approvals",
            new
            {
                quoteVersionId = versionId,
                decision = "APPROVED",
                reviewerLabel = "TEST_REVIEWER",
                rationale = "Human reviewed exact version",
                sourceSystem = "TEST",
                idempotencyKey = $"phase5-api-approve-{Guid.NewGuid():N}"
            });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var accept = await client.PostAsJsonAsync(
            $"/api/economics/quotes/{quoteId}/outcomes",
            new
            {
                quoteVersionId = versionId,
                response = "ACCEPTED",
                actorLabel = "TEST_ADVERTISER",
                rationale = "Advertiser accepted",
                sourceSystem = "TEST",
                idempotencyKey = $"phase5-api-accept-{Guid.NewGuid():N}"
            });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        using var acceptedJson = JsonDocument.Parse(await accept.Content.ReadAsStringAsync());
        Assert.Equal("ACCEPTED", acceptedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, acceptedJson.RootElement.GetProperty("approvalDecisions").GetArrayLength());
        Assert.Equal(1, acceptedJson.RootElement.GetProperty("outcomes").GetArrayLength());

        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync($"/api/economics/quotes/{quoteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/economics/quotes")).StatusCode);
    }
}
