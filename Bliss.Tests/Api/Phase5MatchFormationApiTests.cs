using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase5MatchFormationApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase5MatchFormationApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_then_replay_exposes_one_formation_and_evaluated_match()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var request = Request($"api-formation-{Guid.NewGuid():N}");

        var created = await client.PostAsJsonAsync("/api/bliss/matches", request);
        var first = await created.Content.ReadFromJsonAsync<MatchFormationResultDto>();
        var replayed = await client.PostAsJsonAsync("/api/bliss/matches", request);
        var replay = await replayed.Content.ReadFromJsonAsync<MatchFormationResultDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.False(first!.IsReplay);
        Assert.True(replay!.IsReplay);
        Assert.Equal(first.RunId, replay.RunId);
        Assert.Equal(first.BlissMatchId, replay.BlissMatchId);
        Assert.NotEqual("CREATED", first.MatchStatus);

        var match = await client.GetAsync($"/api/bliss/matches/{first.BlissMatchId}");
        var detail = await client.GetAsync($"/api/match-formation-runs/{first.RunId}");
        var all = await client.GetStringAsync("/api/match-formation-runs");
        Assert.Equal(HttpStatusCode.OK, match.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains(first.RunId.ToString(), all);
        Assert.Contains("\"inputSnapshot\"", await detail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_reference_returns_bad_request_without_formation()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var request = Request($"api-invalid-{Guid.NewGuid():N}") with { CreatorId = Guid.NewGuid() };

        var response = await client.PostAsJsonAsync("/api/bliss/matches", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("existing creator", await response.Content.ReadAsStringAsync());
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
    }

    private static MatchFormationRequest Request(string key) => new(
        SourceSystem: "DashboardFixture",
        IdempotencyKey: key,
        CreatorId: Phase1DataSeeder.CreatorId,
        AdvertiserOpportunityId: Phase2DataSeeder.OpportunityPhId,
        RuleVersionId: Phase2DataSeeder.RuleVersion2Id,
        EvaluateOnCreate: true);
}
