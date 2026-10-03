using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Api;

public sealed class ConversationLaboratoryApiTests
{
    [Fact]
    public async Task The_laboratory_passes_and_does_not_write_the_prospect()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var asked = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/messages", new
        {
            text = "How much does this cost?"
        });
        var before = await asked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);
        var beforeCount = before!.GetProperty("messages").GetArrayLength();
        var beforeReply = before.GetProperty("reply").GetString();

        var response = await client.GetAsync("/api/demonstrations/conversation-laboratory");
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(report!.GetProperty("passed").GetBoolean());
        Assert.Equal(ConversationLaboratory.Scenarios.Count, report.GetProperty("scenarioCount").GetInt32());
        Assert.Equal(report.GetProperty("scenarioCount").GetInt32(), report.GetProperty("passedCount").GetInt32());
        Assert.Equal(0, report.GetProperty("aiCalls").GetInt32());
        Assert.Equal("NOT_SENT", report.GetProperty("delivery").GetString());
        Assert.Contains("Production conversation was not changed", report.GetProperty("notice").GetString());
        Assert.Equal("Ask Alpha", report.GetProperty("voice").GetString());

        var price = report.GetProperty("scenarios").EnumerateArray().Single(item => item.GetProperty("id").GetString() == "unverified-price");
        Assert.Contains("cannot invent a price", price.GetProperty("reply").GetString());
        Assert.Contains("cannot invent a price", beforeReply);

        var afterResponse = await client.GetAsync("/api/demonstrations/mesa-norte");
        var after = await afterResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(beforeCount, after!.GetProperty("messages").GetArrayLength());
        Assert.Equal("OPPORTUNITY_SCORED", after.GetProperty("prospectState").GetString());
        Assert.Equal("NOT_SENT", after.GetProperty("delivery").GetString());
        Assert.DoesNotContain(
            after.GetProperty("messages").EnumerateArray(),
            item => (item.GetProperty("text").GetString() ?? "").Contains("laboratory"));
    }
}
