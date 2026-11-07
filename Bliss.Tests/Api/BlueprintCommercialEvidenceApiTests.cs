using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Bliss.Tests.Api;

/// <summary>
/// ALPHA-345: actual existing catalog rows + physical showcase bytes + seven
/// real ASP.NET Ask Alpha calls on a disposable, in-memory test database.
/// These tests must NOT assert a real creator permission or price exists.
/// </summary>
public sealed class BlueprintCommercialEvidenceApiTests(ITestOutputHelper testOutput)
{
    [Fact]
    public async Task Seven_ask_alpha_conversations_retrieve_existing_rows_and_images_without_selling()
    {
        await using var factory = new BlissApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            // Synthetic ACTIVE fixture rows in the existing canonical table.
            // The static owner file alone cannot promote any of these to a sale.
            var rows = new (string Id, string Tier, string Exclusive, int Capacity, string Slots, string Showcase)[]
            {
                ("ARE-P01", "PREMIUM", "EXCLUSIVE_WHEN_AUTHORIZED", 1,
                    "LEFT_VERTICAL,BOTTOM_FULL", "ARE-001-V1"),
                ("ARE-P02", "PREMIUM", "SHARED", 2,
                    "LEFT_VERTICAL,BOTTOM_LEFT,RIGHT_VERTICAL,BOTTOM_RIGHT", "ARE-002-V1"),
                ("ARE-S01", "ENTRY", "SHARED", 4,
                    "TOP_LEFT,TOP_RIGHT,BOTTOM_LEFT,BOTTOM_RIGHT", "ARE-003-V1"),
                ("ARE-E01", "EXCLUSIVE", "EXCLUSIVE", 1,
                    "LEFT_VERTICAL,BOTTOM_FULL", "ARE-001-V1")
            };
            foreach (var row in rows)
            {
                if (await db.RealEstateProducts.AnyAsync(item => item.ProductId == row.Id))
                    continue;
                db.RealEstateProducts.Add(new RealEstateProductRow
                {
                    ProductId = row.Id, Version = "V1", Tier = row.Tier,
                    Exclusivity = row.Exclusive, MaximumAdvertisers = row.Capacity,
                    SlotIds = row.Slots, ShowcaseId = row.Showcase,
                    Lifecycle = "ACTIVE", OccupancyStatus = "UNRECORDED",
                    DeviceStatus = "NEEDS_REVIEW", PlatformStatus = "NEEDS_REVIEW"
                });
            }
            await db.SaveChangesAsync();
        }

        var cases = new (string Message, string Intent, bool HasCandidates)[]
        {
            ("This is my first campaign. I want to try Alpha, but I don't want to spend very much.", "ENTRY", true),
            ("We want a premium campaign with a much larger visual presence.", "PREMIUM", true),
            ("We don't want another advertiser displayed beside us.", "EXCLUSIVITY", true),
            ("Will this same advertising configuration work on mobile?", "DEVICE", false),
            ("What if I want the advertisement displayed 20 times instead of 5?", "FREQUENCY", false),
            ("How will I know my advertisement actually ran?", "DELIVERY", false),
            ("I want a configuration covering 70% of the podcast screen for $10.", "UNSUPPORTED", false)
        };

        foreach (var (message, intent, expectsCandidates) in cases)
        {
            var response = await client.PostAsJsonAsync("/api/operations/blueprint/ask-evidence", new { message });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var evidence = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("INSPECTION_ONLY_NOT_A_SELLABLE_OFFER", evidence.GetProperty("status").GetString());
            Assert.Equal("public.RealEstateProducts / existing showcase files; candidate records are never reservations",
                evidence.GetProperty("inventorySource").GetString());
            var turn = evidence.GetProperty("turn");
            Assert.Equal(intent, turn.GetProperty("intent").GetString());
            Assert.False(turn.GetProperty("inventedPrice").GetBoolean());
            Assert.False(turn.GetProperty("inventedProduct").GetBoolean());
            Assert.False(turn.GetProperty("showcaseDisplayed").GetBoolean());
            Assert.True(turn.GetProperty("humanEscalation").GetBoolean());
            Assert.False(evidence.GetProperty("offerEligible").GetBoolean());
            Assert.False(evidence.GetProperty("campaignReady").GetBoolean());
            Assert.Equal("NOT_SENT", evidence.GetProperty("delivery").GetString());
            Assert.Equal(0, evidence.GetProperty("modelCalls").GetInt32());
            Assert.DoesNotContain("$10", turn.GetProperty("reply").GetString());

            var candidates = evidence.GetProperty("candidates").EnumerateArray().ToArray();
            if (expectsCandidates)
                Assert.NotEmpty(candidates);
            else
                Assert.Empty(candidates);

            foreach (var candidate in candidates)
            {
                Assert.Equal("ACTIVE", candidate.GetProperty("lifecycle").GetString());
                Assert.Equal("V1", candidate.GetProperty("version").GetString());
                Assert.Equal("CREATOR_SPECIFIC_AUTHORIZATION_MISSING",
                    candidate.GetProperty("creatorAuthorizationStatus").GetString());
                Assert.Equal("PRODUCT_SLOT_AVAILABILITY_UNVERIFIED",
                    candidate.GetProperty("availabilityStatus").GetString());
                Assert.Equal("PRICING_UNAVAILABLE",
                    candidate.GetProperty("economicsPricingStatus").GetString());
                Assert.Equal("DURATION_FREQUENCY_UNVERIFIED",
                    candidate.GetProperty("scheduleStatus").GetString());
                Assert.False(candidate.GetProperty("offerEligible").GetBoolean());
                Assert.True(candidate.GetProperty("showcaseAssetRetrieved").GetBoolean());

                var url = candidate.GetProperty("showcaseImageUrl").GetString();
                Assert.NotNull(url);
                var bytes = await client.GetByteArrayAsync(url);
                Assert.True(bytes.Length > 1000);
                var actualSha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                Assert.Equal(actualSha, candidate.GetProperty("showcaseSha256").GetString());
                Assert.NotEmpty(candidate.GetProperty("ownerRecommendedSlots").EnumerateArray());
                var proof = candidate.GetProperty("geometryProof");
                Assert.Equal("PROTECTED_CREATOR_REGION_UNVERIFIED",
                    proof.GetProperty("status").GetString());
                Assert.False(proof.GetProperty("declaredCreatorRegionProtected").GetBoolean());
                Assert.True(proof.GetProperty("unionAllocatedPixels").GetInt64() > 0);
                Assert.True(proof.GetProperty("frameOccupancyBasisPoints").GetInt32() > 0);
            }
            testOutput.WriteLine("ALPHA345_ASK_RECEIPT " + JsonSerializer.Serialize(new
            {
                intent,
                advertiserQuestion = message,
                retrievedProductIds = candidates.Select(x => x.GetProperty("productId").GetString()).ToArray(),
                showcaseAssetReceipts = candidates.Select(x => new
                {
                    id = x.GetProperty("showcaseId").GetString(),
                    sha256 = x.GetProperty("showcaseSha256").GetString(),
                    frameOccupancyBasisPoints = x.GetProperty("geometryProof").GetProperty("frameOccupancyBasisPoints").GetInt32()
                }).ToArray(),
                humanEscalation = turn.GetProperty("humanEscalation").GetBoolean(),
                offerEligible = evidence.GetProperty("offerEligible").GetBoolean(),
                pricing = evidence.GetProperty("economicsNotice").GetString(),
                creatorAuthorization = evidence.GetProperty("creatorAuthorizationNotice").GetString(),
                answer = turn.GetProperty("reply").GetString(),
                delivery = evidence.GetProperty("delivery").GetString()
            }));
        }

        // The new inspection endpoint cannot create a product, quote, consent,
        // reservation or conversation ledger row; it is read-only by design.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
            Assert.Equal(4, await db.RealEstateProducts.CountAsync());
            Assert.Equal(0, await db.CatalogConversations.CountAsync());
            Assert.Equal(0, await db.Quotes.CountAsync());
            Assert.Equal(0, await db.CampaignPlacements.CountAsync());
        }
    }

    [Fact]
    public async Task Reading_the_legacy_catalog_must_not_create_or_activate_products()
    {
        await using var factory = new BlissApiFactory();
        using var client = factory.CreateClient();
        var board = await client.GetAsync("/api/operations/blueprint");
        Assert.Equal(HttpStatusCode.OK, board.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        Assert.Empty(await db.RealEstateProducts.ToListAsync());
        Assert.Empty(await db.CatalogConversations.ToListAsync());
    }

    [Fact]
    public async Task No_stored_products_means_no_creator_approved_inventory_even_if_owner_file_says_true()
    {
        await using var factory = new BlissApiFactory();
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/operations/blueprint/ask-evidence",
            new { message = "I want a premium advertisement" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("candidates").EnumerateArray());
        Assert.False(body.GetProperty("offerEligible").GetBoolean());

        var invalid = await client.PostAsJsonAsync("/api/operations/blueprint/ask-evidence",
            new { message = new string('a', 2001) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
