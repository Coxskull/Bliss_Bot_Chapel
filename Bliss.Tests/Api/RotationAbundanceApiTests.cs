using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class RotationAbundanceApiTests
{
    [Fact]
    public async Task A_rotation_leaves_open_slots_and_does_not_rewrite_inventory()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var preview = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/rotation");
        Assert.Equal(0, preview!.GetProperty("slotCount").GetInt32());
        Assert.Equal(0, preview.GetProperty("placedAdvertisers").GetInt32());
        Assert.Equal("A rotation has not been read.", preview.GetProperty("result").GetString());
        Assert.False(preview.GetProperty("slotsChanged").GetBoolean());
        Assert.False(preview.GetProperty("greenMeansSend").GetBoolean());
        Assert.Equal("NOT_SENT", preview.GetProperty("delivery").GetString());
        Assert.Contains("not required for every theoretical slot", preview.GetProperty("abundance").GetString());
        Assert.Contains("None was invented", preview.GetProperty("period").GetString());
        Assert.Contains("Green does not send", preview.GetProperty("notice").GetString());

        var empty = await Post(client, 6, true);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        var open = await empty.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, open!.GetProperty("theoreticalSlots").GetInt32());
        Assert.Equal(0, open.GetProperty("placedAdvertisers").GetInt32());
        Assert.Equal(6, open.GetProperty("openSlots").GetInt32());
        Assert.True(open.GetProperty("accepted").GetBoolean());
        Assert.Equal(0, open.GetProperty("advertisers").GetArrayLength());
        Assert.Contains("Open slots remain", open.GetProperty("result").GetString());
        Assert.Contains("None was invented", open.GetProperty("abundance").GetString());
        Assert.False(open.GetProperty("slotsChanged").GetBoolean());
        Assert.Equal("NOT_SENT", open.GetProperty("delivery").GetString());

        await SeedAsync(factory, "Harbor Fixture", "Creator Side", "Sunrise Fixture");
        var overflow = await Post(client, 2, true);
        var error = await overflow.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, overflow.StatusCode);
        Assert.Contains("None is invented", error!.GetProperty("error").GetString());
        Assert.Equal("NOT_SENT", error.GetProperty("delivery").GetString());
        Assert.False(error.GetProperty("slotsChanged").GetBoolean());

        var accepted = await Post(client, 6, true);
        var board = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(6, board!.GetProperty("theoreticalSlots").GetInt32());
        Assert.Equal(3, board.GetProperty("placedAdvertisers").GetInt32());
        Assert.Equal(3, board.GetProperty("openSlots").GetInt32());
        Assert.True(board.GetProperty("accepted").GetBoolean());
        Assert.Equal("Creator Side", board.GetProperty("advertisers")[0].GetString());
        Assert.Equal("Harbor Fixture", board.GetProperty("advertisers")[1].GetString());
        Assert.Equal("Sunrise Fixture", board.GetProperty("advertisers")[2].GetString());
        Assert.Equal(3, board.GetProperty("advertisers").GetArrayLength());
        Assert.Contains("not required for every theoretical slot", board.GetProperty("abundance").GetString());
        Assert.False(board.GetProperty("slotsChanged").GetBoolean());

        var withheld = await Post(client, 4, false);
        var held = await withheld.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(held!.GetProperty("accepted").GetBoolean());
        Assert.Equal(1, held.GetProperty("openSlots").GetInt32());
        Assert.Contains("stays withheld", held.GetProperty("result").GetString());

        var missing = await client.PostAsJsonAsync("/api/demonstrations/rotation", new { pair = 6 });
        var missingBody = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("Creator approval is required", missingBody!.GetProperty("error").GetString());
        Assert.Equal("NOT_SENT", missingBody.GetProperty("delivery").GetString());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/rotation");
        Assert.Equal(0, after!.GetProperty("slotCount").GetInt32());
        Assert.Equal(3, after.GetProperty("placedAdvertisers").GetInt32());
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(0, await database.AdInventorySlots.CountAsync());
        Assert.Equal(3, await database.Advertisers.CountAsync());
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, int pair, bool? creatorApproved) =>
        client.PostAsJsonAsync("/api/demonstrations/rotation", new { pair, creatorApproved });

    private static async Task SeedAsync(BlissApiFactory factory, params string[] names)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var now = DateTime.UtcNow;
        foreach (var name in names)
        {
            database.Advertisers.Add(new Advertiser
            {
                Id = Guid.NewGuid(),
                Name = name,
                CreatedAt = now
            });
        }

        await database.SaveChangesAsync();
    }
}
