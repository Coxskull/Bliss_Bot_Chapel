using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class ScaleProofApiTests
{
    [Fact]
    public async Task The_ladder_is_measured_without_changing_the_prospect()
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
        var before = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/mesa-norte");
        var beforeEvents = before.GetProperty("events").GetArrayLength();
        var beforeState = before.GetProperty("prospectState").GetString();

        var response = await client.GetAsync("/api/demonstrations/scale-proof");
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(report!.GetProperty("passed").GetBoolean());
        Assert.False(report.GetProperty("productionChanged").GetBoolean());
        Assert.Equal(0, report.GetProperty("aiCalls").GetInt32());
        Assert.Equal("NOT_SENT", report.GetProperty("delivery").GetString());
        Assert.Contains("not a claim", report.GetProperty("notice").GetString());
        Assert.Contains("None was invented", report.GetProperty("cost").GetString());
        Assert.Contains("not claimed", report.GetProperty("factoryTarget").GetString());
        var rungs = report.GetProperty("rungs").EnumerateArray().ToList();
        Assert.Equal(3, rungs.Count);
        Assert.Equal(new[] { 100, 1000, 10000 }, rungs.Select(item => item.GetProperty("target").GetInt32()));
        Assert.All(rungs, item =>
        {
            Assert.Equal(item.GetProperty("target").GetInt32(), item.GetProperty("measured").GetInt32());
            Assert.True(item.GetProperty("passed").GetBoolean());
            Assert.False(item.GetProperty("claimed").GetBoolean());
        });

        var after = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/mesa-norte");
        Assert.Equal("Mesa Norte", after.GetProperty("businessName").GetString());
        Assert.Equal(beforeState, after.GetProperty("prospectState").GetString());
        Assert.Equal(beforeEvents, after.GetProperty("events").GetArrayLength());
        Assert.Equal("NOT_SENT", after.GetProperty("delivery").GetString());
    }
}
