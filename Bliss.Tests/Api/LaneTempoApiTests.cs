using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class LaneTempoApiTests
{
    [Fact]
    public async Task Stopping_outreach_leaves_discovery_moving_and_preserves_the_prospect()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/tempo");
        Assert.Equal(6, before.GetProperty("lanes").GetArrayLength());
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.Equal("NOT_SENT", before.GetProperty("delivery").GetString());
        Assert.Contains("does not stop the others", before.GetProperty("notice").GetString());
        Assert.All(before.GetProperty("lanes").EnumerateArray(), lane =>
        {
            Assert.Equal("FULL", lane.GetProperty("tempo").GetString());
            Assert.False(lane.TryGetProperty("ceilingAmount", out _));
        });

        var stopped = await Post(client, "OUTREACH", "STOPPED", null, null, "Pause this lane only");
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        var board = await stopped.Content.ReadFromJsonAsync<JsonElement>();
        var outreach = Lane(board!, "OUTREACH");
        var discovery = Lane(board, "DISCOVERY");
        Assert.Equal("STOPPED", outreach.GetProperty("tempo").GetString());
        Assert.Equal("FULL", discovery.GetProperty("tempo").GetString());
        Assert.Contains("Other lanes keep moving", outreach.GetProperty("notice").GetString());
        Assert.Contains("Green does not send", outreach.GetProperty("notice").GetString());
        Assert.Contains("NOT_SENT", outreach.GetProperty("notice").GetString());
        Assert.Contains("None was invented", outreach.GetProperty("notice").GetString());
        Assert.Equal(1, board.GetProperty("audits").GetArrayLength());
        Assert.Equal("Pause this lane only", board.GetProperty("audits")[0].GetProperty("reason").GetString());
        Assert.False(board.GetProperty("greenMeansSend").GetBoolean());

        var reduced = await Post(client, "DISCOVERY", "REDUCED", 40m, "USD", "Record an operational limit");
        var limited = await reduced.Content.ReadFromJsonAsync<JsonElement>();
        var discoveryAfter = Lane(limited!, "DISCOVERY");
        Assert.Equal("REDUCED", discoveryAfter.GetProperty("tempo").GetString());
        Assert.Equal(40m, discoveryAfter.GetProperty("ceilingAmount").GetDecimal());
        Assert.Equal("USD", discoveryAfter.GetProperty("ceilingCurrency").GetString());
        Assert.Contains("not an Economics price", discoveryAfter.GetProperty("notice").GetString());
        Assert.Equal("STOPPED", Lane(limited, "OUTREACH").GetProperty("tempo").GetString());
        Assert.Equal(2, limited.GetProperty("audits").GetArrayLength());

        var refused = await Post(client, "POLICY", "FULL", 10m, null, "Missing currency");
        var error = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("None is invented", error!.GetProperty("error").GetString());
        Assert.Equal("NOT_SENT", error.GetProperty("delivery").GetString());
        Assert.False(error.GetProperty("greenMeansSend").GetBoolean());

        var prospect = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/mesa-norte");
        Assert.Equal("Mesa Norte", prospect.GetProperty("businessName").GetString());
        Assert.Equal("OPPORTUNITY_SCORED", prospect.GetProperty("prospectState").GetString());
        Assert.Equal("NOT_SENT", prospect.GetProperty("delivery").GetString());
    }

    private static JsonElement Lane(JsonElement board, string name) =>
        board.GetProperty("lanes").EnumerateArray().Single(item => item.GetProperty("lane").GetString() == name);

    private static Task<HttpResponseMessage> Post(
        HttpClient client,
        string lane,
        string tempo,
        decimal? ceilingAmount,
        string? ceilingCurrency,
        string reason) =>
        client.PostAsJsonAsync("/api/operations/tempo", new
        {
            lane,
            tempo,
            ceilingAmount,
            ceilingCurrency,
            reason
        });
}
