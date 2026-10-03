using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class CreativeInventoryApiTests
{
    [Fact]
    public async Task A_pair_reading_does_not_rewrite_stored_slots()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/creative-inventory");
        Assert.Equal(0, before!.GetProperty("slotCount").GetInt32());
        Assert.False(before.GetProperty("slotsChanged").GetBoolean());
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.Equal("NOT_SENT", before.GetProperty("delivery").GetString());
        Assert.Contains("not a balanced pair", before.GetProperty("stored").GetString());
        Assert.Contains("None was invented", before.GetProperty("stored").GetString());
        Assert.Equal("Rotation is not configured. None was invented.", before.GetProperty("rotation").GetString());
        Assert.Contains("A two-over-four stack is not the default", before.GetProperty("notice").GetString());
        Assert.Contains("Green does not send", before.GetProperty("notice").GetString());
        Assert.Contains("NOT_SENT", before.GetProperty("notice").GetString());

        var accepted = await client.PostAsJsonAsync("/api/demonstrations/creative-inventory", new
        {
            pair = 4,
            creatorApproved = true,
            stack = false
        });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var decision = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(decision!.GetProperty("accepted").GetBoolean());
        Assert.False(decision.GetProperty("stackRefused").GetBoolean());
        Assert.False(decision.GetProperty("slotsChanged").GetBoolean());
        Assert.Equal(4, decision.GetProperty("pair").GetInt32());
        Assert.Contains("The balanced pair of 4 is accepted", decision.GetProperty("result").GetString());
        Assert.Contains("Creator approval governs this density", decision.GetProperty("result").GetString());
        Assert.Equal("NOT_SENT", decision.GetProperty("delivery").GetString());
        Assert.False(decision.GetProperty("greenMeansSend").GetBoolean());

        var withheld = await client.PostAsJsonAsync("/api/demonstrations/creative-inventory", new
        {
            pair = 2,
            creatorApproved = false,
            stack = false
        });
        var held = await withheld.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(held!.GetProperty("accepted").GetBoolean());
        Assert.Contains("stays withheld", held.GetProperty("result").GetString());

        var stack = await client.PostAsJsonAsync("/api/demonstrations/creative-inventory", new { stack = true });
        var refused = await stack.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stack.StatusCode);
        Assert.True(refused!.GetProperty("stackRefused").GetBoolean());
        Assert.False(refused.GetProperty("accepted").GetBoolean());
        Assert.Contains("The two-over-four stack is refused", refused.GetProperty("result").GetString());
        Assert.Contains("None was invented", refused.GetProperty("result").GetString());
        Assert.False(refused.GetProperty("slotsChanged").GetBoolean());

        var missing = await client.PostAsJsonAsync("/api/demonstrations/creative-inventory", new
        {
            pair = 8,
            creatorApproved = true,
            stack = false
        });
        var error = await missing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("None is invented", error!.GetProperty("error").GetString());
        Assert.Equal("NOT_SENT", error.GetProperty("delivery").GetString());
        Assert.False(error.GetProperty("slotsChanged").GetBoolean());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/creative-inventory");
        Assert.Equal(0, after!.GetProperty("slotCount").GetInt32());
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Equal(0, await database.AdInventorySlots.CountAsync());
    }
}
