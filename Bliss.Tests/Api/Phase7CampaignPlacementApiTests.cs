using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase7CampaignPlacementApiTests : IClassFixture<BlissApiFactory>
{
    private static readonly Guid BrazilSlotId =
        Guid.Parse("99999999-9999-9999-9999-999999999997");
    private readonly BlissApiFactory _factory;

    public Phase7CampaignPlacementApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_then_replay_exposes_one_planned_binding()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var request = Request($"api-binding-{Guid.NewGuid():N}");

        var queue = await client.GetStringAsync("/api/campaign-bindings/queue");
        Assert.Contains(Phase2DataSeeder.MatchBrazilApprovedId.ToString(), queue);

        var created = await client.PostAsJsonAsync("/api/campaign-placements", request);
        var first = await created.Content.ReadFromJsonAsync<CampaignPlacementResultDto>();
        var replayed = await client.PostAsJsonAsync("/api/campaign-placements", request);
        var replay = await replayed.Content.ReadFromJsonAsync<CampaignPlacementResultDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.False(first!.IsReplay);
        Assert.True(replay!.IsReplay);
        Assert.Equal(first.RunId, replay.RunId);
        Assert.Equal(first.CampaignPlacementId, replay.CampaignPlacementId);
        Assert.Equal("PLANNED", first.Outcome);

        var detail = await client.GetAsync($"/api/campaign-placement-runs/{first.RunId}");
        var campaign = await client.GetStringAsync($"/api/campaigns/{Phase2DataSeeder.Campaign2Id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains(first.BlissMatchId.ToString(), campaign);
        Assert.Contains("\"inputSnapshot\"", await detail.Content.ReadAsStringAsync());

        var after = await client.GetStringAsync("/api/campaign-bindings/queue");
        Assert.DoesNotContain(Phase2DataSeeder.MatchBrazilApprovedId.ToString(), after);
    }

    [Fact]
    public async Task Nonapproved_match_returns_bad_request()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var request = Request($"api-invalid-{Guid.NewGuid():N}") with
        {
            BlissMatchId = Phase1DataSeeder.MatchAId
        };

        var response = await client.PostAsJsonAsync("/api/campaign-placements", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("APPROVED", await response.Content.ReadAsStringAsync());
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
    }

    private static CampaignPlacementRequest Request(string key) => new(
        SourceSystem: "DashboardFixture",
        IdempotencyKey: key,
        OperatorLabel: "TEST_OPERATOR",
        BlissMatchId: Phase2DataSeeder.MatchBrazilApprovedId,
        CampaignId: Phase2DataSeeder.Campaign2Id,
        ContentItemId: Phase2DataSeeder.BrazilContentId,
        AdInventorySlotId: BrazilSlotId);
}
