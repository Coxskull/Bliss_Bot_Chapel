using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class RecipeRuntimeApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public RecipeRuntimeApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Produce_stores_overlay_1_and_serves_the_player()
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

        var slice = await client.PostAsJsonAsync(
            "/api/demonstrations/source-media/studio-slice",
            new { market = "Panama City", country = "Panama", seconds = 8 });
        Assert.Equal(HttpStatusCode.OK, slice.StatusCode);
        var clip = await slice.Content.ReadFromJsonAsync<JsonElement>();

        var produced = await client.PostAsJsonAsync(
            "/api/demonstrations/mesa-norte/produce",
            new { sourceClipId = clip!.GetProperty("id").GetGuid() });
        var page = await produced.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, produced.StatusCode);
        Assert.Equal("NOT_SENT", page!.GetProperty("delivery").GetString());
        Assert.Contains("Mesa Norte has not sponsored", page.GetProperty("disclosure").GetString());
        var concept = page.GetProperty("concepts")[0];
        Assert.Equal("overlay-1", concept.GetProperty("recipeVersion").GetString());
        Assert.Equal("PASSED", concept.GetProperty("qaStatus").GetString());
        Assert.Equal("La Mesa", concept.GetProperty("headline").GetString());
        Assert.Contains("/demonstrations/mesa-norte", concept.GetProperty("qrDestination").GetString());

        Assert.Equal("PLAYER", concept.GetProperty("servedPicture").GetString());
        Assert.Contains("not required", concept.GetProperty("playerNotice").GetString());
        Assert.Contains("NOT_SENT", concept.GetProperty("playerNotice").GetString());
        Assert.False(concept.TryGetProperty("videoUrl", out _));
        var source = await client.GetAsync(concept.GetProperty("sourceUrl").GetString());
        var qr = await client.GetAsync(concept.GetProperty("qrUrl").GetString());
        var flat = await client.GetAsync("/api/demonstrations/mesa-norte/concepts/" + concept.GetProperty("id").GetString() + "/video");
        Assert.Equal(HttpStatusCode.OK, source.StatusCode);
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, flat.StatusCode);
        Assert.True(source.Content.Headers.ContentLength > 0);
        Assert.Equal("video/mp4", source.Content.Headers.ContentType?.MediaType);
    }
}
