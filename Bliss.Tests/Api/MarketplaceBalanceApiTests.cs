using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class MarketplaceBalanceApiTests
{
    [Fact]
    public async Task Balance_reads_stored_rows_and_leaves_revenue_unrecorded()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/balance");
        Assert.Equal(0, empty!.GetProperty("advertiserCount").GetInt32());
        Assert.Equal(0, empty.GetProperty("creatorCount").GetInt32());
        Assert.False(empty.GetProperty("censusClaimed").GetBoolean());
        Assert.False(empty.GetProperty("greenMeansSend").GetBoolean());
        Assert.Equal("NOT_SENT", empty.GetProperty("delivery").GetString());
        Assert.Equal("Stored advertisers: 0. Stored creators: 0.", empty.GetProperty("counts").GetString());
        Assert.Equal("Stored advertiser pressure and stored creator pressure are level.", empty.GetProperty("pressure").GetString());
        Assert.Equal("Revenue is not recorded. None was invented.", empty.GetProperty("revenue").GetString());
        Assert.Equal("Inventory is not recorded. None was invented.", empty.GetProperty("inventory").GetString());
        Assert.Contains("not a market census", empty.GetProperty("notice").GetString());
        Assert.Contains("None was invented", empty.GetProperty("notice").GetString());
        Assert.Contains("Green does not send", empty.GetProperty("notice").GetString());
        Assert.Contains("NOT_SENT", empty.GetProperty("notice").GetString());

        await SeedAsync(factory);
        var board = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/balance");
        Assert.Equal(1, board!.GetProperty("advertiserCount").GetInt32());
        Assert.Equal(2, board.GetProperty("creatorCount").GetInt32());
        Assert.Equal("Stored creator pressure is ahead of stored advertiser pressure.", board.GetProperty("pressure").GetString());
        Assert.Equal("Stored advertisers: 1. Stored creators: 2.", board.GetProperty("counts").GetString());
        Assert.Equal("Harbor Fixture", board.GetProperty("advertisers")[0].GetString());
        Assert.Equal("Creator A", board.GetProperty("creators")[0].GetString());
        Assert.Equal("Creator B", board.GetProperty("creators")[1].GetString());
        Assert.Equal("Revenue is not recorded. None was invented.", board.GetProperty("revenue").GetString());
        Assert.Equal("Inventory is not recorded. None was invented.", board.GetProperty("inventory").GetString());
        Assert.False(board.GetProperty("censusClaimed").GetBoolean());
        Assert.False(board.GetProperty("greenMeansSend").GetBoolean());
        Assert.Equal("NOT_SENT", board.GetProperty("delivery").GetString());

        var again = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/balance");
        Assert.Equal(1, again!.GetProperty("advertiserCount").GetInt32());
        Assert.Equal(2, again.GetProperty("creatorCount").GetInt32());
        var library = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/library");
        Assert.Equal(0, library!.GetProperty("demonstrations").GetArrayLength());
    }

    private static async Task SeedAsync(BlissApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var now = DateTime.UtcNow;
        database.Advertisers.Add(new Advertiser
        {
            Id = Guid.NewGuid(),
            Name = "Harbor Fixture",
            CreatedAt = now
        });
        database.Creators.Add(new Creator
        {
            Id = Guid.NewGuid(),
            Name = "Creator B",
            CreatedAt = now,
            UpdatedAt = now
        });
        database.Creators.Add(new Creator
        {
            Id = Guid.NewGuid(),
            Name = "Creator A",
            CreatedAt = now,
            UpdatedAt = now
        });
        await database.SaveChangesAsync();
    }
}
