using System.Net;
using Bliss.Domain.Demonstrations;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class EligibleDeliveryApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public EligibleDeliveryApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Preview_adapter_marks_one_road_eligible_and_transmits_nothing()
    {
        var client = _factory.CreateClient();
        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Mesa Norte",
            publicSourceUrl = "https://example.com/mesa-norte"
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var missing = await Decide(client, "mesa-norte", "", DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.Contains("stored contact road", missing.GetProperty("deliveryNotice").GetString());
        Assert.False(missing.GetProperty("outreachEligible").GetBoolean());
        Assert.Equal("NOT_SENT", missing.GetProperty("delivery").GetString());
        Assert.Equal("NOT_SENT", missing.GetProperty("transmission").GetString());

        var road = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/contact-roads", new
        {
            kind = "company_marketing",
            value = "marketing@example.com",
            sourceUrl = "https://example.com/mesa-norte/contact"
        });
        var recorded = await road.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, road.StatusCode);
        Assert.False(recorded!.GetProperty("outreachEligible").GetBoolean());
        Assert.False(recorded.GetProperty("contactRoads")[0].GetProperty("outreachEligible").GetBoolean());
        var roadId = recorded.GetProperty("contactRoads")[0].GetProperty("id").GetString()!;

        var smtp = await Decide(client, "mesa-norte", roadId, DeliveryPolicy.PreviewPolicy, "smtp", DeliveryPolicy.PreviewAuthorization);
        Assert.Contains("adapter is not approved", smtp.GetProperty("deliveryNotice").GetString());
        Assert.False(smtp.GetProperty("outreachEligible").GetBoolean());
        Assert.Equal("NOT_SENT", smtp.GetProperty("delivery").GetString());

        var send = await Decide(client, "mesa-norte", roadId, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, "Send it now");
        Assert.Contains("does not authorize the preview adapter", send.GetProperty("deliveryNotice").GetString());
        Assert.False(send.GetProperty("contactRoads")[0].GetProperty("outreachEligible").GetBoolean());

        var prepared = await Decide(client, "mesa-norte", roadId, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.True(prepared.GetProperty("outreachEligible").GetBoolean());
        Assert.True(prepared.GetProperty("contactRoads")[0].GetProperty("outreachEligible").GetBoolean());
        Assert.Equal("DISCOVERED", prepared.GetProperty("contactRoads")[0].GetProperty("state").GetString());
        Assert.Equal("NOT_SENT", prepared.GetProperty("delivery").GetString());
        Assert.Equal("NOT_SENT", prepared.GetProperty("transmission").GetString());
        Assert.Contains("prepared a copy", prepared.GetProperty("deliveryNotice").GetString());
        Assert.Contains("did not transmit", prepared.GetProperty("deliveryDecisions").EnumerateArray().Last().GetProperty("preparedCopy").GetString());
        Assert.Equal("ELIGIBLE", prepared.GetProperty("deliveryDecisions").EnumerateArray().Last().GetProperty("eligibility").GetString());
        Assert.Contains("not permission to send", prepared.GetProperty("contactRoads")[0].GetProperty("permission").GetString());

        var again = await Decide(client, "mesa-norte", roadId, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.Equal(1, again.GetProperty("deliveryDecisions").EnumerateArray().Count(item => item.GetProperty("eligibility").GetString() == "ELIGIBLE"));
        Assert.Equal("NOT_SENT", again.GetProperty("delivery").GetString());

        var reply = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/messages", new { text = "Can you email this road?" });
        var conversation = await reply.Content.ReadFromJsonAsync<JsonElement>();
        var answer = conversation!.GetProperty("reply").GetString()!;
        Assert.Contains("not permission to send", answer);
        Assert.Contains("preview adapter can prepare a copy", answer);
        Assert.Contains("NOT_SENT", answer);
        Assert.DoesNotContain("suppressed", answer);

        var sent = await client.PostAsJsonAsync("/api/demonstrations/mesa-norte/events", new { kind = "SENT" });
        var refused = await sent.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, sent.StatusCode);
        Assert.Contains("Nothing was sent", refused!.GetProperty("error").GetString());

        var preserved = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = "https://example.com/puerto-azul"
        });
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
        var azulRoad = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/contact-roads", new
        {
            kind = "company_marketing",
            value = "marketing@example.com",
            sourceUrl = "https://example.com/puerto-azul/contact"
        });
        var azul = await azulRoad.Content.ReadFromJsonAsync<JsonElement>();
        var azulId = azul!.GetProperty("contactRoads")[0].GetProperty("id").GetString();
        var stopped = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/suppression", new
        {
            reason = "The business asked Alpha to stop"
        });
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        var blocked = await Decide(client, "puerto-azul", azulId!, DeliveryPolicy.PreviewPolicy, DeliveryPolicy.PreviewAdapter, DeliveryPolicy.PreviewAuthorization);
        Assert.Contains("Suppression comes before the adapter", blocked.GetProperty("deliveryNotice").GetString());
        Assert.False(blocked.GetProperty("outreachEligible").GetBoolean());
        Assert.Equal("NOT_SENT", blocked.GetProperty("delivery").GetString());
        Assert.All(blocked.GetProperty("contactRoads").EnumerateArray(), item =>
            Assert.False(item.GetProperty("outreachEligible").GetBoolean()));
    }

    private static async Task<JsonElement> Decide(HttpClient client, string slug, string roadId, string policy, string adapter, string authorization)
    {
        var response = await client.PostAsJsonAsync("/api/demonstrations/" + slug + "/delivery", new
        {
            roadId,
            policy,
            adapter,
            authorization
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body!;
    }
}
