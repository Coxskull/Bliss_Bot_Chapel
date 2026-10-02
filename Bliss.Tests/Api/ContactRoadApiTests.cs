using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class ContactRoadApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public ContactRoadApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_roads_stay_ineligible_and_suppression_sends_nothing()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = "https://example.com/puerto-azul"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var rejected = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/contact-roads", new
        {
            kind = "company_marketing",
            value = "marketing@example.com",
            sourceUrl = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(rejectedBody.GetProperty("outreachEligible").GetBoolean());
        Assert.Contains("will not store a road", rejectedBody.GetProperty("error").GetString());

        var empty = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/puerto-azul");
        Assert.Empty(empty.GetProperty("contactRoads").EnumerateArray());
        Assert.Equal("NOT_SENT", empty.GetProperty("delivery").GetString());

        var first = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/contact-roads", new
        {
            kind = "company_marketing",
            value = "marketing@example.com",
            sourceUrl = "https://example.com/puerto-azul/contact"
        });
        var page = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("PRESERVED", page!.GetProperty("prospectState").GetString());
        Assert.False(page.GetProperty("outreachEligible").GetBoolean());
        Assert.Equal("NOT_SENT", page.GetProperty("delivery").GetString());
        Assert.Equal("UNVERIFIED", page.GetProperty("decisionMakerStatus").GetString());
        Assert.Single(page.GetProperty("contactRoads").EnumerateArray());
        Assert.Equal("DISCOVERED", page.GetProperty("contactRoads")[0].GetProperty("state").GetString());
        Assert.False(page.GetProperty("contactRoads")[0].GetProperty("outreachEligible").GetBoolean());

        var second = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/contact-roads", new
        {
            kind = "published_messaging",
            value = "+50760001111",
            sourceUrl = "https://example.com/puerto-azul/contact"
        });
        var two = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, two!.GetProperty("contactRoads").GetArrayLength());

        var duplicate = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/contact-roads", new
        {
            kind = "company_marketing",
            value = "marketing@example.com",
            sourceUrl = "https://example.com/puerto-azul/contact"
        });
        var stillTwo = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, stillTwo!.GetProperty("contactRoads").GetArrayLength());

        var blank = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/suppression", new { reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var suppressed = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/suppression", new
        {
            reason = "The business asked Alpha to stop"
        });
        var stopped = await suppressed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, suppressed.StatusCode);
        Assert.True(stopped!.GetProperty("suppressed").GetBoolean());
        Assert.False(stopped.GetProperty("outreachEligible").GetBoolean());
        Assert.Equal("NOT_SENT", stopped.GetProperty("delivery").GetString());
        Assert.All(stopped.GetProperty("contactRoads").EnumerateArray(), road =>
        {
            Assert.Equal("SUPPRESSED", road.GetProperty("state").GetString());
            Assert.False(road.GetProperty("outreachEligible").GetBoolean());
        });

        var during = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/contact-roads", new
        {
            kind = "general_company",
            value = "hello@example.com",
            sourceUrl = "https://example.com/puerto-azul/contact"
        });
        var held = await during.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, held!.GetProperty("contactRoads").GetArrayLength());
        Assert.Equal("SUPPRESSED", held.GetProperty("contactRoads")[2].GetProperty("state").GetString());
        Assert.Equal("NOT_SENT", held.GetProperty("delivery").GetString());

        var reply = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/messages", new
        {
            text = "Can you email this road?"
        });
        var conversation = await reply.Content.ReadFromJsonAsync<JsonElement>();
        var answer = conversation!.GetProperty("reply").GetString();
        Assert.Contains("not permission to send", answer);
        Assert.Contains("NOT_SENT", answer);
        Assert.Contains("suppressed", answer);
    }
}
