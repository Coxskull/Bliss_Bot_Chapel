using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class BlueprintPhaseApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public BlueprintPhaseApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Blueprint_board_registers_candidate_uploads_without_activating_them()
    {
        var client = _factory.CreateClient();
        var board = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint");
        var references = board.GetProperty("references");

        Assert.Equal("NOT_SENT", board.GetProperty("delivery").GetString());
        Assert.False(board.GetProperty("campaignReady").GetBoolean());
        Assert.Equal(0, board.GetProperty("modelCalls").GetInt32());
        Assert.Equal("RECORDED", board.GetProperty("geometryStatus").GetString());
        Assert.Equal(4, board.GetProperty("products").GetArrayLength());
        Assert.Equal(7, board.GetProperty("slots").GetArrayLength());
        Assert.All(board.GetProperty("slots").EnumerateArray(), slot =>
        {
            Assert.Equal("ARE-GEO-V1", slot.GetProperty("version").GetString());
            Assert.True(slot.GetProperty("width").GetInt32() > 0);
            Assert.True(slot.GetProperty("height").GetInt32() > 0);
            Assert.Equal(
                slot.GetProperty("width").GetInt32() * slot.GetProperty("height").GetInt32(),
                slot.GetProperty("area").GetInt32());
        });
        Assert.Equal(50, references.GetProperty("registered").GetInt32());
        Assert.Equal(50, references.GetProperty("uploaded").GetInt32());
        Assert.Equal(0, references.GetProperty("awaitingUpload").GetInt32());
        Assert.Equal(50, references.GetProperty("active").GetInt32());
        Assert.Contains("will not state a number", board.GetProperty("economics").GetString());
        Assert.Equal("BASELINE_NOT_RECORDED", board.GetProperty("regressionStatus").GetString());
        Assert.Equal("RETRIEVED", board.GetProperty("sampleRetrieval").GetProperty("status").GetString());
        Assert.Equal(8, board.GetProperty("unassigned").GetArrayLength());
        Assert.Contains(
            board.GetProperty("unassigned").EnumerateArray(),
            item => item.GetProperty("fileName").GetString() == "Catalog.jpeg");

        var pharmacy = references.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("referenceId").GetString() == "ACA-001-V1");
        Assert.Equal("Pharmacy.jpeg", pharmacy.GetProperty("expectedFile").GetString());
        Assert.Equal("ACTIVE", pharmacy.GetProperty("lifecycle").GetString());
        Assert.Equal("UPLOADED", pharmacy.GetProperty("uploadStatus").GetString());
        Assert.True(pharmacy.GetProperty("assetPresent").GetBoolean());
        Assert.Contains("product lighting", pharmacy.GetProperty("learn").GetString());
        Assert.Contains("VidaCare", pharmacy.GetProperty("doNotCopy").GetString());

        var usedCar = references.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("referenceId").GetString() == "ACA-005-V1");
        Assert.True(usedCar.GetProperty("assetPresent").GetBoolean());
        Assert.Equal("ACTIVE", usedCar.GetProperty("lifecycle").GetString());
        Assert.Equal("UPLOADED", usedCar.GetProperty("uploadStatus").GetString());
        Assert.Contains("DriveMax", usedCar.GetProperty("doNotCopy").GetString());

        foreach (var referenceId in new[] { "ACA-004-V1", "ACA-014-V1", "ACA-033-V1", "ACA-044-V1" })
        {
            var confirmed = references.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("referenceId").GetString() == referenceId);
            Assert.Equal("ACTIVE", confirmed.GetProperty("lifecycle").GetString());
            Assert.Contains("Owner confirmed 2026-10-07", confirmed.GetProperty("mappingNote").GetString());
            Assert.False(string.IsNullOrWhiteSpace(confirmed.GetProperty("learn").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(confirmed.GetProperty("doNotCopy").GetString()));
        }

        var needs = board.GetProperty("ownerNeeds");
        Assert.True(needs.GetProperty("open").GetInt32() > 0);
        Assert.Contains(
            needs.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("needId").GetString() == "ACA-005-FILE"
                && item.GetProperty("status").GetString() == "SUPPLIED"
                && item.GetProperty("ownerEntry").GetString()!.Contains("ACA-005-V1.png"));
        Assert.Contains(
            needs.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("needId").GetString() == "BIND-004"
                && item.GetProperty("status").GetString() == "SUPPLIED");
        Assert.Equal("UNCLASSIFIED", board.GetProperty("quality").GetProperty("status").GetString());
        Assert.All(
            board.GetProperty("quality").GetProperty("attributes").EnumerateArray(),
            item => Assert.Equal("UNCLASSIFIED", item.GetProperty("grade").GetString()));

        var joint = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint/joint-acceptance");
        Assert.Equal("BLOCKED", joint.GetProperty("status").GetString());
        Assert.Equal(7, joint.GetProperty("conversations").GetArrayLength());
        Assert.Equal("RETRIEVED", joint.GetProperty("retrievalStatus").GetString());
        Assert.Equal("RECOMPOSED", joint.GetProperty("adaptationStatus").GetString());
        Assert.Equal("NO_AUTHORIZED_PRICE", joint.GetProperty("gates").EnumerateArray().Single(item => item.GetProperty("gateId").GetString() == "ECONOMICS").GetProperty("status").GetString());
        Assert.Equal("UNCLAIMED", joint.GetProperty("hostedAcceptance").GetString());
        Assert.Equal(0, joint.GetProperty("modelCalls").GetInt32());
        Assert.False(joint.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", joint.GetProperty("delivery").GetString());
        Assert.Equal(4, joint.GetProperty("missionControl").GetArrayLength());

        foreach (var showcase in board.GetProperty("showcases").EnumerateArray())
        {
            Assert.True(showcase.GetProperty("assetPresent").GetBoolean());
            var lifecycle = showcase.GetProperty("lifecycle").GetString();
            Assert.Equal(showcase.GetProperty("showcaseId").GetString() == "ARE-GUIDE-001-V1" ? "DRAFT" : "ACTIVE", lifecycle);
        }

    }

    [Fact]
    public async Task Blueprint_serves_stored_images_and_refuses_a_missing_or_unassigned_path()
    {
        var client = _factory.CreateClient();
        var pharmacy = await client.GetAsync("/api/operations/blueprint/references/ACA-001-V1/image");
        var missing = await client.GetAsync("/api/operations/blueprint/references/ACA-999-V1/image");
        var showcase = await client.GetAsync("/api/operations/blueprint/showcases/ARE-001-V1/image");
        var catalog = await client.GetAsync("/api/operations/blueprint/unassigned/Catalog.jpeg");
        var claimedAsUnassigned = await client.GetAsync("/api/operations/blueprint/unassigned/Pharmacy.jpeg");
        var traversal = await client.GetAsync("/api/operations/blueprint/unassigned/../MANIFEST.tsv");

        Assert.Equal(HttpStatusCode.OK, pharmacy.StatusCode);
        Assert.Equal("image/jpeg", pharmacy.Content.Headers.ContentType?.MediaType);
        Assert.True(pharmacy.Content.Headers.ContentLength > 0);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, showcase.StatusCode);
        Assert.Equal("image/jpeg", showcase.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        Assert.Equal("image/jpeg", catalog.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, claimedAsUnassigned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, traversal.StatusCode);
    }

    [Fact]
    public async Task Ask_refuses_an_invented_configuration_and_records_the_turn()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/operations/blueprint/ask",
            new { message = "I want a configuration covering 70% of the podcast screen for $10." });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var turn = body.GetProperty("turn");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("UNSUPPORTED", turn.GetProperty("intent").GetString());
        Assert.True(turn.GetProperty("humanEscalation").GetBoolean());
        Assert.False(turn.GetProperty("inventedProduct").GetBoolean());
        Assert.False(turn.GetProperty("inventedPrice").GetBoolean());
        Assert.False(turn.GetProperty("showcaseDisplayed").GetBoolean());
        Assert.Equal("NOT_SENT", turn.GetProperty("delivery").GetString());
        Assert.DoesNotContain("$", turn.GetProperty("reply").GetString());
    }

    [Fact]
    public async Task Adapt_uses_owner_approved_geometry_and_rejects_a_scaled_prototype()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/operations/blueprint/adapt",
            new
            {
                productId = "ARE-P01",
                scalePrototype = false,
                brandName = "Norte Salud",
                headline = "Retira tu receta",
                face = "A local pharmacist",
                product = "A neighborhood pharmacy"
            });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var adaptation = body.GetProperty("adaptation");
        var slots = adaptation.GetProperty("slots").EnumerateArray().Select(item => item.GetProperty("slotId").GetString()).ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("RECOMPOSED", adaptation.GetProperty("status").GetString());
        Assert.Equal(["LEFT_VERTICAL", "BOTTOM_FULL"], slots);
        Assert.Equal(320, adaptation.GetProperty("slots")[0].GetProperty("width").GetInt32());
        Assert.Equal(1080, adaptation.GetProperty("slots")[0].GetProperty("height").GetInt32());
        Assert.Equal(1280, adaptation.GetProperty("slots")[1].GetProperty("width").GetInt32());
        Assert.Equal(180, adaptation.GetProperty("slots")[1].GetProperty("height").GetInt32());
        Assert.False(adaptation.GetProperty("scaledFromPrototype").GetBoolean());
        Assert.Equal("PASS", body.GetProperty("similarity").GetProperty("status").GetString());
        Assert.Equal(8, body.GetProperty("workers").GetArrayLength());
        Assert.Equal(0, body.GetProperty("modelCalls").GetInt32());
        Assert.False(body.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", body.GetProperty("delivery").GetString());

        var copied = await client.PostAsJsonAsync(
            "/api/operations/blueprint/adapt",
            new
            {
                productId = "ARE-P01",
                brandName = "VidaCare Pharmacy",
                headline = "Care for a Brighter You",
                face = "VidaCare pharmacist",
                product = "VidaCare"
            });
        var copiedBody = await copied.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, copied.StatusCode);
        Assert.Equal("REGENERATE", copiedBody.GetProperty("similarity").GetProperty("status").GetString());
        Assert.False(copiedBody.GetProperty("campaignReady").GetBoolean());

        var scaled = await client.PostAsJsonAsync(
            "/api/operations/blueprint/adapt",
            new { productId = "ARE-P01", scalePrototype = true });
        var scaledBody = await scaled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, scaled.StatusCode);
        Assert.Contains("not scaled", scaledBody.GetProperty("error").GetString());
        Assert.Equal("NOT_SENT", scaledBody.GetProperty("delivery").GetString());

        var unknown = await client.PostAsJsonAsync(
            "/api/operations/blueprint/adapt",
            new { productId = "ARE-NOPE" });
        var unknownBody = await unknown.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("None was invented", unknownBody.GetProperty("error").GetString());
    }
}
