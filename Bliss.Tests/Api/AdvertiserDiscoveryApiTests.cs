using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class AdvertiserDiscoveryApiTests
{
    [Fact]
    public async Task A_public_source_is_written_once_and_the_reading_counts_stored_rows()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/discovery");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.False(before.GetProperty("censusClaimed").GetBoolean());
        Assert.Contains("not a census", before.GetProperty("notice").GetString());
        Assert.Contains("crawler did not run", before.GetProperty("notice").GetString());
        Assert.Equal(0, before.GetProperty("stored").GetInt32());
        Assert.Equal(0, before.GetProperty("prospects").GetArrayLength());

        var blank = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = " ",
            publicSourceUrl = "https://example.com/blank"
        });
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var symbols = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "!!!",
            publicSourceUrl = "https://example.com/symbols"
        });
        Assert.Equal(HttpStatusCode.BadRequest, symbols.StatusCode);
        var symbolBody = await symbols.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("usable prospect id", symbolBody.GetProperty("error").GetString());

        var created = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = "https://example.com/casa-verde"
        });
        var prospect = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.True(prospect!.GetProperty("written").GetBoolean());
        Assert.False(prospect.GetProperty("duplicate").GetBoolean());
        Assert.Equal(100, prospect.GetProperty("opportunityScore").GetInt32());
        Assert.Equal("OPPORTUNITY_SCORED", prospect.GetProperty("prospectState").GetString());
        Assert.Equal("NOT_SENT", prospect.GetProperty("delivery").GetString());
        Assert.Contains("crawler did not run", prospect.GetProperty("discoveryNotice").GetString());

        var repeated = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde Norte",
            publicSourceUrl = "https://example.com/casa-verde/"
        });
        var duplicate = await repeated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.True(duplicate!.GetProperty("duplicate").GetBoolean());
        Assert.False(duplicate.GetProperty("written").GetBoolean());
        Assert.Equal("casa-verde", duplicate.GetProperty("slug").GetString());
        Assert.Equal("Casa Verde", duplicate.GetProperty("businessName").GetString());
        Assert.Contains("second prospect was not written", duplicate.GetProperty("discoveryNotice").GetString());
        Assert.Equal("NOT_SENT", duplicate.GetProperty("delivery").GetString());

        var preserved = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Andes Table",
            publicSourceUrl = "https://example.com/andes-table"
        });
        var held = await preserved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);
        Assert.True(held!.GetProperty("written").GetBoolean());
        Assert.Equal("PRESERVED", held.GetProperty("prospectState").GetString());
        Assert.Equal(75, held.GetProperty("opportunityScore").GetInt32());
        Assert.Equal("NOT_SENT", held.GetProperty("delivery").GetString());

        var library = await client.GetFromJsonAsync<JsonElement>("/api/demonstrations/library");
        var slugs = library!.GetProperty("demonstrations").EnumerateArray().Select(item => item.GetProperty("slug").GetString()).ToList();
        Assert.Equal(2, slugs.Count);
        Assert.Contains("casa-verde", slugs);
        Assert.Contains("andes-table", slugs);

        var reading = await client.GetFromJsonAsync<JsonElement>("/api/operations/discovery");
        Assert.Equal(2, reading!.GetProperty("stored").GetInt32());
        Assert.Equal(1, reading.GetProperty("scored").GetInt32());
        Assert.Equal(1, reading.GetProperty("preserved").GetInt32());
        Assert.False(reading.GetProperty("censusClaimed").GetBoolean());
        Assert.Equal(0, reading.GetProperty("withheld").GetArrayLength());
        Assert.Equal("NOT_SENT", reading.GetProperty("delivery").GetString());
        var names = reading.GetProperty("prospects").EnumerateArray().Select(item => item.GetProperty("businessName").GetString()).ToList();
        Assert.Equal(new[] { "Andes Table", "Casa Verde" }, names);
        Assert.Contains("https://example.com/casa-verde", reading.GetProperty("prospects").EnumerateArray().Select(item => item.GetProperty("sourceUrl").GetString()));
    }
}
