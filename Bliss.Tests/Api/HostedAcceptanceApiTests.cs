using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class HostedAcceptanceApiTests
{
    [Fact]
    public async Task The_reading_stores_the_process_posture_and_does_not_claim_hosted_acceptance()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<JsonElement>("/api/runtime/status");
        var posture = status!.GetProperty("productionPosture");
        var before = await client.GetFromJsonAsync<JsonElement>("/api/operations/hosted");
        Assert.Equal("NOT_SENT", before!.GetProperty("delivery").GetString());
        Assert.False(before.GetProperty("hostedAcceptanceClaimed").GetBoolean());
        Assert.False(before.GetProperty("identityContacted").GetBoolean());
        Assert.False(before.GetProperty("backupDrillRun").GetBoolean());
        Assert.Equal(0, before.GetProperty("history").GetArrayLength());
        Assert.Equal(posture.GetProperty("productionGatesApplied").GetBoolean(), before.GetProperty("productionGatesApplied").GetBoolean());
        Assert.Equal(posture.GetProperty("hostedDatabaseConfigured").GetBoolean(), before.GetProperty("hostedDatabaseConfigured").GetBoolean());

        var stored = await client.PostAsJsonAsync("/api/operations/hosted", new { readingKey = "hosted-reading-1" });
        var first = await stored.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(first!.GetProperty("written").GetBoolean());
        Assert.False(first.GetProperty("hostedAcceptanceClaimed").GetBoolean());
        var row = first.GetProperty("history")[0];
        Assert.Equal("hosted-reading-1", row.GetProperty("readingKey").GetString());
        Assert.Equal(status.GetProperty("environment").GetString(), row.GetProperty("environmentName").GetString());
        Assert.False(row.GetProperty("hostedAcceptanceClaimed").GetBoolean());
        Assert.False(row.GetProperty("identityContacted").GetBoolean());
        Assert.False(row.GetProperty("backupDrillRun").GetBoolean());
        Assert.Equal("NOT_SENT", row.GetProperty("delivery").GetString());

        var again = await client.PostAsJsonAsync("/api/operations/hosted", new { readingKey = "hosted-reading-1" });
        var second = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(second!.GetProperty("duplicate").GetBoolean());
        Assert.False(second.GetProperty("written").GetBoolean());
        Assert.Equal(1, second.GetProperty("history").GetArrayLength());

        var claim = await client.PostAsJsonAsync("/api/operations/hosted", new { readingKey = "hosted-reading-2", claimHosted = true });
        Assert.Equal(HttpStatusCode.BadRequest, claim.StatusCode);
        var refused = await claim.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not on file", refused!.GetProperty("error").GetString());
        Assert.False(refused.GetProperty("hostedAcceptanceClaimed").GetBoolean());
    }
}
