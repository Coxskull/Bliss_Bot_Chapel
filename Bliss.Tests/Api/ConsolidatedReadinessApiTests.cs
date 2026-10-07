using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

/// <summary>
/// One fail-closed reading across the approved Academy and catalog records.
/// This is a current-state contract, not a claim that the deferred live voyage
/// or hosted acceptance has completed.
/// </summary>
public sealed class ConsolidatedReadinessApiTests
{
    [Fact]
    public async Task Approved_inputs_clear_reference_and_geometry_gates_without_claiming_delivery()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();

        var board = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint");
        var joint = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint/joint-acceptance");
        var voyageResponse = await client.PostAsJsonAsync(
            "/api/operations/academy/acceptance-voyages",
            new
            {
                voyageKey = "consolidated-readiness-panama-pharmacy",
                advertiserName = "Harborlight Pharmacy",
                city = "Panama City",
                market = "Panama",
                niche = "pharmacy",
                objective = "Introduce prescription pickup to local customers",
                inventoryProductId = "ARE-P01"
            });
        var voyage = (await voyageResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("voyage")
            .GetProperty("report");

        Assert.Equal("RECORDED", board.GetProperty("geometryStatus").GetString());
        Assert.Equal(50, board.GetProperty("references").GetProperty("active").GetInt32());
        Assert.Equal("RETRIEVED", board.GetProperty("sampleRetrieval").GetProperty("status").GetString());
        Assert.True(board.GetProperty("creatorAuthorized").GetBoolean());
        Assert.Equal(6, board.GetProperty("ownerNeeds").GetProperty("open").GetInt32());
        Assert.All(board.GetProperty("slots").EnumerateArray(), slot =>
        {
            var width = slot.GetProperty("width").GetInt32();
            var height = slot.GetProperty("height").GetInt32();
            Assert.Equal("ARE-GEO-V1", slot.GetProperty("version").GetString());
            Assert.Equal(width * height, slot.GetProperty("area").GetInt32());
        });

        Assert.Equal("BLOCKED", joint.GetProperty("status").GetString());
        Assert.Equal("RETRIEVED", joint.GetProperty("retrievalStatus").GetString());
        Assert.Equal("RECOMPOSED", joint.GetProperty("adaptationStatus").GetString());
        Assert.Equal("UNCLAIMED", joint.GetProperty("hostedAcceptance").GetString());
        Assert.Equal(
            "NO_AUTHORIZED_PRICE",
            joint.GetProperty("gates").EnumerateArray()
                .Single(gate => gate.GetProperty("gateId").GetString() == "ECONOMICS")
                .GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.OK, voyageResponse.StatusCode);
        Assert.Equal("BLOCKED", voyage.GetProperty("status").GetString());
        Assert.Equal("READY", voyage.GetProperty("referenceIntelligence").GetProperty("status").GetString());
        Assert.Equal("RECOMPOSED", voyage.GetProperty("inventoryPreflight").GetProperty("status").GetString());
        Assert.Equal("PROVIDER_CONFIGURATION_REQUIRED", voyage.GetProperty("providerJob").GetProperty("status").GetString());
        Assert.Contains(
            voyage.GetProperty("blockers").EnumerateArray(),
            blocker => blocker.GetString() == "VISUAL_QUALITY_QA_REQUIRED");
        Assert.Contains(
            voyage.GetProperty("blockers").EnumerateArray(),
            blocker => blocker.GetString() == "HUMAN_REVIEW_REQUIRED");
        Assert.Contains(
            voyage.GetProperty("blockers").EnumerateArray(),
            blocker => blocker.GetString() == "USAGE_COST_UNRECORDED");
        Assert.DoesNotContain(
            voyage.GetProperty("blockers").EnumerateArray(),
            blocker => blocker.GetString() == "INVENTORY_GEOMETRY_REQUIRED");
        Assert.Equal("BASELINE_NOT_RECORDED", voyage.GetProperty("regression").GetProperty("status").GetString());
        Assert.Equal(0, voyage.GetProperty("modelCalls").GetInt32());
        Assert.False(voyage.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", voyage.GetProperty("delivery").GetString());
    }
}
