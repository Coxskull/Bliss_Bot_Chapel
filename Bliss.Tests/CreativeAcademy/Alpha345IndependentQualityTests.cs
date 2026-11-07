using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.CreativeAcademy;

public sealed class Alpha345IndependentQualityTests
{
    [Fact]
    public void New_fictional_brand_requires_independent_brand_and_reference_craft_evidence()
    {
        // Fictional "LumiLokal" market PH, authorized brief colors navy/gold,
        // approved Tagalog copy. These are synthetic review signals, NOT
        // claims that an actual uploaded photograph passed human visual QA.
        var brand = new BrandComplianceEvidence(
            ApprovedBrandSnapshot: true, ApprovedLogoRights: true,
            ColorsMatch: true, CopyMatches: true, TypographyMatches: true,
            ProductsMatch: true, LanguageMatches: true, ClaimsSubstantiated: true,
            MediaRightsConfirmed: true);
        var quality = new ReferenceQualityEvidence(
            HumanVisualReviewRecorded: true, SourceArtworkPresent: true,
            OriginalComposition: true, CommercialPhotography: true,
            Lighting: true, RichColorContrast: true, HighlightShadowControl: true,
            DimensionalDepth: true, MaterialRealism: true, TextureFidelity: true,
            HeroProductDominance: true, VisualHierarchy: true,
            LegibleTypography: true, PremiumPolish: true);
        var locality = new LocalizationEvidence(
            "PH", MarketResearchReviewed: true, AdvertiserApprovedLanguage: true,
            AdvertiserApprovedSetting: true, CastingRightsReviewed: true);

        var dual = GlobalVisualDnaGate.EvaluateIndependent(brand, quality, locality);
        Assert.Equal("PASS", dual.BrandComplianceStatus);
        Assert.Equal("PASS", dual.ReferenceQualityStatus);
        Assert.Equal("PASS", dual.LocalizationStatus);
        Assert.True(dual.EligibleForAdvertiserReview);
        Assert.False(dual.CampaignReady);
        Assert.Equal("NOT_SENT", dual.Delivery);
        Assert.Equal(0, dual.ModelCalls);

        var wrongBrand = GlobalVisualDnaGate.EvaluateIndependent(
            brand with { ColorsMatch = false }, quality, locality);
        Assert.Equal("FAIL", wrongBrand.BrandComplianceStatus);
        Assert.Equal("PASS", wrongBrand.ReferenceQualityStatus);
        Assert.False(wrongBrand.EligibleForAdvertiserReview);

        var unreviewed = GlobalVisualDnaGate.EvaluateIndependent(
            brand, quality with { HumanVisualReviewRecorded = false }, locality);
        Assert.Equal("PASS", unreviewed.BrandComplianceStatus);
        Assert.Equal("WITHHELD", unreviewed.ReferenceQualityStatus);
        Assert.False(unreviewed.EligibleForAdvertiserReview);

        var cloned = GlobalVisualDnaGate.EvaluateIndependent(
            brand, quality with { OriginalComposition = false }, locality);
        Assert.Equal("FAIL", cloned.ReferenceQualityStatus);
        Assert.False(cloned.EligibleForAdvertiserReview);

        var unverifiedMarket = GlobalVisualDnaGate.EvaluateIndependent(
            brand, quality,
            locality with { CountryCode = "ID", MarketResearchReviewed = false });
        Assert.Equal("LOCALIZATION_REVIEW_REQUIRED", unverifiedMarket.LocalizationStatus);
        Assert.False(unverifiedMarket.EligibleForAdvertiserReview);

        var wrongMarket = GlobalVisualDnaGate.EvaluateIndependent(
            brand, quality, locality with { CountryCode = "ZZ" });
        Assert.Equal("LOCALIZATION_REVIEW_REQUIRED", wrongMarket.LocalizationStatus);
        Assert.False(wrongMarket.EligibleForAdvertiserReview);

        var rightsUnknown = GlobalVisualDnaGate.EvaluateIndependent(
            brand with { MediaRightsConfirmed = false }, quality, locality);
        Assert.Equal("FAIL", rightsUnknown.BrandComplianceStatus);
        Assert.False(rightsUnknown.EligibleForAdvertiserReview);
    }
}
