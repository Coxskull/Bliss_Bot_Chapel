using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class FleetLanesApiTests
{
    [Fact]
    public async Task Reading_fleets_keeps_a_broken_lane_from_stopping_the_ocean()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var stopped = await client.PostAsJsonAsync("/api/operations/tempo", new
        {
            lane = "OUTREACH",
            tempo = "STOPPED",
            reason = "Pause this lane only"
        });
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/tempo");
        var audits = before!.GetProperty("audits").GetArrayLength();

        var response = await client.GetAsync("/api/operations/tempo/fleets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var board = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NOT_SENT", board!.GetProperty("delivery").GetString());
        Assert.False(board.GetProperty("greenMeansSend").GetBoolean());
        Assert.True(board.GetProperty("oceanMoving").GetBoolean());
        Assert.Contains("A broken lane does not stop the ocean", board.GetProperty("notice").GetString());
        Assert.Contains("Green does not send", board.GetProperty("notice").GetString());
        Assert.Contains("NOT_SENT", board.GetProperty("notice").GetString());
        Assert.Contains("Bliss Chapel stays the matching middle", board.GetProperty("bliss").GetString());
        Assert.Equal("The ocean keeps moving.", board.GetProperty("ocean").GetString());

        var fishing = Fleet(board, "FISHING");
        var creator = Fleet(board, "CREATOR");
        Assert.True(fishing.GetProperty("moving").GetBoolean());
        Assert.True(creator.GetProperty("moving").GetBoolean());
        Assert.Equal("The Fishing Fleet keeps moving.", fishing.GetProperty("notice").GetString());
        Assert.Equal("The Creator Fleet keeps moving.", creator.GetProperty("notice").GetString());
        Assert.Equal("Broken. This lane is stopped.", Place(fishing, "OUTREACH"));
        Assert.Equal("Moving. This lane keeps its own tempo.", Place(fishing, "DISCOVERY"));
        Assert.Equal("Moving. This lane keeps its own tempo.", Place(creator, "CREATOR_DISCOVERY"));

        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/tempo");
        Assert.Equal(audits, after!.GetProperty("audits").GetArrayLength());
        Assert.Equal("STOPPED", Lane(after, "OUTREACH").GetProperty("tempo").GetString());
        Assert.Equal("FULL", Lane(after, "DISCOVERY").GetProperty("tempo").GetString());
        Assert.Equal("FULL", Lane(after, "CREATOR_DISCOVERY").GetProperty("tempo").GetString());

        var creatorStopped = await client.PostAsJsonAsync("/api/operations/tempo", new
        {
            lane = "CREATOR_DISCOVERY",
            tempo = "STOPPED",
            reason = "Pause the creator fleet only"
        });
        Assert.Equal(HttpStatusCode.OK, creatorStopped.StatusCode);
        var second = await client.GetFromJsonAsync<JsonElement>("/api/operations/tempo/fleets");
        Assert.False(Fleet(second!, "CREATOR").GetProperty("moving").GetBoolean());
        Assert.True(Fleet(second, "FISHING").GetProperty("moving").GetBoolean());
        Assert.Contains("other fleet is not stopped", Fleet(second, "CREATOR").GetProperty("notice").GetString());
        Assert.True(second.GetProperty("oceanMoving").GetBoolean());
        Assert.Equal("NOT_SENT", second.GetProperty("delivery").GetString());
        Assert.False(second.GetProperty("greenMeansSend").GetBoolean());
    }

    private static JsonElement Fleet(JsonElement board, string name) =>
        board.GetProperty("fleets").EnumerateArray().Single(item => item.GetProperty("fleet").GetString() == name);

    private static string? Place(JsonElement fleet, string lane) =>
        fleet.GetProperty("lanes").EnumerateArray().Single(item => item.GetProperty("lane").GetString() == lane)
            .GetProperty("place").GetString();

    private static JsonElement Lane(JsonElement board, string name) =>
        board.GetProperty("lanes").EnumerateArray().Single(item => item.GetProperty("lane").GetString() == name);
}
