using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Alpha345;

public sealed class CommercialAuthorityBoundaryTests
{
    [Fact]
    public async Task Inactive_or_metadata_conflicting_catalog_rows_cannot_become_offers()
    {
        await using var app = new Bliss.Tests.Api.BlissApiFactory();
        using var client = app.CreateClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        var product = new RealEstateProductRow
        {
            ProductId = "ARE-P01", Version = "V1", Tier = "PREMIUM",
            Exclusivity = "EXCLUSIVE_WHEN_AUTHORIZED", MaximumAdvertisers = 1,
            SlotIds = "LEFT_VERTICAL,BOTTOM_FULL", ShowcaseId = "ARE-001-V1",
            Lifecycle = "DRAFT"
        };
        db.RealEstateProducts.Add(product);
        await db.SaveChangesAsync();

        async Task<JsonElement> Ask()
        {
            var response = await client.PostAsJsonAsync("/api/operations/blueprint/ask-evidence",
                new { message = "I want a premium advertisement with more screen space" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        var inactive = await Ask();
        Assert.Empty(inactive.GetProperty("candidates").EnumerateArray());
        Assert.False(inactive.GetProperty("offerEligible").GetBoolean());

        product.Lifecycle = "ACTIVE";
        product.ShowcaseId = "UNREGISTERED-ILLUSTRATION";
        await db.SaveChangesAsync();
        var mismatched = await Ask();
        Assert.Empty(mismatched.GetProperty("candidates").EnumerateArray());
        Assert.False(mismatched.GetProperty("offerEligible").GetBoolean());

        product.ShowcaseId = "ARE-001-V1";
        product.SlotIds = "RIGHT_VERTICAL"; // Wrong inventory configuration.
        await db.SaveChangesAsync();
        var wrongSlots = await Ask();
        Assert.Empty(wrongSlots.GetProperty("candidates").EnumerateArray());

        Assert.Equal(1, await db.RealEstateProducts.CountAsync());
        Assert.Equal(0, await db.CatalogConversations.CountAsync());
        Assert.Equal(0, await db.Quotes.CountAsync());
    }

    [Fact]
    public void Unsubstantiated_ad_claim_fails_brand_axis_even_with_synthetic_premium_visual_scores()
    {
        var brand = new BrandComplianceEvidence(true, true, true, true, true, true, true,
            ClaimsSubstantiated: false, MediaRightsConfirmed: true);
        var quality = new ReferenceQualityEvidence(
            true, true, true, true, true, true, true, true, true, true, true, true, true, true);
        var locality = new LocalizationEvidence("DO", true, true, true, true);
        var review = GlobalVisualDnaGate.EvaluateIndependent(brand, quality, locality);
        Assert.Equal("FAIL", review.BrandComplianceStatus);
        Assert.Equal("PASS", review.ReferenceQualityStatus);
        Assert.False(review.EligibleForAdvertiserReview);
        Assert.False(review.CampaignReady);
        Assert.Equal("NOT_SENT", review.Delivery);
    }
}
