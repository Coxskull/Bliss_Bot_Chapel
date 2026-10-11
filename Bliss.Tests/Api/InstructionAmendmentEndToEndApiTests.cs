using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

/// <summary>
/// Walks every stored owner instruction and both amendments. Supplied facts must
/// be present. Open facts must stay open. Neither amendment is closed.
/// </summary>
public sealed class InstructionAmendmentEndToEndApiTests
{
    [Fact]
    public async Task Every_instruction_and_both_amendments_are_read_without_inventing_open_facts()
    {
        await using var factory = new BlissApiFactory();
        var client = factory.CreateClient();
        var board = await client.GetFromJsonAsync<JsonElement>("/api/operations/blueprint");
        var references = board.GetProperty("references").GetProperty("items").EnumerateArray().ToList();
        var needs = board.GetProperty("ownerNeeds").GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(50, references.Count);
        Assert.Equal(50, board.GetProperty("references").GetProperty("active").GetInt32());
        Assert.Equal(0, board.GetProperty("references").GetProperty("awaitingUpload").GetInt32());
        Assert.All(references, reference =>
        {
            Assert.Equal("ACTIVE", reference.GetProperty("lifecycle").GetString());
            Assert.Equal("UPLOADED", reference.GetProperty("uploadStatus").GetString());
            Assert.True(reference.GetProperty("assetPresent").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(reference.GetProperty("learn").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(reference.GetProperty("doNotCopy").GetString()));
            Assert.Equal("UNCLASSIFIED", reference.GetProperty("qualityStatus").GetString());
            Assert.Equal("owner-upload", reference.GetProperty("provenanceSource").GetString());
            Assert.Equal("owner", reference.GetProperty("ownership").GetString());
            Assert.Equal("2026-10-07", reference.GetProperty("approvedOn").GetString());
            Assert.Equal("owner", reference.GetProperty("approvingAuthority").GetString());
            Assert.Equal("Academy quality reference", reference.GetProperty("permittedInternalUse").GetString());
        });
        Assert.All(
            board.GetProperty("quality").GetProperty("attributes").EnumerateArray(),
            attribute => Assert.Equal("UNCLASSIFIED", attribute.GetProperty("grade").GetString()));

        AssertReference(references, "ACA-005-V1", "ACA-005-V1.png", "used-car", "DriveMax");
        AssertReference(references, "ACA-004-V1", "Automotive.jpeg", "new-car", "Automotive.jpeg is new-car");
        AssertReference(references, "ACA-014-V1", "Italian restaurant.jpeg", "pizza", "Italian restaurant.jpeg is the pizza niche");
        AssertReference(references, "ACA-033-V1", "Home renovation.jpeg", "furniture", "Home renovation.jpeg is the furniture niche");
        AssertReference(references, "ACA-044-V1", "Coffeee.jpeg", "coffee-brand", "coffee product brand");
        AssertReference(references, "ACA-001-V1", "Pharmacy.jpeg", "pharmacy", "VidaCare");
        AssertReference(references, "ACA-002-V1", "Market.jpeg", "supermarket", "FreshMart");
        AssertReference(references, "ACA-007-V1", "Gym.jpeg", "fitness", "Nova Fit");
        AssertReference(references, "ACA-008-V1", "Auto shop.jpeg", "automotive", "Taller Ruta");
        AssertReference(references, "ACA-010-V1", "Motorcycle.jpeg", "motorcycle", "Brava Moto");

        var unassigned = board.GetProperty("unassigned").EnumerateArray()
            .Select(item => item.GetProperty("fileName").GetString() ?? string.Empty)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "Catalog.jpeg",
                "Podcast with ads 1.jpeg",
                "Podcast with ads 2.jpeg",
                "Podcast with ads 3.jpeg",
                "Podcast with ads 5.jpeg",
                "Podcast with ads 6.jpeg",
                "Podcast with ads.jpeg",
                "podcast with ads 4.jpeg"
            ],
            unassigned);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/operations/blueprint/references/ACA-005-V1/image")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/operations/blueprint/unassigned/Catalog.jpeg")).StatusCode);

        Assert.Equal("RECORDED", board.GetProperty("geometryStatus").GetString());
        Assert.Equal(7, board.GetProperty("slots").GetArrayLength());
        Assert.All(board.GetProperty("slots").EnumerateArray(), slot =>
        {
            Assert.Equal("ARE-GEO-V1", slot.GetProperty("version").GetString());
            Assert.Contains("not measured from the guide", slot.GetProperty("notice").GetString());
            Assert.Equal(
                slot.GetProperty("width").GetInt32() * slot.GetProperty("height").GetInt32(),
                slot.GetProperty("area").GetInt32());
        });

        Assert.Equal(4, board.GetProperty("products").GetArrayLength());
        Assert.All(board.GetProperty("products").EnumerateArray(), product =>
        {
            Assert.Equal("ACTIVE", product.GetProperty("lifecycle").GetString());
            Assert.Equal("OWNER_APPROVED", product.GetProperty("deviceStatus").GetString());
            Assert.Equal("OWNER_APPROVED", product.GetProperty("platformStatus").GetString());
            Assert.Equal("UNRECORDED", product.GetProperty("occupancyStatus").GetString());
            Assert.Equal(15, product.GetProperty("durationSeconds")[0].GetInt32());
            Assert.Contains("unpriced", product.GetProperty("notice").GetString());
        });
        Assert.True(board.GetProperty("creatorAuthorized").GetBoolean());
        Assert.Contains("will not state a number", board.GetProperty("economics").GetString());
        Assert.DoesNotContain("$", board.GetProperty("economics").GetString());
        Assert.Equal("NOT_SENT", board.GetProperty("delivery").GetString());
        Assert.False(board.GetProperty("campaignReady").GetBoolean());
        Assert.Equal(0, board.GetProperty("modelCalls").GetInt32());

        foreach (var showcase in board.GetProperty("showcases").EnumerateArray())
        {
            var id = showcase.GetProperty("showcaseId").GetString();
            Assert.True(showcase.GetProperty("assetPresent").GetBoolean());
            Assert.Equal(id == "ARE-GUIDE-001-V1" ? "DRAFT" : "ACTIVE", showcase.GetProperty("lifecycle").GetString());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/operations/blueprint/showcases/{id}/image")).StatusCode);
        }

        var openNeeds = needs.Where(item => item.GetProperty("status").GetString() == "OPEN")
            .Select(item => item.GetProperty("needId").GetString() ?? string.Empty)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["ACA-QUALITY", "ARE-DELIVERY", "ARE-DURATION-PRICE", "ARE-ECONOMICS", "HOSTED-ACCEPTANCE", "OPENAI-CREDITS"],
            openNeeds);
        Assert.All(
            needs.Where(item => item.GetProperty("status").GetString() == "OPEN"),
            item => Assert.Equal(string.Empty, item.GetProperty("ownerEntry").GetString()));
        Assert.Contains(needs, item => item.GetProperty("needId").GetString() == "AMENDMENT-REVIEW"
            && item.GetProperty("status").GetString() == "SUPPLIED"
            && item.GetProperty("ownerEntry").GetString()!.Contains("Both amendments stay OPEN"));

        var prompts = new (string Prompt, string Intent, bool Displayed)[]
        {
            ("This is my first campaign. I want to try Alpha, but I don't want to spend very much.", "ENTRY", true),
            ("We want a premium campaign with a much larger visual presence.", "PREMIUM", true),
            ("We don't want another advertiser displayed beside us.", "EXCLUSIVITY", true),
            ("Will this same advertising configuration work on mobile?", "DEVICE", false),
            ("What if I want the advertisement displayed 20 times instead of 5?", "FREQUENCY", false),
            ("How will I know my advertisement actually ran?", "DELIVERY", false),
            ("I want a configuration covering 70% of the podcast screen for $10.", "UNSUPPORTED", false)
        };
        foreach (var prompt in prompts)
        {
            var response = await client.PostAsJsonAsync("/api/operations/blueprint/ask", new { message = prompt.Prompt });
            var turn = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("turn");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(prompt.Intent, turn.GetProperty("intent").GetString());
            Assert.Equal(prompt.Displayed, turn.GetProperty("showcaseDisplayed").GetBoolean());
            Assert.False(turn.GetProperty("inventedProduct").GetBoolean());
            Assert.False(turn.GetProperty("inventedPrice").GetBoolean());
            Assert.DoesNotContain("$", turn.GetProperty("reply").GetString());
            Assert.Equal("NOT_SENT", turn.GetProperty("delivery").GetString());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var ledger = await scope.ServiceProvider.GetRequiredService<BlissDbContext>().CatalogConversations.ToListAsync();
            Assert.Equal(7, ledger.Count);
            Assert.All(ledger, row => Assert.Equal("NOT_SENT", row.Delivery));
        }

        var adapted = await client.PostAsJsonAsync(
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
        var adaptation = (await adapted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("adaptation");
        Assert.Equal("RECOMPOSED", adaptation.GetProperty("status").GetString());
        Assert.False(adaptation.GetProperty("scaledFromPrototype").GetBoolean());

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
        Assert.Contains("OPEN", joint.GetProperty("catalogAmendment").GetString());
        Assert.Contains("OPEN", joint.GetProperty("academyAmendment").GetString());
        Assert.Equal("UNCLAIMED", joint.GetProperty("hostedAcceptance").GetString());
        Assert.Equal("RETRIEVED", joint.GetProperty("retrievalStatus").GetString());
        Assert.Equal("RECOMPOSED", joint.GetProperty("adaptationStatus").GetString());
        Assert.Equal("NOT_SENT", joint.GetProperty("delivery").GetString());
        Assert.Equal(0, joint.GetProperty("modelCalls").GetInt32());
        AssertGate(joint, "CATALOG_AMENDMENT", "OPEN");
        AssertGate(joint, "ACADEMY_AMENDMENT", "OPEN");
        AssertGate(joint, "HOSTED_ACCEPTANCE", "UNCLAIMED");
        AssertGate(joint, "ECONOMICS", "NO_AUTHORIZED_PRICE");
        AssertGate(joint, "ACADEMY_RETRIEVAL", "RECORDED");
        AssertGate(joint, "INVENTORY_ADAPTATION", "RECORDED");
        AssertGate(joint, "OWNER_INPUTS", "OPEN");
        Assert.Equal(7, joint.GetProperty("conversations").GetArrayLength());
        Assert.All(joint.GetProperty("conversations").EnumerateArray(), conversation =>
        {
            Assert.True(conversation.GetProperty("integrityPassed").GetBoolean());
            Assert.False(conversation.GetProperty("inventedPrice").GetBoolean());
        });

        var voyageResponse = await client.PostAsJsonAsync(
            "/api/operations/academy/acceptance-voyages",
            new
            {
                voyageKey = "instruction-amendment-end-to-end",
                advertiserName = "Harborlight Pharmacy",
                city = "Panama City",
                market = "Panama",
                niche = "pharmacy",
                objective = "Introduce prescription pickup to local customers",
                inventoryProductId = "ARE-P01"
            });
        var voyage = (await voyageResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("voyage").GetProperty("report");
        Assert.Equal(HttpStatusCode.OK, voyageResponse.StatusCode);
        Assert.Equal("BLOCKED", voyage.GetProperty("status").GetString());
        Assert.Contains("OPEN", voyage.GetProperty("amendmentStatus").GetString());
        Assert.Equal("READY", voyage.GetProperty("referenceIntelligence").GetProperty("status").GetString());
        Assert.Equal("RECOMPOSED", voyage.GetProperty("inventoryPreflight").GetProperty("status").GetString());
        Assert.Equal("PROVIDER_CONFIGURATION_REQUIRED", voyage.GetProperty("providerJob").GetProperty("status").GetString());
        Assert.Equal("NOT_CREATED", voyage.GetProperty("finishedCreative").GetProperty("status").GetString());
        Assert.Equal("NOT_RUN", voyage.GetProperty("qualityQa").GetProperty("status").GetString());
        Assert.Equal("NOT_REQUESTED", voyage.GetProperty("humanReview").GetProperty("status").GetString());
        Assert.Equal("BASELINE_NOT_RECORDED", voyage.GetProperty("regression").GetProperty("status").GetString());
        Assert.False(voyage.GetProperty("regression").GetProperty("passed").GetBoolean());
        Assert.False(voyage.GetProperty("referenceAssetsSentToProvider").GetBoolean());
        Assert.Equal("UNCLASSIFIED", voyage.GetProperty("storedQuality")[0].GetProperty("status").GetString());
        Assert.All(
            voyage.GetProperty("provenance").EnumerateArray(),
            item => Assert.Equal("NOT_AUTHORIZED", item.GetProperty("permittedProviderUse").GetString()));
        Assert.Equal(0, voyage.GetProperty("modelCalls").GetInt32());
        Assert.False(voyage.GetProperty("campaignReady").GetBoolean());
        Assert.Equal("NOT_SENT", voyage.GetProperty("delivery").GetString());
        Assert.Contains(voyage.GetProperty("blockers").EnumerateArray(), item => item.GetString() == "PROVIDER_CONFIGURATION_REQUIRED");
        Assert.Contains(voyage.GetProperty("blockers").EnumerateArray(), item => item.GetString() == "VISUAL_QUALITY_QA_REQUIRED");
        Assert.Contains(voyage.GetProperty("blockers").EnumerateArray(), item => item.GetString() == "HUMAN_REVIEW_REQUIRED");
        Assert.Contains(voyage.GetProperty("blockers").EnumerateArray(), item => item.GetString() == "USAGE_COST_UNRECORDED");
        Assert.DoesNotContain(voyage.GetProperty("blockers").EnumerateArray(), item => item.GetString() == "INVENTORY_GEOMETRY_REQUIRED");
    }

    private static void AssertReference(
        IReadOnlyList<JsonElement> references,
        string referenceId,
        string fileName,
        string nicheKey,
        string note)
    {
        var reference = references.Single(item => item.GetProperty("referenceId").GetString() == referenceId);
        Assert.Equal(fileName, reference.GetProperty("expectedFile").GetString());
        Assert.Equal(nicheKey, reference.GetProperty("nicheKey").GetString());
        Assert.Contains(note, reference.GetProperty("doNotCopy").GetString() + reference.GetProperty("mappingNote").GetString());
    }

    private static void AssertGate(JsonElement joint, string gateId, string status)
    {
        var gate = joint.GetProperty("gates").EnumerateArray().Single(item => item.GetProperty("gateId").GetString() == gateId);
        Assert.Equal(status, gate.GetProperty("status").GetString());
    }
}
