using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class ClosedModelsApiTests
{
    [Fact]
    public async Task A_closed_reading_matches_the_laboratory_and_does_not_configure_a_model()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var laboratory = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/conversation-laboratory");
        var learning = await client.GetFromJsonAsync<JsonElement>("/api/operations/learning");
        var scenarios = laboratory!.GetProperty("scenarioCount").GetInt32();
        var passed = laboratory.GetProperty("passedCount").GetInt32();
        var learningNotice = learning!.GetProperty("notice").GetString();

        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/models");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.Equal(0, before.GetProperty("configuredModels").GetInt32());
        Assert.Equal(0, before.GetProperty("modelCalls").GetInt32());
        Assert.False(before.GetProperty("modelsConfigured").GetBoolean());
        Assert.False(before.GetProperty("authorizedTraffic").GetBoolean());
        Assert.False(before.GetProperty("productionChanged").GetBoolean());
        Assert.Equal(scenarios, before.GetProperty("scenarioCount").GetInt32());
        Assert.Equal(passed, before.GetProperty("passedCount").GetInt32());
        Assert.Equal(0, before.GetProperty("history").GetArrayLength());
        Assert.Contains("Seven grooming models are not configured", before.GetProperty("learningNotice").GetString());
        Assert.Equal(learningNotice, before.GetProperty("learningNotice").GetString());
        Assert.Contains("not a traffic count", before.GetProperty("notice").GetString());

        var stored = await client.PostAsJsonAsync("/api/operations/models", new { readingKey = "models-reading-1" });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.Equal(0, first.GetProperty("configuredModels").GetInt32());
        var row = first.GetProperty("history")[0];
        Assert.Equal("models-reading-1", row.GetProperty("readingKey").GetString());
        Assert.Equal(0, row.GetProperty("configuredModels").GetInt32());
        Assert.Equal(0, row.GetProperty("modelCalls").GetInt32());
        Assert.Equal(scenarios, row.GetProperty("scenarioCount").GetInt32());
        Assert.Equal(passed, row.GetProperty("passedCount").GetInt32());
        Assert.False(row.GetProperty("modelsConfigured").GetBoolean());
        Assert.Equal("NOT_SENT", row.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/models", new { readingKey = "models-reading-1" });
        var second = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.Equal(1, second.GetProperty("history").GetArrayLength());

        var configured = await client.PostAsJsonAsync("/api/operations/models", new { readingKey = "models-reading-1", configureModel = true });
        Assert.Equal(HttpStatusCode.BadRequest, configured.StatusCode);
        var refused = await configured.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not configured", refused!.GetProperty("error").GetString());
        Assert.Equal(0, refused.GetProperty("configuredModels").GetInt32());
        Assert.False(refused.GetProperty("modelsConfigured").GetBoolean());

        var afterLearning = await client.GetFromJsonAsync<JsonElement>("/api/operations/learning");
        Assert.Equal(learningNotice, afterLearning!.GetProperty("notice").GetString());
        Assert.Equal(0, afterLearning.GetProperty("modelCalls").GetInt32());
        var after = await client.GetFromJsonAsync<JsonElement>("/api/operations/models");
        Assert.Equal(1, after!.GetProperty("history").GetArrayLength());
        Assert.Equal(0, after.GetProperty("modelCalls").GetInt32());
    }
}
