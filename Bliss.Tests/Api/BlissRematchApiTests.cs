using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class BlissRematchApiTests
{
    private const string Phase2Json = """
        {
          "minAudienceSize": 10000,
          "requireOpportunityMarketCountry": true,
          "requireLanguageOverlap": true,
          "prohibitedCategories": [ "Alcohol" ],
          "weights": { "geography": 0.4, "audienceSize": 0.3, "language": 0.3 }
        }
        """;

    [Fact]
    public async Task Approved_alternate_matches_the_evaluator_and_sends_nothing()
    {
        await using var factory = new BlissApiFactory();
        var currentId = Guid.NewGuid();
        var approvedId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        await SeedAsync(factory, opportunityId, EntityStatuses.Active, true,
            Creator(currentId, "Current Harbor", "PH"),
            Creator(approvedId, "Luz Canal", "PA"),
            Creator(Guid.NewGuid(), "Far Coast", "US"));

        var client = factory.CreateClient();
        await Discover(client, "Harbor Table");
        var response = await client.PostAsJsonAsync("/api/demonstrations/harbor-table/rematch", new
        {
            opportunityId,
            currentCreatorId = currentId
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var approved = database.Creators.Single(item => item.Id == approvedId);
        var opportunity = database.AdvertiserOpportunities.Single(item => item.Id == opportunityId);
        var rules = DeterministicRuleEvaluator.ParseDocument(Phase2Json);
        var expected = DeterministicRuleEvaluator.Evaluate(approved, opportunity, rules);

        Assert.Equal("Luz Canal", body!.GetProperty("rematchCreatorName").GetString());
        Assert.Equal(EntityStatuses.Approved, body.GetProperty("rematchStatus").GetString());
        Assert.Equal(expected.OverallScore, body.GetProperty("rematchScore").GetDecimal());
        Assert.Equal("Harbor Table", body.GetProperty("businessName").GetString());
        Assert.Equal("OPPORTUNITY_SCORED", body.GetProperty("prospectState").GetString());
        Assert.NotEqual("WON", body.GetProperty("prospectState").GetString());
        Assert.Equal("NOT_SENT", body.GetProperty("delivery").GetString());
        var notice = body.GetProperty("rematchNotice").GetString()!;
        Assert.Contains("Bliss approved Luz Canal through the accepted evaluator", notice);
        Assert.Contains("not a win", notice);
        Assert.Contains("NOT_SENT", notice);
        Assert.DoesNotContain("Current Harbor", notice);
        Assert.DoesNotContain("Far Coast", notice);
        Assert.DoesNotContain("$", notice);
        Assert.DoesNotContain("1", notice);
    }

    [Fact]
    public async Task No_approved_alternate_keeps_the_advertiser()
    {
        await using var factory = new BlissApiFactory();
        var opportunityId = Guid.NewGuid();
        await SeedAsync(factory, opportunityId, EntityStatuses.Active, true,
            Creator(Guid.NewGuid(), "Current Harbor", "PH"),
            Creator(Guid.NewGuid(), "Far Coast", "US"));

        var client = factory.CreateClient();
        await Discover(client, "Kept Table");
        var response = await client.PostAsJsonAsync("/api/demonstrations/kept-table/rematch", new
        {
            opportunityId
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var notice = body!.GetProperty("rematchNotice").GetString()!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no other approved match", notice);
        Assert.Contains("advertiser is kept", notice);
        Assert.Contains("not a win", notice);
        Assert.Contains("NOT_SENT", notice);
        Assert.Equal(string.Empty, body.GetProperty("rematchCreatorName").GetString());
        Assert.False(body.TryGetProperty("rematchScore", out _));
        Assert.Equal("Kept Table", body.GetProperty("businessName").GetString());
        Assert.Equal("NOT_SENT", body.GetProperty("delivery").GetString());
        Assert.DoesNotContain("Current Harbor", notice);
        Assert.DoesNotContain("Far Coast", notice);
        Assert.DoesNotContain("Luz Canal", notice);
        Assert.DoesNotMatch(@"\d", notice);
    }

    [Fact]
    public async Task A_missing_opportunity_names_nobody()
    {
        await using var factory = new BlissApiFactory();
        await SeedAsync(factory, Guid.NewGuid(), EntityStatuses.Active, true,
            Creator(Guid.NewGuid(), "Luz Canal", "PA"));

        var client = factory.CreateClient();
        await Discover(client, "Mesa Norte");
        var response = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/rematch", new { });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var notice = body!.GetProperty("rematchNotice").GetString()!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("will not rematch without an active opportunity", notice);
        Assert.Contains("advertiser is kept", notice);
        Assert.Contains("NOT_SENT", notice);
        Assert.Equal("Mesa Norte", body.GetProperty("businessName").GetString());
        Assert.Equal(string.Empty, body.GetProperty("rematchCreatorName").GetString());
        Assert.Equal("NOT_SENT", body.GetProperty("delivery").GetString());
        Assert.DoesNotContain("Luz Canal", notice);
        Assert.DoesNotMatch(@"\d", notice);

        var again = await client.GetAsync("/api/demonstrations/mesa-norte");
        var stored = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(notice, stored!.GetProperty("rematchNotice").GetString());
        Assert.Equal("NOT_SENT", stored.GetProperty("delivery").GetString());
    }

    private static async Task Discover(HttpClient client, string businessName)
    {
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName,
            publicSourceUrl = "https://example.com/" + businessName.ToLowerInvariant().Replace(' ', '-')
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    private static async Task SeedAsync(
        BlissApiFactory factory,
        Guid opportunityId,
        string status,
        bool activeRules,
        params Creator[] creators)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var now = DateTime.UtcNow;
        var advertiser = new Advertiser
        {
            Id = Guid.NewGuid(),
            Name = "Fixture advertiser",
            CreatedAt = now
        };
        var program = new AdvertiserProgram
        {
            Id = Guid.NewGuid(),
            AdvertiserId = advertiser.Id,
            Name = "Fixture program",
            Status = EntityStatuses.Active
        };
        database.Advertisers.Add(advertiser);
        database.AdvertiserPrograms.Add(program);
        database.AdvertiserOpportunities.Add(new AdvertiserOpportunity
        {
            Id = opportunityId,
            AdvertiserProgramId = program.Id,
            Name = "Fixture opportunity",
            Category = "Creator Tools",
            MarketCountryCode = "PA",
            Language = "English",
            Status = status
        });
        database.RuleVersions.Add(new RuleVersion
        {
            Id = Guid.NewGuid(),
            Version = "rematch-fixture",
            Name = "Phase 2 fixture",
            DocumentJson = activeRules ? Phase2Json : null,
            IsActive = activeRules,
            CreatedAt = now
        });
        database.Creators.AddRange(creators);
        await database.SaveChangesAsync();
    }

    private static Creator Creator(Guid id, string name, string country) => new()
    {
        Id = id,
        Name = name,
        CountryCode = country,
        PrimaryLanguage = "English",
        AudienceSize = 20000,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
