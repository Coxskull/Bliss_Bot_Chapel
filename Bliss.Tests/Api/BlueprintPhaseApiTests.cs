using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bliss.Tests.Api;

public sealed class BlueprintPhaseApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public BlueprintPhaseApiTests(BlissApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Blueprint_board_reports_draft_products_and_awaiting_prototypes()
    {
        var client = _factory.CreateClient();
        var board = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint");

        Assert.Equal("NOT_SENT", board.GetProperty("delivery").GetString());
        Assert.False(board.GetProperty("campaignReady").GetBoolean());
        Assert.Equal(0, board.GetProperty("modelCalls").GetInt32());
        Assert.Equal("UNRECORDED", board.GetProperty("geometryStatus").GetString());
        Assert.Equal(4, board.GetProperty("products").GetArrayLength());
        Assert.Equal(7, board.GetProperty("slots").GetArrayLength());
        Assert.Equal(50, board.GetProperty("references").GetProperty("registered").GetInt32());
        Assert.Equal(0, board.GetProperty("references").GetProperty("active").GetInt32());
        Assert.Contains("will not state a number", board.GetProperty("economics").GetString());
        Assert.Equal("BASELINE_NOT_RECORDED", board.GetProperty("regressionStatus").GetString());
        Assert.Equal("NICHE_REFERENCE_NOT_ACTIVE", board.GetProperty("sampleRetrieval").GetProperty("status").GetString());
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
