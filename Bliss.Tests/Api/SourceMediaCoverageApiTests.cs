using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class SourceMediaCoverageApiTests
{
    [Fact]
    public async Task A_studio_slice_covers_its_stored_market_and_a_duplicate_does_not()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/fuel");
        Assert.Equal("NOT_SENT", before.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.Contains("not a census", before.GetProperty("notice").GetString());
        Assert.Equal(0, before.GetProperty("markets").GetArrayLength());
        Assert.Equal("SHORTAGE", before.GetProperty("fuel").GetProperty("fuelStatus").GetString());

        var slice = await client.PostAsJsonAsync(
            "/api/demonstrations/source-media/studio-slice",
            new { market = "Panama City", country = "Panama", seconds = 8 });
        Assert.Equal(HttpStatusCode.OK, slice.StatusCode);

        var covered = await client.GetFromJsonAsync<JsonElement>("/api/operations/fuel");
        var market = Assert.Single(covered.GetProperty("markets").EnumerateArray());
        Assert.Equal("Panama City", market.GetProperty("market").GetString());
        Assert.Equal("Panama", market.GetProperty("countryLine").GetString());
        Assert.Equal(1, market.GetProperty("qualifiedSlices").GetInt32());
        Assert.Contains("stored rows", market.GetProperty("notice").GetString());
        Assert.Equal(1, covered.GetProperty("fuel").GetProperty("qualifiedUnique").GetInt32());
        Assert.Equal(0, covered.GetProperty("withheld").GetArrayLength());
        Assert.Equal("NOT_SENT", covered.GetProperty("delivery").GetString());
    }
}
