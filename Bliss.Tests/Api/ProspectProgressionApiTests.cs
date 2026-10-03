using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class ProspectProgressionApiTests
{
    [Fact]
    public async Task Green_yellow_and_red_leave_the_prospect_on_the_record()
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

        var green = await client.PostAsync("/api/demonstrations/mesa-norte/progression", null);
        var finding = await green.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, green.StatusCode);
        Assert.Equal("GREEN", finding!.GetProperty("progressionSignal").GetString());
        Assert.Contains("road finding", finding.GetProperty("progressionNotice").GetString());
        Assert.Contains("Green does not send", finding.GetProperty("progressionNotice").GetString());
        Assert.False(finding.GetProperty("greenMeansSend").GetBoolean());
        Assert.False(finding.GetProperty("erased").GetBoolean());
        Assert.Equal("OPPORTUNITY_SCORED", finding.GetProperty("prospectState").GetString());
        Assert.Equal("NOT_SENT", finding.GetProperty("delivery").GetString());
        Assert.Equal("Mesa Norte", finding.GetProperty("businessName").GetString());

        var road = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/contact-roads", new
        {
            kind = "company_marketing",
            value = "marketing@example.com",
            sourceUrl = "https://example.com/mesa-norte/contact"
        });
        Assert.Equal(HttpStatusCode.OK, road.StatusCode);
        var policyResponse = await client.PostAsync("/api/demonstrations/mesa-norte/progression", null);
        var policy = await policyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GREEN", policy!.GetProperty("progressionSignal").GetString());
        Assert.Contains("policy check", policy.GetProperty("progressionNotice").GetString());
        Assert.Contains("not permission to send", policy.GetProperty("progressionNotice").GetString());
        Assert.Equal(2, policy.GetProperty("progressions").GetArrayLength());

        var suppressed = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/suppression", new
        {
            reason = "The business asked Alpha to stop"
        });
        Assert.Equal(HttpStatusCode.OK, suppressed.StatusCode);
        var redResponse = await client.PostAsync("/api/demonstrations/mesa-norte/progression", null);
        var red = await redResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RED", red!.GetProperty("progressionSignal").GetString());
        Assert.Contains("prospect record is preserved", red.GetProperty("progressionNotice").GetString());
        Assert.Contains("The business asked Alpha to stop", red.GetProperty("progressionNotice").GetString());
        Assert.Contains("Green does not send", red.GetProperty("progressionNotice").GetString());
        Assert.Equal("Mesa Norte", red.GetProperty("businessName").GetString());
        Assert.Equal("OPPORTUNITY_SCORED", red.GetProperty("prospectState").GetString());
        Assert.True(red.GetProperty("suppressed").GetBoolean());
        Assert.Equal("NOT_SENT", red.GetProperty("delivery").GetString());
        Assert.False(red.GetProperty("erased").GetBoolean());
        Assert.False(red.GetProperty("greenMeansSend").GetBoolean());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/mesa-norte");
        Assert.Equal(beforeEvents + 2, after.GetProperty("events").GetArrayLength());
        Assert.Equal("Mesa Norte", after.GetProperty("businessName").GetString());

        var preserved = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Calle Sur",
            publicSourceUrl = "https://example.com/calle-sur"
        });
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
        var yellowResponse = await client.PostAsync("/api/demonstrations/calle-sur/progression", null);
        var yellow = await yellowResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, yellowResponse.StatusCode);
        Assert.Equal("YELLOW", yellow!.GetProperty("progressionSignal").GetString());
        Assert.Equal("PRESERVED", yellow.GetProperty("prospectState").GetString());
        Assert.Contains("Yellow keeps the record", yellow.GetProperty("progressionNotice").GetString());
        Assert.Equal("Calle Sur", yellow.GetProperty("businessName").GetString());
        Assert.Equal("NOT_SENT", yellow.GetProperty("delivery").GetString());
        Assert.False(yellow.GetProperty("erased").GetBoolean());
    }
}