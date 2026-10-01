using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class ProspectDiscoveryApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public ProspectDiscoveryApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Discovery_requires_a_public_source_then_one_demonstration_stays_unsent()
    {
        var client = _factory.CreateClient();
        var rejected = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var rejectedBody = await rejected.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(rejectedBody.GetProperty("passesInitialScreen").GetBoolean());
        Assert.Contains("public http", rejectedBody.GetProperty("error").GetString());

        var catalog = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/catalog");
        Assert.Contains("restaurant", catalog.GetProperty("niches").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("Panama City", catalog.GetProperty("markets").EnumerateArray().Select(x => x.GetProperty("city").GetString()));

        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = "https://example.com/casa-verde"
        });
        var prospect = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(100, prospect!.GetProperty("opportunityScore").GetInt32());
        Assert.Equal("UNVERIFIED", prospect.GetProperty("decisionMakerStatus").GetString());
        Assert.Equal("NOT_SENT", prospect.GetProperty("delivery").GetString());
        Assert.Equal("OPPORTUNITY_SCORED", prospect.GetProperty("prospectState").GetString());
        Assert.Empty(prospect.GetProperty("concepts").EnumerateArray());
        Assert.Contains("Owner", prospect.GetProperty("buyingRoles").EnumerateArray().Select(x => x.GetString()));

        var tooSoon = await client.PostAsJsonAsync("/api/demonstrations/casa-verde/produce", new { sourceClipId = (Guid?)null });
        Assert.Equal(HttpStatusCode.BadRequest, tooSoon.StatusCode);

        var slice = await client.PostAsJsonAsync(
            "/api/demonstrations/source-media/studio-slice",
            new { market = "Panama City", country = "Panama", seconds = 8 });
        Assert.Equal(HttpStatusCode.OK, slice.StatusCode);
        var clip = await slice.Content.ReadFromJsonAsync<JsonElement>();

        var produced = await client.PostAsJsonAsync(
            "/api/demonstrations/casa-verde/produce",
            new { sourceClipId = clip!.GetProperty("id").GetGuid() });
        var page = await produced.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, produced.StatusCode);
        Assert.Equal("DEMONSTRATION_PREPARED", page!.GetProperty("prospectState").GetString());
        Assert.Single(page.GetProperty("concepts").EnumerateArray());
        Assert.Equal("NOT_SENT", page.GetProperty("delivery").GetString());
        Assert.Contains("Casa Verde has not sponsored", page.GetProperty("disclosure").GetString());

        var reply = await client.PostAsJsonAsync(
            "/api/demonstrations/casa-verde/messages",
            new { text = "Who is the marketing director?" });
        var conversation = await reply.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not verified", conversation!.GetProperty("reply").GetString(), StringComparison.OrdinalIgnoreCase);

        var outreach = await client.GetAsync("/outreach/casa-verde");
        Assert.Equal(HttpStatusCode.OK, outreach.StatusCode);
    }
}
