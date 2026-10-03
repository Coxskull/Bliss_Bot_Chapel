using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class ContactRouteApiTests
{
    [Fact]
    public async Task A_transmission_request_is_audited_once_and_the_route_stays_unsent()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/routes");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.False(before.GetProperty("censusClaimed").GetBoolean());
        Assert.Contains("not permission to send", before.GetProperty("notice").GetString());
        Assert.Equal(0, before.GetProperty("stored").GetInt32());
        Assert.Equal(0, before.GetProperty("audits").GetArrayLength());

        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = "https://example.com/casa-verde"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var discovered = await client.GetFromJsonAsync<JsonElement>("/api/operations/routes");
        var route = Assert.Single(discovered!.GetProperty("routes").EnumerateArray());
        Assert.Equal("Casa Verde", route.GetProperty("businessName").GetString());
        Assert.Equal("MISSING_EVIDENCE", route.GetProperty("route").GetString());
        Assert.Equal("none", route.GetProperty("adapter").GetString());
        Assert.Equal("NOT_SENT", route.GetProperty("transmission").GetString());
        Assert.Contains("None was invented", route.GetProperty("notice").GetString());

        var blank = await client.PostAsJsonAsync("/api/operations/routes/transmission", new
        {
            authorization = " ",
            idempotencyKey = "operator-transmission-1"
        });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var blankBody = await blank.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NOT_SENT", blankBody.GetProperty("delivery").GetString());
        Assert.False(blankBody.GetProperty("greenMeansSend").GetBoolean());

        var recorded = await client.PostAsJsonAsync("/api/operations/routes/transmission", new
        {
            authorization = "Send the message",
            idempotencyKey = "operator-transmission-1"
        });
        var first = await recorded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("duplicate").GetBoolean());
        Assert.Equal("none", first.GetProperty("adapter").GetString());
        Assert.Equal("NOT_SENT", first.GetProperty("transmission").GetString());
        Assert.Contains("No adapter transmitted it", first.GetProperty("notice").GetString());

        var repeated = await client.PostAsJsonAsync("/api/operations/routes/transmission", new
        {
            authorization = "Send it through a mailbox",
            idempotencyKey = "operator-transmission-1"
        });
        var second = await repeated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.False(second.GetProperty("written").GetBoolean());
        Assert.Equal("NOT_SENT", second.GetProperty("transmission").GetString());
        Assert.Contains("already recorded", second.GetProperty("notice").GetString());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/routes");
        var audit = Assert.Single(after!.GetProperty("audits").EnumerateArray());
        Assert.Equal("operator-transmission-1", audit.GetProperty("idempotencyKey").GetString());
        Assert.Equal("Send the message", audit.GetProperty("authorizationLine").GetString());
        Assert.Equal("none", audit.GetProperty("adapter").GetString());
        Assert.Equal("NOT_SENT", audit.GetProperty("transmission").GetString());
        Assert.Equal("NOT_SENT", after.GetProperty("delivery").GetString());
    }
}
