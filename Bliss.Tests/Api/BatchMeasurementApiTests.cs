using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class BatchMeasurementApiTests
{
    [Fact]
    public async Task A_local_batch_is_measured_once_and_a_retry_is_a_second_row()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/measure");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("greenMeansSend").GetBoolean());
        Assert.False(before.GetProperty("hostedAcceptanceClaimed").GetBoolean());
        Assert.False(before.GetProperty("factoryTargetClaimed").GetBoolean());
        Assert.False(before.GetProperty("censusClaimed").GetBoolean());
        Assert.False(before.GetProperty("measured").GetBoolean());
        Assert.Contains("not hosted acceptance", before.GetProperty("notice").GetString());
        Assert.Contains("15-minute factory target is not claimed", before.GetProperty("notice").GetString());

        var casa = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Panama City",
            businessName = "Casa Verde",
            publicSourceUrl = "https://example.com/casa-verde"
        });
        Assert.Equal(HttpStatusCode.OK, casa.StatusCode);
        var puerto = await client.PostAsJsonAsync("/api/demonstrations/discover", new
        {
            niche = "restaurant",
            market = "Quito",
            businessName = "Puerto Azul",
            publicSourceUrl = "https://example.com/puerto-azul"
        });
        Assert.Equal(HttpStatusCode.OK, puerto.StatusCode);

        var measured = await client.PostAsJsonAsync("/api/operations/measure", new
        {
            idempotencyKey = "local-batch-1",
            attempt = 1
        });
        var first = await measured.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, measured.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("duplicate").GetBoolean());
        var latest = first.GetProperty("latest");
        Assert.Equal(2, latest.GetProperty("storedProspects").GetInt32());
        Assert.True(latest.GetProperty("elapsedMilliseconds").GetInt64() >= 0);
        Assert.True(latest.GetProperty("workingSetBytes").GetInt64() > 0);
        Assert.Contains("not a hosted capacity claim", latest.GetProperty("resourceLine").GetString());
        Assert.Contains("No invoice is on file", latest.GetProperty("costLine").GetString());
        Assert.Equal(0, latest.GetProperty("retries").GetInt32());
        Assert.Equal(0, latest.GetProperty("partialFailures").GetInt32());
        Assert.True(latest.GetProperty("recovered").GetBoolean());
        Assert.False(latest.GetProperty("leakage").GetBoolean());
        Assert.False(latest.GetProperty("hostedAcceptanceClaimed").GetBoolean());
        Assert.False(latest.GetProperty("factoryTargetClaimed").GetBoolean());
        Assert.False(latest.GetProperty("censusClaimed").GetBoolean());
        Assert.Equal("NOT_SENT", latest.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/measure", new
        {
            idempotencyKey = "local-batch-1",
            attempt = 1
        });
        var duplicate = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.True(duplicate!.GetProperty("duplicate").GetBoolean());
        Assert.False(duplicate.GetProperty("written").GetBoolean());
        Assert.Equal(latest.GetProperty("elapsedMilliseconds").GetInt64(), duplicate.GetProperty("latest").GetProperty("elapsedMilliseconds").GetInt64());

        var retry = await client.PostAsJsonAsync("/api/operations/measure", new
        {
            idempotencyKey = "local-batch-retry-1",
            attempt = 2
        });
        var second = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.True(second!.GetProperty("written").GetBoolean());
        Assert.Equal(1, second.GetProperty("latest").GetProperty("retries").GetInt32());
        Assert.True(second.GetProperty("latest").GetProperty("recovered").GetBoolean());
        Assert.False(second.GetProperty("latest").GetProperty("leakage").GetBoolean());
        Assert.False(second.GetProperty("hostedAcceptanceClaimed").GetBoolean());

        var board = await client.GetFromJsonAsync<JsonElement>("/api/operations/measure");
        Assert.Equal(2, board!.GetProperty("measurements").GetArrayLength());
        Assert.Equal("NOT_SENT", board.GetProperty("delivery").GetString());
        Assert.False(board.GetProperty("censusClaimed").GetBoolean());
    }
}
