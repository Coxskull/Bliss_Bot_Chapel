using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class CreativeAcademyApiTests
{
    [Fact]
    public async Task The_curriculum_is_judged_once_and_does_not_send()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/academy");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.Equal(0, before.GetProperty("modelCalls").GetInt32());
        Assert.False(before.GetProperty("campaignReady").GetBoolean());
        Assert.Equal(0, before.GetProperty("lessons").GetArrayLength());

        var stored = await client.PostAsJsonAsync(
            "/api/operations/academy/curriculum",
            new { curriculumKey = "patisserie-curriculum-1" });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.Equal(4, first.GetProperty("lessons").GetArrayLength());

        var lamour = Lesson(first, "lamour-sucre");
        var solara = Lesson(first, "solara");
        var belmonte = Lesson(first, "belmonte");
        var prototype = Lesson(first, "maison-fleur");
        Assert.Equal("REVISE", lamour.GetProperty("status").GetString());
        Assert.Equal("REVISE", solara.GetProperty("status").GetString());
        Assert.Equal("WITHHELD", belmonte.GetProperty("status").GetString());
        Assert.Equal("REFERENCE", prototype.GetProperty("status").GetString());
        Assert.Equal(0, lamour.GetProperty("modelCalls").GetInt32());
        Assert.False(lamour.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", lamour.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync(
            "/api/operations/academy/curriculum",
            new { curriculumKey = "patisserie-curriculum-1" });
        var second = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.Equal(4, second.GetProperty("lessons").GetArrayLength());

        var visual = await client.PostAsJsonAsync(
            "/api/operations/academy/visual",
            new { lessonKey = "belmonte", visualBenchmarkMet = true });
        var judged = await visual.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, visual.StatusCode);
        Assert.Equal("PASS", Lesson(judged!, "belmonte").GetProperty("status").GetString());
        Assert.Equal("REVISE", Lesson(judged, "lamour-sucre").GetProperty("status").GetString());

        var campaign = await client.PostAsJsonAsync(
            "/api/operations/academy/visual",
            new { lessonKey = "belmonte", visualBenchmarkMet = true, campaignReady = true });
        var refused = await campaign.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, campaign.StatusCode);
        Assert.Equal("NOT_SENT", refused!.GetProperty("delivery").GetString());
        Assert.False(refused.GetProperty("campaignReady").GetBoolean());
        Assert.Contains("Campaign ready is refused", refused.GetProperty("error").GetString());
    }

    private static JsonElement Lesson(JsonElement board, string key)
    {
        foreach (var lesson in board.GetProperty("lessons").EnumerateArray())
        {
            if (lesson.GetProperty("lessonKey").GetString() == key)
            {
                return lesson;
            }
        }

        throw new InvalidOperationException("Missing lesson " + key);
    }
}
