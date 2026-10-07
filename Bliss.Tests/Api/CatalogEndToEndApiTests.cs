using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

/// <summary>
/// Walks the catalog conversation surface, stored images, adaptation, ledger,
/// joint reading, and deferred voyage. A blocked voyage is the expected end
/// of this path.
/// </summary>
public sealed class CatalogEndToEndApiTests
{
    [Fact]
    public async Task Catalog_surface_runs_from_question_through_blocked_voyage()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();

        var board = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint");
        Assert.Equal("RECORDED", board.GetProperty("geometryStatus").GetString());
        Assert.Equal(50, board.GetProperty("references").GetProperty("active").GetInt32());
        Assert.True(board.GetProperty("creatorAuthorized").GetBoolean());

        var usedCar = await client.GetAsync("/api/operations/blueprint/references/ACA-005-V1/image");
        var premium = await client.GetAsync("/api/operations/blueprint/showcases/ARE-001-V1/image");
        var entry = await client.GetAsync("/api/operations/blueprint/showcases/ARE-003-V1/image");
        Assert.Equal(HttpStatusCode.OK, usedCar.StatusCode);
        Assert.Equal("image/png", usedCar.Content.Headers.ContentType?.MediaType);
        Assert.True(usedCar.Content.Headers.ContentLength > 1000);
        Assert.Equal(HttpStatusCode.OK, premium.StatusCode);
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);

        var prompts = new (string Prompt, string Intent, bool Displayed, bool Escalated)[]
        {
            ("This is my first campaign. I want to try Alpha, but I don't want to spend very much.", "ENTRY", true, false),
            ("We want a premium campaign with a much larger visual presence.", "PREMIUM", true, false),
            ("We don't want another advertiser displayed beside us.", "EXCLUSIVITY", true, false),
            ("Will this same advertising configuration work on mobile?", "DEVICE", false, true),
            ("What if I want the advertisement displayed 20 times instead of 5?", "FREQUENCY", false, true),
            ("How will I know my advertisement actually ran?", "DELIVERY", false, true),
            ("I want a configuration covering 70% of the podcast screen for $10.", "UNSUPPORTED", false, true)
        };

        foreach (var prompt in prompts)
        {
            var response = await client.PostAsJsonAsync(
                "/api/operations/blueprint/ask",
                new { message = prompt.Prompt });
            var turn = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("turn");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(prompt.Intent, turn.GetProperty("intent").GetString());
            Assert.Equal(prompt.Displayed, turn.GetProperty("showcaseDisplayed").GetBoolean());
            Assert.Equal(prompt.Escalated, turn.GetProperty("humanEscalation").GetBoolean());
            Assert.False(turn.GetProperty("inventedProduct").GetBoolean());
            Assert.False(turn.GetProperty("inventedPrice").GetBoolean());
            Assert.Equal("RECORDED", turn.GetProperty("geometryStatus").GetString());
            if (prompt.Intent is not ("DEVICE" or "DELIVERY"))
            {
                Assert.Contains("will not state a number", turn.GetProperty("reply").GetString());
            }

            Assert.DoesNotContain("$", turn.GetProperty("reply").GetString());
            Assert.Equal(0, turn.GetProperty("modelCalls").GetInt32());
            Assert.False(turn.GetProperty("campaignReady").GetBoolean());
            Assert.Equal("NOT_SENT", turn.GetProperty("delivery").GetString());
            if (prompt.Displayed)
            {
                Assert.NotEmpty(turn.GetProperty("productIds").EnumerateArray());
                Assert.NotEmpty(turn.GetProperty("showcaseIds").EnumerateArray());
            }
        }

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            var ledger = await database.CatalogConversations.OrderBy(item => item.RecordedAt).ToListAsync();
            Assert.Equal(7, ledger.Count);
            Assert.Equal(
                ["ENTRY", "PREMIUM", "EXCLUSIVITY", "DEVICE", "FREQUENCY", "DELIVERY", "UNSUPPORTED"],
                ledger.Select(item => item.Intent).ToArray());
            Assert.All(ledger, row =>
            {
                Assert.False(row.InventedProduct);
                Assert.False(row.InventedPrice);
                Assert.Equal(0, row.ModelCalls);
                Assert.False(row.CampaignReady);
                Assert.Equal("NOT_SENT", row.Delivery);
            });
            Assert.All(ledger.Take(3), row =>
            {
                Assert.True(row.ShowcaseDisplayed);
                Assert.False(string.IsNullOrWhiteSpace(row.ProductIds));
                Assert.False(string.IsNullOrWhiteSpace(row.ShowcaseIds));
            });
        }

        var adaptationResponse = await client.PostAsJsonAsync(
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
        var adaptation = (await adaptationResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("adaptation");
        Assert.Equal("RECOMPOSED", adaptation.GetProperty("status").GetString());
        Assert.Equal(320, adaptation.GetProperty("slots")[0].GetProperty("width").GetInt32());
        Assert.Equal(1080, adaptation.GetProperty("slots")[0].GetProperty("height").GetInt32());
        Assert.Equal(1280, adaptation.GetProperty("slots")[1].GetProperty("width").GetInt32());
        Assert.Equal(180, adaptation.GetProperty("slots")[1].GetProperty("height").GetInt32());

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
        Assert.Equal(
            "REGENERATE",
            (await copied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("similarity").GetProperty("status").GetString());

        var joint = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint/joint-acceptance");
        Assert.Equal("BLOCKED", joint.GetProperty("status").GetString());
        Assert.Equal("RETRIEVED", joint.GetProperty("retrievalStatus").GetString());
        Assert.Equal("RECOMPOSED", joint.GetProperty("adaptationStatus").GetString());
        Assert.Equal("UNCLAIMED", joint.GetProperty("hostedAcceptance").GetString());
        Assert.Equal(7, joint.GetProperty("conversations").GetArrayLength());
        Assert.Equal("NOT_SENT", joint.GetProperty("delivery").GetString());

        var voyageResponse = await client.PostAsJsonAsync(
            "/api/operations/academy/acceptance-voyages",
            new
            {
                voyageKey = "catalog-end-to-end-panama-pharmacy",
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
        Assert.Equal(HttpStatusCode.OK, voyageResponse.StatusCode);
        Assert.Equal("BLOCKED", voyage.GetProperty("status").GetString());
        Assert.Equal("PROVIDER_CONFIGURATION_REQUIRED", voyage.GetProperty("providerJob").GetProperty("status").GetString());
        Assert.Equal("RECOMPOSED", voyage.GetProperty("inventoryPreflight").GetProperty("status").GetString());
        Assert.Equal(0, voyage.GetProperty("modelCalls").GetInt32());
        Assert.False(voyage.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", voyage.GetProperty("delivery").GetString());
    }
}
