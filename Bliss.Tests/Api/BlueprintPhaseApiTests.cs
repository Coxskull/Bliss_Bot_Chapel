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
        Assert.Equal("UNRECORDED", board.GetProperty("geometryStatus").GetString());
        Assert.Equal(4, board.GetProperty("products").GetArrayLength());
        Assert.Equal(7, board.GetProperty("slots").GetArrayLength());
        Assert.Equal(50, references.GetProperty("registered").GetInt32());
        Assert.Equal(49, references.GetProperty("uploaded").GetInt32());
        Assert.Equal(1, references.GetProperty("awaitingUpload").GetInt32());
        Assert.Equal(0, references.GetProperty("active").GetInt32());
        Assert.Contains("will not state a number", board.GetProperty("economics").GetString());
        Assert.Equal("BASELINE_NOT_RECORDED", board.GetProperty("regressionStatus").GetString());
        Assert.Equal("NICHE_REFERENCE_NOT_ACTIVE", board.GetProperty("sampleRetrieval").GetProperty("status").GetString());
        Assert.Equal(8, board.GetProperty("unassigned").GetArrayLength());
        Assert.Contains(
            board.GetProperty("unassigned").EnumerateArray(),
            item => item.GetProperty("fileName").GetString() == "Catalog.jpeg");

        var pharmacy = references.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("referenceId").GetString() == "ACA-001-V1");
        Assert.Equal("Pharmacy.jpeg", pharmacy.GetProperty("expectedFile").GetString());
        Assert.Equal("CANDIDATE", pharmacy.GetProperty("lifecycle").GetString());
        Assert.Equal("UPLOADED", pharmacy.GetProperty("uploadStatus").GetString());
        Assert.True(pharmacy.GetProperty("assetPresent").GetBoolean());
        Assert.Equal(string.Empty, pharmacy.GetProperty("learn").GetString());

        var usedCar = references.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("referenceId").GetString() == "ACA-005-V1");
        Assert.False(usedCar.GetProperty("assetPresent").GetBoolean());
        Assert.Equal("AWAITING_UPLOAD", usedCar.GetProperty("uploadStatus").GetString());

        foreach (var referenceId in new[] { "ACA-004-V1", "ACA-014-V1", "ACA-033-V1", "ACA-044-V1" })
        {
            var confirmed = references.GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("referenceId").GetString() == referenceId);
            Assert.Equal("CANDIDATE", confirmed.GetProperty("lifecycle").GetString());
            Assert.Contains("Owner confirmed 2026-10-07", confirmed.GetProperty("mappingNote").GetString());
            Assert.Equal(string.Empty, confirmed.GetProperty("learn").GetString());
            Assert.Equal(string.Empty, confirmed.GetProperty("doNotCopy").GetString());
        }

        foreach (var showcase in board.GetProperty("showcases").EnumerateArray())
        {
            Assert.True(showcase.GetProperty("assetPresent").GetBoolean());
            Assert.Equal("DRAFT", showcase.GetProperty("lifecycle").GetString());
        }
    }

    [Fact]
    public async Task Blueprint_serves_stored_images_and_refuses_a_missing_or_unassigned_path()
    {
        var client = _factory.CreateClient();
        var pharmacy = await client.GetAsync("/api/operations/blueprint/references/ACA-001-V1/image");
        var missing = await client.GetAsync("/api/operations/blueprint/references/ACA-005-V1/image");
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
}
