using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class RotationPeriodApiTests
{
    [Fact]
    public async Task A_later_period_stores_the_measured_slots_and_does_not_fill_them()
    {
        await using var factory = new BlissApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
        }

        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/rotation");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.False(before.GetProperty("censusClaimed").GetBoolean());
        Assert.False(before.GetProperty("slotsChanged").GetBoolean());
        Assert.Contains("No revenue row is on file", before.GetProperty("revenueLine").GetString());
        Assert.Contains("not a census", before.GetProperty("notice").GetString());
        var placed = before.GetProperty("advertisers").GetArrayLength();
        var slots = before.GetProperty("slotCount").GetInt32();
        var advertisers = before.GetProperty("advertiserCount").GetInt32();
        Assert.True(placed > 0);
        Assert.True(placed <= 6);
        Assert.Equal(0, before.GetProperty("history").GetArrayLength());

        var stored = await client.PostAsJsonAsync("/api/operations/rotation", new
        {
            pair = 6,
            creatorApproved = true,
            periodKey = "later-period-1"
        });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("duplicate").GetBoolean());
        Assert.False(first.GetProperty("slotsChanged").GetBoolean());
        Assert.Equal(slots, first.GetProperty("slotCount").GetInt32());
        Assert.Equal(advertisers, first.GetProperty("advertiserCount").GetInt32());
        var row = first.GetProperty("history")[0];
        Assert.Equal("later-period-1", row.GetProperty("periodKey").GetString());
        Assert.Equal(6, row.GetProperty("theoreticalSlots").GetInt32());
        Assert.Equal(placed, row.GetProperty("placedAdvertisers").GetInt32());
        Assert.Equal(6 - placed, row.GetProperty("openSlots").GetInt32());
        Assert.Equal(slots, row.GetProperty("slotCount").GetInt32());
        Assert.True(row.GetProperty("creatorApproved").GetBoolean());
        Assert.False(row.GetProperty("censusClaimed").GetBoolean());
        Assert.False(row.GetProperty("slotsChanged").GetBoolean());
        Assert.Contains("No revenue row is on file", row.GetProperty("revenueLine").GetString());
        Assert.Equal("NOT_SENT", row.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/rotation", new
        {
            pair = 6,
            creatorApproved = true,
            periodKey = "later-period-1"
        });
        var duplicate = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True(duplicate!.GetProperty("duplicate").GetBoolean());
        Assert.False(duplicate.GetProperty("written").GetBoolean());
        Assert.Equal(1, duplicate.GetProperty("history").GetArrayLength());

        var withheld = await client.PostAsJsonAsync("/api/operations/rotation", new
        {
            pair = 6,
            creatorApproved = false,
            periodKey = "later-period-withheld"
        });
        var second = await withheld.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, withheld.StatusCode);
        Assert.True(second!.GetProperty("written").GetBoolean());
        Assert.Equal(2, second.GetProperty("history").GetArrayLength());
        Assert.True(second.GetProperty("history")[0].GetProperty("creatorApproved").GetBoolean());
        Assert.False(second.GetProperty("history")[1].GetProperty("creatorApproved").GetBoolean());
        Assert.Equal(6 - placed, second.GetProperty("history")[1].GetProperty("openSlots").GetInt32());
        Assert.Equal(slots, second.GetProperty("slotCount").GetInt32());
        Assert.Equal(advertisers, second.GetProperty("advertiserCount").GetInt32());

        var revenue = await client.PostAsJsonAsync("/api/operations/rotation", new
        {
            pair = 6,
            creatorApproved = true,
            periodKey = "later-period-revenue",
            revenueAmount = 12
        });
        Assert.Equal(HttpStatusCode.BadRequest, revenue.StatusCode);
        var filled = await client.PostAsJsonAsync("/api/operations/rotation", new
        {
            pair = 6,
            creatorApproved = true,
            periodKey = "later-period-fill",
            fillOpenSlots = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, filled.StatusCode);
        Assert.Contains("not filled", (await filled.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("error").GetString());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/rotation");
        Assert.Equal(slots, after!.GetProperty("slotCount").GetInt32());
        Assert.Equal(advertisers, after.GetProperty("advertiserCount").GetInt32());
        Assert.Equal(placed, after.GetProperty("advertisers").GetArrayLength());
        Assert.Equal("NOT_SENT", after.GetProperty("delivery").GetString());
    }
}
