using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class MarketplaceHandoffApiTests
{
    [Fact]
    public async Task A_qualified_advertiser_is_handed_once_and_stays_inside_its_tenant()
    {
        await using var factory = new BlissApiFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            await new Phase1DataSeeder(db).SeedAsync();
            await new Phase2DataSeeder(db).SeedAsync();
        }

        var client = factory.CreateClient();
        var casa = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = "https://example.com/casa-verde"
        });
        Assert.Equal(HttpStatusCode.OK, casa.StatusCode);
        var mesa = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, mesa.StatusCode);
        var puerto = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = "https://example.com/puerto-azul"
        });
        Assert.Equal(HttpStatusCode.OK, puerto.StatusCode);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/bliss/matches");
        var matchCount = before!.GetArrayLength();
        var board = await client.GetFromJsonAsync<JsonElement>("/api/operations/handoff?tenant=casa-verde");
        Assert.Equal("NOT_SENT", board!.GetProperty("delivery").GetString());
        Assert.False(board.GetProperty("greenMeansSend").GetBoolean());
        Assert.False(board.GetProperty("winClaimed").GetBoolean());
        Assert.Equal("DeterministicRuleEvaluator", board.GetProperty("evaluator").GetString());
        Assert.Equal("Phase 2 Explicit Rules", board.GetProperty("ruleName").GetString());
        Assert.Equal(0, board.GetProperty("handoffs").GetArrayLength());
        Assert.Contains(board.GetProperty("advertisers").EnumerateArray(), item =>
            item.GetProperty("businessName").GetString() == "Puerto Azul" && item.GetProperty("qualified").GetBoolean() == false);

        var handed = await client.PostAsJsonAsync("/api/operations/handoff", new
        {
            tenant = "casa-verde",
            creatorId = Phase2DataSeeder.BrazilCreatorId
        });
        var first = await handed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, handed.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("duplicate").GetBoolean());
        Assert.True(first.GetProperty("evaluatorInvoked").GetBoolean());
        Assert.Equal("INELIGIBLE", first.GetProperty("matchStatus").GetString());
        Assert.False(first.GetProperty("winClaimed").GetBoolean());
        Assert.Equal("NOT_SENT", first.GetProperty("delivery").GetString());
        Assert.Contains("not a win", first.GetProperty("notice").GetString());
        Assert.Contains("not converted into a code", first.GetProperty("notice").GetString());
        Assert.Equal("https://example.com/casa-verde", first.GetProperty("result").GetProperty("sourceUrl").GetString());
        Assert.Equal("Test Creator Brazil", first.GetProperty("result").GetProperty("creatorName").GetString());
        Assert.Equal(1, first.GetProperty("handoffs").GetArrayLength());

        var again = await client.PostAsJsonAsync("/api/operations/handoff", new
        {
            tenant = "casa-verde",
            creatorId = Phase2DataSeeder.BrazilCreatorId
        });
        var duplicate = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True(duplicate!.GetProperty("duplicate").GetBoolean());
        Assert.False(duplicate.GetProperty("written").GetBoolean());
        Assert.Equal("INELIGIBLE", duplicate.GetProperty("matchStatus").GetString());
        Assert.Equal(1, duplicate.GetProperty("handoffs").GetArrayLength());

        var other = await client.GetFromJsonAsync<JsonElement>("/api/operations/handoff?tenant=mesa-norte");
        Assert.Equal(0, other!.GetProperty("handoffs").GetArrayLength());
        Assert.DoesNotContain(other.GetProperty("handoffs").EnumerateArray(), item =>
            item.GetProperty("businessName").GetString() == "Casa Verde");

        var withheld = await client.PostAsJsonAsync("/api/operations/handoff", new
        {
            tenant = "puerto-azul",
            creatorId = Phase2DataSeeder.BrazilCreatorId
        });
        var kept = await withheld.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, withheld.StatusCode);
        Assert.False(kept!.GetProperty("written").GetBoolean());
        Assert.False(kept.GetProperty("evaluatorInvoked").GetBoolean());
        Assert.Contains("not qualified", kept.GetProperty("notice").GetString());
        Assert.Equal(0, kept.GetProperty("handoffs").GetArrayLength());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/bliss/matches");
        Assert.Equal(matchCount, after!.GetArrayLength());
        Assert.Equal("NOT_SENT", (await client.GetFromJsonAsync<JsonElement>("/api/operations/handoff?tenant=casa-verde"))!
            .GetProperty("delivery").GetString());
    }
}
