using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;

namespace Bliss.Tests.Api;

public sealed class Phase4CreatorIngestionApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase4CreatorIngestionApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_then_replay_returns_created_then_ok_with_same_run()
    {
        var client = _factory.CreateClient();
        var request = Request($"api-{Guid.NewGuid():N}");

        var created = await client.PostAsJsonAsync("/api/creator-ingestions", request);
        var first = await created.Content.ReadFromJsonAsync<CreatorIngestionResultDto>();
        var replayed = await client.PostAsJsonAsync("/api/creator-ingestions", request);
        var replay = await replayed.Content.ReadFromJsonAsync<CreatorIngestionResultDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(replay);
        Assert.False(first!.IsReplay);
        Assert.True(replay!.IsReplay);
        Assert.Equal(first.RunId, replay.RunId);
        Assert.Equal(first.CreatorId, replay.CreatorId);

        var creator = await client.GetAsync($"/api/creators/{first.CreatorId}");
        var creatorBody = await creator.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, creator.StatusCode);
        Assert.Contains("\"countryCode\":\"CA\"", creatorBody);
        Assert.Contains("\"primaryLanguage\":\"English\"", creatorBody);

        var detail = await client.GetAsync($"/api/creator-ingestions/{first.RunId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains("inputSnapshot", await detail.Content.ReadAsStringAsync());

        var all = await client.GetAsync("/api/creator-ingestions");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Contains(first.RunId.ToString(), await all.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_observation_returns_bad_request()
    {
        var client = _factory.CreateClient();
        var request = Request($"invalid-{Guid.NewGuid():N}") with { Followers = -4 };

        var response = await client.PostAsJsonAsync("/api/creator-ingestions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("cannot be negative", await response.Content.ReadAsStringAsync());
    }

    private static CreatorIngestionRequest Request(string idempotencyKey) => new(
        SourceSystem: "DashboardFixture",
        IdempotencyKey: idempotencyKey,
        Platform: "YouTube",
        ExternalProfileId: $"channel-{idempotencyKey}",
        CreatorName: "API Fixture Creator",
        ProfileUrl: "https://example.test/api-fixture",
        CountryCode: "CA",
        PrimaryLanguage: "English",
        AudienceSize: null,
        Followers: 15_000,
        FemalePercentage: null,
        MalePercentage: null,
        PrimaryAgeRange: null,
        PrimaryGeography: null,
        EngagementLevel: null,
        SourceUrl: "https://example.test/api-fixture/source",
        ConfidenceLevel: "MEDIUM",
        CollectedAt: null);
}
