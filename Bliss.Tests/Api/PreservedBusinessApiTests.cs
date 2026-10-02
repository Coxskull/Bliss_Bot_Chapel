using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class PreservedBusinessApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public PreservedBusinessApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_named_public_business_below_100_is_kept_without_a_demonstration()
    {
        var client = _factory.CreateClient();
        var rejected = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var missing = await client.GetAsync("/api/demonstrations/puerto-azul");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = "https://example.com/puerto-azul"
        });
        var prospect = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(75, prospect!.GetProperty("opportunityScore").GetInt32());
        Assert.Equal("PRESERVED", prospect.GetProperty("prospectState").GetString());
        Assert.Equal("Quito", prospect.GetProperty("market").GetString());
        Assert.Empty(prospect.GetProperty("concepts").EnumerateArray());
        Assert.Equal("UNVERIFIED", prospect.GetProperty("decisionMakerStatus").GetString());
        Assert.Equal("NOT_SENT", prospect.GetProperty("delivery").GetString());
        Assert.Contains("Owner", prospect.GetProperty("buyingRoles").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("No demonstration", prospect.GetProperty("subject").GetString());
        Assert.Contains("Nothing is sent", prospect.GetProperty("messages")[0].GetProperty("text").GetString());

        var produced = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/produce", new { sourceClipId = (Guid?)null });
        Assert.Equal(HttpStatusCode.BadRequest, produced.StatusCode);
        var producedBody = await produced.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("no demonstration is manufactured", producedBody.GetProperty("error").GetString());

        var evidence = await client.PostAsJsonAsync("/api/demonstrations/puerto-azul/decision-maker", new
        {
            personName = "Ana Ruiz",
            role = "Owner",
            evidenceKind = "team_page",
            evidenceUrl = "https://example.com/puerto-azul/team"
        });
        Assert.Equal(HttpStatusCode.BadRequest, evidence.StatusCode);
        var evidenceBody = await evidence.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Nothing is sent", evidenceBody.GetProperty("error").GetString());

        var other = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "other",
            market = "Quito",
            businessName = "Mercado Luna",
            publicSourceUrl = "https://example.com/mercado-luna"
        });
        var otherPage = await other.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(50, otherPage!.GetProperty("opportunityScore").GetInt32());
        Assert.Equal("PRESERVED", otherPage.GetProperty("prospectState").GetString());
        Assert.Empty(otherPage.GetProperty("buyingRoles").EnumerateArray());

        var blankMarket = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "   ",
            businessName = "Rio Claro",
            publicSourceUrl = "https://example.com/rio-claro"
        });
        var blankPage = await blankMarket.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, blankMarket.StatusCode);
        Assert.Equal("Outside the initial markets", blankPage!.GetProperty("market").GetString());
        Assert.Equal("PRESERVED", blankPage.GetProperty("prospectState").GetString());
    }
}
