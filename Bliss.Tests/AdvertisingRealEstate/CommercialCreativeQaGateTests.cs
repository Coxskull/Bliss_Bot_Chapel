using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;

namespace Bliss.Tests.AdvertisingRealEstate;

public sealed class CommercialCreativeQaGateTests
{
    private static readonly string CandidateSha = new('a', 64);
    private static readonly string ReferenceSha = new('b', 64);
    private static readonly Guid BrandId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static WeddingPlannerBrandDna Dna() => new()
    {
        Id = BrandId, WorkspaceId = WorkspaceId, Version = 3, Status = "CONFIRMED",
        BrandName = "Fictional Orchid Coffee", Niche = "coffee", Market = "PH",
        Language = "fil", Colors = "#143A38 and #FFE4B5", Font = "Example Sans",
        Goals = "Synthetic campaign test", Restrictions = "No unsupported health claims",
        ActorLabel = "FICTIONAL_REVIEWER"
    };

    private static CommercialBrandReviewProof Brand() =>
        new(BrandId, 3, CandidateSha, "Fictional Orchid Coffee",
            "#143A38 and #FFE4B5", "Example Sans", "fil",
            true, true, true, "fictional-brand-reviewer", "synthetic://brand-qa", "BRANDQA-V3");

    private static AcademyReferenceRecord Reference() =>
        new("FICTIONAL-QUALITY-REF-01", 1, "coffee", "Coffee", "ref.png",
            ReferenceLibrary.Active, ReferenceLibrary.Uploaded, true,
            "Study lighting, color and dimensional craftsmanship",
            "Do not copy the reference logo, composition, imagery or people",
            ProvenanceSource: "synthetic://not-real-source",
            Ownership: "FICTIONAL_RIGHTS",
            ApprovingAuthority: "FICTIONAL_REFERENCE_REVIEWER",
            PermittedInternalUse: "SYNTHETIC_TEST_ONLY");

    private static CommercialQualityReviewProof Quality() =>
        new(CandidateSha, "FICTIONAL-QUALITY-REF-01", "REF-V1", ReferenceSha,
            CreativeAcademy.ReferenceParityGate.ToDictionary(x => x, _ => true, StringComparer.Ordinal),
            99, [], true, true, true, true,
            "fictional-quality-reviewer", "synthetic://quality-inspection");

    private static CommercialLocalizationReviewProof Locale() =>
        new("PH", "fil", "synthetic://locale-review", "fictional-localizer", true, true, true);

    private static CommercialPolicyReviewProof Policy() =>
        new("synthetic://policy-review", "fictional-policy-reviewer",
            true, true, true, true, true, true, true);

    private static CommercialGeometryEvidence Geometry() =>
        new("GEOMETRY_VALIDATED", "G1", 576000L, 2073600L, 2778, 5000, []);

    private static CommercialCreativeQaDecision Run(
        WeddingPlannerBrandDna? dna = null, CommercialBrandReviewProof? brand = null,
        AcademyReferenceRecord? reference = null, CommercialQualityReviewProof? quality = null,
        CommercialLocalizationReviewProof? locale = null, CommercialPolicyReviewProof? policy = null,
        CommercialGeometryEvidence? geometry = null)
        => CommercialCreativeQaGate.Evaluate(dna ?? Dna(), brand ?? Brand(),
            reference ?? Reference(), quality ?? Quality(),
            locale ?? Locale(), policy ?? Policy(), geometry ?? Geometry());

    [Fact]
    public void Fictional_double_pass_is_not_live_approval_or_delivered_impression()
    {
        var result = Run();
        Assert.Equal("OFFLINE_SYNTHETIC_ONLY", result.Mode);
        Assert.Equal(CommercialCreativeQaGate.Passed, result.BrandStatus);
        Assert.Equal(CommercialCreativeQaGate.Passed, result.QualityStatus);
        Assert.Equal(CommercialCreativeQaGate.Passed, result.LocalizationStatus);
        Assert.Equal(CommercialCreativeQaGate.Passed, result.PolicyStatus);
        Assert.True(result.SyntheticCandidateEligibleForAdvertiserReview);
        Assert.False(result.ExternalUseAuthorized);
        Assert.False(result.CampaignReady);
        Assert.Equal("NOT_SENT", result.Delivery);
    }

    [Fact]
    public void T14_quality_pass_does_not_overrule_brand_failure()
    {
        var result = Run(brand: Brand() with { LogoRightsVerified = false });
        Assert.Equal(CommercialCreativeQaGate.Blocked, result.BrandStatus);
        Assert.Equal(CommercialCreativeQaGate.Passed, result.QualityStatus);
        Assert.False(result.SyntheticCandidateEligibleForAdvertiserReview);
        Assert.False(result.ExternalUseAuthorized);
    }

    [Fact]
    public void T15_brand_pass_does_not_overrule_reference_quality_failure()
    {
        var dimensions = Quality().ParityDimensions.ToDictionary(x => x.Key, x => x.Value);
        dimensions["lighting craftsmanship"] = false;
        var result = Run(quality: Quality() with { ParityDimensions = dimensions });
        Assert.Equal(CommercialCreativeQaGate.Passed, result.BrandStatus);
        Assert.Equal(CommercialCreativeQaGate.Blocked, result.QualityStatus);
        Assert.False(result.SyntheticCandidateEligibleForAdvertiserReview);
        Assert.Contains("REFERENCE_QUALITY_OR_ORIGINALITY_REVIEW_MISSING_OR_FAILED", result.Findings);
    }

    [Fact]
    public void Reference_clone_and_missing_originality_are_refused_even_at_perfect_score()
    {
        var result = Run(quality: Quality() with
        {
            ReportedScore = 100, OriginalityConfirmed = false, DoNotCopyConfirmed = false
        });
        Assert.Equal(CommercialCreativeQaGate.Blocked, result.QualityStatus);
        Assert.False(result.SyntheticCandidateEligibleForAdvertiserReview);
        Assert.False(result.ExternalUseAuthorized);
    }

    [Fact]
    public void Unsigned_or_draft_WeddingPlanner_Brand_Dna_cannot_establish_brand_rights()
    {
        var draft = Run(dna: new WeddingPlannerBrandDna
        {
            Id = BrandId, WorkspaceId = WorkspaceId, Version = 3, Status = "DRAFT",
            BrandName = "Fictional Orchid Coffee",
            Colors = "#143A38 and #FFE4B5", Font = "Example Sans", Language = "fil"
        });
        Assert.Equal(CommercialCreativeQaGate.Blocked, draft.BrandStatus);
        Assert.Equal(CommercialCreativeQaGate.Passed, draft.QualityStatus);

        var wrongVersion = Run(brand: Brand() with { BrandDnaVersion = 4 });
        Assert.Equal(CommercialCreativeQaGate.Blocked, wrongVersion.BrandStatus);

        var unverifiedCopy = Run(brand: Brand() with { ApprovedCopyOnly = false });
        Assert.Equal(CommercialCreativeQaGate.Blocked, unverifiedCopy.BrandStatus);
    }

    [Fact]
    public void Candidate_digest_mismatch_blocks_both_independent_qa_gates()
    {
        var result = Run(quality: Quality() with { CandidateSha256 = new string('c', 64) });
        Assert.Equal(CommercialCreativeQaGate.Blocked, result.BrandStatus);
        Assert.Equal(CommercialCreativeQaGate.Blocked, result.QualityStatus);
        Assert.False(result.SyntheticCandidateEligibleForAdvertiserReview);
    }

    [Fact]
    public void Inactive_or_unproven_reference_assets_are_not_promoted_by_a_high_score()
    {
        var inactive = Run(reference: Reference() with { Lifecycle = ReferenceLibrary.Candidate });
        Assert.Equal(CommercialCreativeQaGate.Blocked, inactive.QualityStatus);
        var noEvidence = Run(reference: Reference() with { ProvenanceSource = "" });
        Assert.Equal(CommercialCreativeQaGate.Blocked, noEvidence.QualityStatus);
    }

    [Fact]
    public void T16_unsupported_claim_or_missing_sponsorship_disclosure_blocks_review()
    {
        var claim = Run(policy: Policy() with { ClaimsSubstantiated = false });
        Assert.Equal(CommercialCreativeQaGate.Review, claim.PolicyStatus);
        Assert.False(claim.SyntheticCandidateEligibleForAdvertiserReview);
        Assert.Equal(CommercialCreativeQaGate.Passed, claim.BrandStatus);
        Assert.Equal(CommercialCreativeQaGate.Passed, claim.QualityStatus);

        var disclosure = Run(policy: Policy() with { SponsorshipDisclosureVerified = false });
        Assert.False(disclosure.SyntheticCandidateEligibleForAdvertiserReview);
        Assert.Equal("NOT_SENT", disclosure.Delivery);
    }

    [Theory]
    [InlineData("PH", "fil", true)]
    [InlineData("DO", "es", true)]
    [InlineData("PA", "es", true)]
    [InlineData("CO", "es", true)]
    [InlineData("MY", "ms", true)]
    [InlineData("ID", "id", true)]
    [InlineData("ZZ", "en", false)]
    public void T17_only_declared_markets_with_a_reviewed_matching_language_are_eligible(
        string market, string language, bool supported)
    {
        var dna = Dna();
        dna.Language = language;
        var result = Run(dna: dna, locale: Locale() with { CountryCode = market, Language = language });
        Assert.Equal(supported, result.LocalizationStatus == CommercialCreativeQaGate.Passed);
        Assert.False(result.ExternalUseAuthorized);
    }

    [Fact]
    public void Market_evidence_never_fabricates_cultural_approval_from_country_or_casting()
    {
        var missingReview = Run(locale: Locale() with { CastingRightsAndRepresentationReviewed = false });
        Assert.Equal(CommercialCreativeQaGate.Review, missingReview.LocalizationStatus);
        Assert.False(missingReview.SyntheticCandidateEligibleForAdvertiserReview);
    }

    [Fact]
    public void Creator_inventory_geometry_is_a_separate_required_gate()
    {
        var blocked = Run(geometry: Geometry() with { Status = "CREATOR_CONTENT_OVERLAP" });
        Assert.Equal(CommercialCreativeQaGate.Review, blocked.GeometryStatus);
        Assert.False(blocked.SyntheticCandidateEligibleForAdvertiserReview);
        Assert.False(blocked.ExternalUseAuthorized);
    }
}
