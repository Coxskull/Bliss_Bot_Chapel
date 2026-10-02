using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class FactoryBatchApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public FactoryBatchApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_batch_counts_a_preserved_business_and_a_produced_recipe()
    {
        var client = _factory.CreateClient();
        var preserved = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = "https://example.com/puerto-azul"
        });
        Assert.Equal(HttpStatusCode.OK, preserved.StatusCode);

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
        var clip = await slice.Content.ReadFromJsonAsync<JsonElement>();
        var produced = await client.PostAsJsonAsync(
            "/api/demonstrations/mesa-norte/produce",
            new { sourceClipId = clip!.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.OK, produced.StatusCode);

        var batchResponse = await client.PostAsJsonAsync("/api/demonstrations/factory-batch", new { });
        var batch = await batchResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, batchResponse.StatusCode);
        Assert.Equal("PASSED", batch!.GetProperty("status").GetString());
        Assert.Equal(1, batch.GetProperty("preservedCount").GetInt32());
        Assert.Equal(1, batch.GetProperty("conceptCount").GetInt32());
        Assert.Equal(0, batch.GetProperty("aiCalls").GetInt32());
        Assert.Equal(0, batch.GetProperty("exceptionCount").GetInt32());
        Assert.Equal("NOT_SENT", batch.GetProperty("delivery").GetString());
        var cost = batch.GetProperty("cost").GetString();
        Assert.Contains("No dollar amount", cost);
        Assert.DoesNotContain("$", cost);
    }
}
