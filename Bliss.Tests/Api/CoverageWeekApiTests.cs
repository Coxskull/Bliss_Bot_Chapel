using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class CoverageWeekApiTests
{
    [Fact]
    public async Task A_coverage_week_matches_the_fuel_reading_and_does_not_add_a_market()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var fuel = await client.GetFromJsonAsync<JsonElement>("/api/operations/fuel");
        var qualified = fuel!.GetProperty("fuel").GetProperty("qualifiedUnique").GetInt32();
        var status = fuel.GetProperty("fuel").GetProperty("fuelStatus").GetString();
        var marketCount = fuel.GetProperty("markets").GetArrayLength();

        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/week");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("censusClaimed").GetBoolean());
        Assert.False(before.GetProperty("slicesChanged").GetBoolean());
        Assert.Equal(qualified, before.GetProperty("qualifiedSlices").GetInt32());
        Assert.Equal(0, before.GetProperty("history").GetArrayLength());
        Assert.Contains("not a census", before.GetProperty("notice").GetString());

        var stored = await client.PostAsJsonAsync("/api/operations/week", new { weekKey = "coverage-week-1" });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("slicesChanged").GetBoolean());
        var row = first.GetProperty("history")[0];
        Assert.Equal("coverage-week-1", row.GetProperty("weekKey").GetString());
        Assert.Equal(qualified, row.GetProperty("qualifiedSlices").GetInt32());
        Assert.Equal(marketCount, row.GetProperty("marketCount").GetInt32());
        Assert.Equal(status, row.GetProperty("fuelStatus").GetString());
        Assert.False(row.GetProperty("censusClaimed").GetBoolean());
        Assert.Equal("NOT_SENT", row.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/week", new { weekKey = "coverage-week-1" });
        var second = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.Equal(1, second.GetProperty("history").GetArrayLength());

        var added = await client.PostAsJsonAsync("/api/operations/week", new { weekKey = "coverage-week-2", addMissingMarket = true });
        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        var refused = await added.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not added", refused!.GetProperty("error").GetString());
        Assert.False(refused.GetProperty("slicesChanged").GetBoolean());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/fuel");
        Assert.Equal(qualified, after!.GetProperty("fuel").GetProperty("qualifiedUnique").GetInt32());
        Assert.Equal(marketCount, after.GetProperty("markets").GetArrayLength());
    }
}
