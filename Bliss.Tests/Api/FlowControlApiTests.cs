using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class FlowControlApiTests
{
    [Fact]
    public async Task Regulating_flow_keeps_every_stored_prospect()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        foreach (var business in new[] { "Mesa Norte", "Casa Verde", "Puerto Azul" })
        {
            var market = business == "Puerto Azul" ? "Quito" : "Panama City";
            var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
            {
                niche = "restaurant",
                market,
                businessName = business,
                publicSourceUrl = "https://example.com/" + business.ToLowerInvariant().Replace(' ', '-')
            });
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        }

        var suppressed = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/suppression", new
        {
            reason = "The business asked Alpha to stop"
        });
        Assert.Equal(HttpStatusCode.OK, suppressed.StatusCode);
        var before = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/library");
        var beforeNames = before.GetProperty("demonstrations").EnumerateArray()
            .Select(item => item.GetProperty("businessName").GetString())
            .OrderBy(item => item)
            .ToArray();

        var response = await client.PostAsJsonAsync("/api/demonstrations/flow", new { capacity = 1 });
        var board = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(beforeNames.Length, board!.GetProperty("preserved").GetInt32());
        Assert.Equal(1, board.GetProperty("released").GetInt32());
        Assert.True(board.GetProperty("held").GetInt32() >= 1);
        Assert.Equal(1, board.GetProperty("withheld").GetInt32());
        Assert.Equal(0, board.GetProperty("discarded").GetInt32());
        Assert.Contains("None was discarded", board.GetProperty("notice").GetString());
        Assert.Contains("Green does not send", board.GetProperty("notice").GetString());
        Assert.Equal("NOT_SENT", board.GetProperty("delivery").GetString());
        Assert.False(board.GetProperty("greenMeansSend").GetBoolean());
        Assert.Contains("not an Economics price", board.GetProperty("capacityNotice").GetString());
        var withheld = board.GetProperty("assignments").EnumerateArray()
            .Single(item => item.GetProperty("lane").GetString() == "WITHHELD");
        Assert.Equal("Puerto Azul", withheld.GetProperty("businessName").GetString());

        var refused = await client.PostAsJsonAsync("/api/demonstrations/flow", new { capacity = 0 });
        var error = await refused.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("None is invented", error!.GetProperty("error").GetString());
        Assert.Equal(0, error.GetProperty("discarded").GetInt32());
        Assert.Equal("NOT_SENT", error.GetProperty("delivery").GetString());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/library");
        var afterNames = after.GetProperty("demonstrations").EnumerateArray()
            .Select(item => item.GetProperty("businessName").GetString())
            .OrderBy(item => item)
            .ToArray();
        Assert.Equal(beforeNames, afterNames);
        var puerto = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/puerto-azul");
        Assert.Equal("PRESERVED", puerto.GetProperty("prospectState").GetString());
        Assert.True(puerto.GetProperty("suppressed").GetBoolean());
        Assert.Equal("NOT_SENT", puerto.GetProperty("delivery").GetString());
    }
}