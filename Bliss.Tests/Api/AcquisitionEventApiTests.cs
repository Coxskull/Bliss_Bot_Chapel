using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class AcquisitionEventApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public AcquisitionEventApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task An_opinion_is_refused_and_a_page_open_is_stored()
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
        var prospect = await created.Content.ReadFromJsonAsync<JsonElement>();
        var recorded = prospect!.GetProperty("events");
        Assert.Equal(1, recorded.GetArrayLength());
        Assert.Equal("PROSPECT_RECORDED", recorded[0].GetProperty("kind").GetString());
        Assert.Equal(0, recorded[0].GetProperty("aiCalls").GetInt32());
        Assert.Equal("NOT_SENT", recorded[0].GetProperty("delivery").GetString());
        Assert.Contains("public source", recorded[0].GetProperty("observation").GetString());

        var opinion = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/events", new { kind = "INTERESTED" });
        var opinionBody = await opinion.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, opinion.StatusCode);
        Assert.Contains("will not store an opinion", opinionBody!.GetProperty("error").GetString());

        var named = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/events", new
        {
            kind = "PAGE_OPENED",
            watcher = "Ana Ruiz"
        });
        var namedBody = await named.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, named.StatusCode);
        Assert.Contains("does not name who watched", namedBody!.GetProperty("error").GetString());

        var sent = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/events", new { kind = "SENT" });
        var sentBody = await sent.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, sent.StatusCode);
        Assert.Contains("Nothing was sent", sentBody!.GetProperty("error").GetString());

        var opened = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/events", new { kind = "PAGE_OPENED" });
        var page = await opened.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal("PAGE_OPENED", page!.GetProperty("kind").GetString());
        Assert.Equal(0, page.GetProperty("aiCalls").GetInt32());
        Assert.Equal("NOT_SENT", page.GetProperty("delivery").GetString());
        Assert.Contains("not named", page.GetProperty("observation").GetString());
        Assert.Equal(2, page.GetProperty("events").GetArrayLength());

        var asked = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/messages", new { text = "How does this work?" });
        Assert.Equal(HttpStatusCode.OK, asked.StatusCode);
        var detail = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/puerto-azul");
        var events = detail!.GetProperty("events");
        Assert.Equal(3, events.GetArrayLength());
        Assert.Equal("MESSAGE_RECEIVED", events[2].GetProperty("kind").GetString());
        Assert.DoesNotContain("INTERESTED", detail.GetRawText());
    }
}
