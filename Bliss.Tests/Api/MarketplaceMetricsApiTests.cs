using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class MarketplaceMetricsApiTests
{
    [Fact]
    public async Task A_marketplace_reading_matches_stored_rows_and_does_not_record_revenue()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var balance = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/balance");
        var inventory = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/creative-inventory");
        var advertisers = balance!.GetProperty("advertiserCount").GetInt32();
        var creators = balance.GetProperty("creatorCount").GetInt32();
        var slots = inventory!.GetProperty("slotCount").GetInt32();

        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/metrics");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("censusClaimed").GetBoolean());
        Assert.False(before.GetProperty("revenueRecorded").GetBoolean());
        Assert.False(before.GetProperty("slotsChanged").GetBoolean());
        Assert.Equal(advertisers, before.GetProperty("advertiserCount").GetInt32());
        Assert.Equal(creators, before.GetProperty("creatorCount").GetInt32());
        Assert.Equal(slots, before.GetProperty("slotCount").GetInt32());
        Assert.Equal(0, before.GetProperty("revenueRowCount").GetInt32());
        Assert.Equal(0, before.GetProperty("history").GetArrayLength());
        Assert.Equal("Revenue is not recorded. None was invented.", before.GetProperty("balanceRevenue").GetString());
        Assert.Equal("Inventory is not recorded. None was invented.", before.GetProperty("balanceInventory").GetString());
        Assert.Contains("not a census", before.GetProperty("notice").GetString());

        var stored = await client.PostAsJsonAsync("/api/operations/metrics", new { metricKey = "metrics-reading-1" });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("revenueRecorded").GetBoolean());
        var row = first.GetProperty("history")[0];
        Assert.Equal("metrics-reading-1", row.GetProperty("metricKey").GetString());
        Assert.Equal(advertisers, row.GetProperty("advertiserCount").GetInt32());
        Assert.Equal(creators, row.GetProperty("creatorCount").GetInt32());
        Assert.Equal(slots, row.GetProperty("slotCount").GetInt32());
        Assert.Equal(0, row.GetProperty("revenueRowCount").GetInt32());
        Assert.False(row.GetProperty("censusClaimed").GetBoolean());
        Assert.Equal("NOT_SENT", row.GetProperty("delivery").GetString());
        Assert.Contains("No revenue row is on file", row.GetProperty("revenueLine").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/metrics", new { metricKey = "metrics-reading-1" });
        var second = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.Equal(1, second.GetProperty("history").GetArrayLength());

        var revenue = await client.PostAsJsonAsync("/api/operations/metrics", new { metricKey = "metrics-reading-1", revenueAmount = 215 });
        Assert.Equal(HttpStatusCode.BadRequest, revenue.StatusCode);
        var refused = await revenue.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not on file", refused!.GetProperty("error").GetString());
        Assert.False(refused.GetProperty("revenueRecorded").GetBoolean());

        var added = await client.PostAsJsonAsync("/api/operations/metrics", new { metricKey = "metrics-reading-2", addSlot = true });
        Assert.Equal(HttpStatusCode.BadRequest, added.StatusCode);
        Assert.Contains("not added", (await added.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("error").GetString());

        var afterBalance = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/balance");
        var afterInventory = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/creative-inventory");
        Assert.Equal("Revenue is not recorded. None was invented.", afterBalance!.GetProperty("revenue").GetString());
        Assert.Equal("Inventory is not recorded. None was invented.", afterBalance.GetProperty("inventory").GetString());
        Assert.Equal(advertisers, afterBalance.GetProperty("advertiserCount").GetInt32());
        Assert.Equal(slots, afterInventory!.GetProperty("slotCount").GetInt32());
        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/metrics");
        Assert.Equal(1, after!.GetProperty("history").GetArrayLength());
        Assert.Equal(0, after.GetProperty("revenueRowCount").GetInt32());
    }
}
