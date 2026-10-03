using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class LearningLedgerApiTests
{
    [Fact]
    public async Task A_research_note_stays_with_its_prospect_and_does_not_change_production()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = "https://example.com/casa-verde"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        })).StatusCode);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/discovery");
        var casaBefore = before!.GetProperty("prospects").EnumerateArray()
            .Single(item => item.GetProperty("businessName").GetString() == "Casa Verde");
        var stateBefore = casaBefore.GetProperty("state").GetString();

        var empty = await client.GetFromJsonAsync<JsonElement>("/api/operations/learning?prospect=casa-verde");
        Assert.Equal("NOT_SENT", empty!.GetProperty("delivery").GetString());
        Assert.False(empty.GetProperty("greenMeansSend").GetBoolean());
        Assert.False(empty.GetProperty("productionChanged").GetBoolean());
        Assert.False(empty.GetProperty("behaviorChanged").GetBoolean());
        Assert.False(empty.GetProperty("authorizedTraffic").GetBoolean());
        Assert.Equal(0, empty.GetProperty("modelCalls").GetInt32());
        Assert.True(empty.GetProperty("laboratoryPassed").GetBoolean());
        Assert.Equal(empty.GetProperty("laboratoryScenarios").GetInt32(), empty.GetProperty("laboratoryPassedCount").GetInt32());
        Assert.Contains("not a traffic count", empty.GetProperty("notice").GetString());
        Assert.Contains("Seven grooming models are not configured", empty.GetProperty("notice").GetString());
        Assert.Equal(0, empty.GetProperty("notes").GetArrayLength());

        var appended = await client.PostAsJsonAsync("/api/operations/learning", new
        {
            prospectSlug = "casa-verde",
            idempotencyKey = "learning-casa-1"
        });
        var first = await appended.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, appended.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("duplicate").GetBoolean());
        Assert.False(first.GetProperty("productionChanged").GetBoolean());
        Assert.Equal(stateBefore, first.GetProperty("prospectState").GetString());
        var note = first.GetProperty("notes")[0];
        Assert.Equal("casa-verde", note.GetProperty("prospectSlug").GetString());
        Assert.Contains("No authorized traffic is on file", note.GetProperty("body").GetString());
        Assert.True(note.GetProperty("laboratoryGraduated").GetBoolean());
        Assert.False(note.GetProperty("authorizedTraffic").GetBoolean());
        Assert.False(note.GetProperty("productionChanged").GetBoolean());
        Assert.False(note.GetProperty("behaviorChanged").GetBoolean());
        Assert.Equal(0, note.GetProperty("modelCalls").GetInt32());
        Assert.Equal("NOT_SENT", note.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/learning", new
        {
            prospectSlug = "casa-verde",
            idempotencyKey = "learning-casa-1"
        });
        var duplicate = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True(duplicate!.GetProperty("duplicate").GetBoolean());
        Assert.False(duplicate.GetProperty("written").GetBoolean());
        Assert.Equal(1, duplicate.GetProperty("notes").GetArrayLength());

        var other = await client.GetFromJsonAsync<JsonElement>("/api/operations/learning?prospect=mesa-norte");
        Assert.Equal("mesa-norte", other!.GetProperty("prospect").GetString());
        Assert.Equal(0, other.GetProperty("notes").GetArrayLength());
        Assert.False(other.GetProperty("authorizedTraffic").GetBoolean());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/discovery");
        var casaAfter = after!.GetProperty("prospects").EnumerateArray()
            .Single(item => item.GetProperty("businessName").GetString() == "Casa Verde");
        Assert.Equal(stateBefore, casaAfter.GetProperty("state").GetString());

        var production = await client.PostAsJsonAsync("/api/operations/learning", new
        {
            prospectSlug = "casa-verde",
            idempotencyKey = "learning-casa-2",
            applyToProduction = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, production.StatusCode);
        var refused = await production.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("does not control production", refused!.GetProperty("error").GetString());
        Assert.False(refused.GetProperty("productionChanged").GetBoolean());
        Assert.Equal(0, refused.GetProperty("modelCalls").GetInt32());

        var traffic = await client.PostAsJsonAsync("/api/operations/learning", new
        {
            prospectSlug = "casa-verde",
            idempotencyKey = "learning-casa-3",
            authorizedTraffic = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, traffic.StatusCode);
        var invented = await client.PostAsJsonAsync("/api/operations/learning", new
        {
            prospectSlug = "casa-verde",
            idempotencyKey = "learning-casa-4",
            engagementCount = 4
        });
        Assert.Equal(HttpStatusCode.BadRequest, invented.StatusCode);
        Assert.Contains("None was invented", (await invented.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("error").GetString());
    }
}
