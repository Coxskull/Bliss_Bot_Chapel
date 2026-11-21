using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.Entities;

namespace Bliss.Domain.CreativeAcademy;

/// <summary>
/// Version-bound evidence supplied for an OFFLINE fictional quality review.
/// A submitted Boolean is never itself a signed live approval.
/// </summary>
public sealed record CommercialBrandReviewProof(
    Guid BrandDnaId, int BrandDnaVersion, string CandidateSha256,
    string BrandName, string Palette, string Typography, string Language,
    bool LogoRightsVerified, bool ApprovedCopyOnly, bool RestrictionsReviewed,
    string Reviewer, string ReviewReference, string ReviewVersion);

public sealed record CommercialQualityReviewProof(
    string CandidateSha256, string ReferenceId, string ReferenceVersion,
    string ReferenceSha256, IReadOnlyDictionary<string, bool> ParityDimensions,
    int ReportedScore, IReadOnlyList<string> DefectCodes,
    bool VisualEvidenceRecorded, bool OriginalityConfirmed,
    bool DoNotCopyConfirmed, bool QrVerified,
    string Reviewer, string ReviewReference);

public sealed record CommercialLocalizationReviewProof(
    string CountryCode, string Language, string ReviewReference,
    string Reviewer, bool LocalLanguageApproved,
    bool MarketContextVerified, bool CastingRightsAndRepresentationReviewed);

public sealed record CommercialPolicyReviewProof(
    string ReviewReference, string Reviewer,
    bool AdvertiserAssetRightsConfirmed, bool ClaimsSubstantiated,
    bool SectorRestrictionsReviewed, bool SponsorshipDisclosureVerified,
    bool CreatorApproved, bool PlatformPolicyVerified, bool FormatValidated);

public sealed record CommercialCreativeQaDecision(
    string Mode, string BrandStatus, string QualityStatus,
    string LocalizationStatus, string PolicyStatus,
    string GeometryStatus, bool SyntheticCandidateEligibleForAdvertiserReview,
    bool ExternalUseAuthorized, bool CampaignReady, string Delivery,
    string CandidateSha256, string BrandDnaVersion, string ReferenceId,
    IReadOnlyList<string> Findings, string Notice);

/// <summary>
/// Independent, fail-closed gates over EXISTING Wedding Planner Brand DNA,
/// existing Creative Academy quality dimensions/reference library, existing
/// GlobalVisualDnaGate and existing Advertising Real Estate geometry.
/// Offline fixture approval NEVER means real advertiser or creator approval.
/// </summary>
public static class CommercialCreativeQaGate
{
    public const string Passed = "PASS_SYNTHETIC";
    public const string Blocked = "FAIL";
    public const string Review = "REVIEW_REQUIRED";

    private static readonly IReadOnlySet<string> Markets = new HashSet<string>(
        ["DO", "PA", "CO", "PH", "MY", "ID"], StringComparer.Ordinal);

    public static CommercialCreativeQaDecision Evaluate(
        WeddingPlannerBrandDna? brandDna,
        CommercialBrandReviewProof? brandReview,
        AcademyReferenceRecord? reference,
        CommercialQualityReviewProof? qualityReview,
        CommercialLocalizationReviewProof? localeReview,
        CommercialPolicyReviewProof? policyReview,
        CommercialGeometryEvidence? geometry)
    {
        var findings = new List<string>();
        var candidate = brandReview?.CandidateSha256 ?? qualityReview?.CandidateSha256 ?? "";
        var brandDigestValid = IsSha256(brandReview?.CandidateSha256);
        var qualityDigestValid = IsSha256(qualityReview?.CandidateSha256);
        var sameCandidate = brandDigestValid && qualityDigestValid
            && string.Equals(brandReview!.CandidateSha256, qualityReview!.CandidateSha256,
                StringComparison.OrdinalIgnoreCase);
        if (!sameCandidate) findings.Add("CANDIDATE_ASSET_IDENTITY_MISMATCH");

        // The real Wedding Planner intake uses CONFIRMED, not invented "APPROVED".
        // Confirmation of a brief is NOT sufficient: rights/reviewer evidence
        // must also be present for the separate BRAND gate to pass.
        var brandPass = brandDna is not null && brandReview is not null && brandDigestValid
            && brandDna.Id != Guid.Empty && brandDna.WorkspaceId != Guid.Empty
            && brandDna.Status == "CONFIRMED" && brandDna.Version > 0
            && brandReview.BrandDnaId == brandDna.Id
            && brandReview.BrandDnaVersion == brandDna.Version
            && Equal(brandReview.BrandName, brandDna.BrandName)
            && Equal(brandReview.Palette, brandDna.Colors)
            && Equal(brandReview.Typography, brandDna.Font)
            && Equal(brandReview.Language, brandDna.Language)
            && brandReview.LogoRightsVerified && brandReview.ApprovedCopyOnly
            && brandReview.RestrictionsReviewed
            && Evidence(brandReview.ReviewReference, brandReview.Reviewer, brandReview.ReviewVersion);
        if (!brandPass) findings.Add("BRAND_COMPLIANCE_EVIDENCE_MISSING_OR_FAILED");

        // Reference artwork is a quality teacher, NEVER the advertiser brand.
        // Require explicit identity, owner provenance, a human do-not-copy note
        // and a full, individually inspected CreativeAcademy parity signature.
        var qualityPass = reference is not null && qualityReview is not null && qualityDigestValid
            && reference.Lifecycle == ReferenceLibrary.Active && reference.AssetPresent
            && !string.IsNullOrWhiteSpace(reference.Learn)
            && !string.IsNullOrWhiteSpace(reference.DoNotCopy)
            && !string.IsNullOrWhiteSpace(reference.ProvenanceSource)
            && !string.IsNullOrWhiteSpace(reference.ApprovingAuthority)
            && reference.ReferenceId == qualityReview.ReferenceId
            && Evidence(qualityReview.ReferenceVersion, qualityReview.Reviewer, qualityReview.ReviewReference)
            && IsSha256(qualityReview.ReferenceSha256)
            && qualityReview.ParityDimensions is not null
            && qualityReview.ParityDimensions.Count == CreativeAcademy.ReferenceParityGate.Count
            && CreativeAcademy.ReferenceParityGate.All(name =>
                qualityReview.ParityDimensions.TryGetValue(name, out var passed) && passed)
            && qualityReview.ReportedScore is >= 0 and <= 100
            && qualityReview.VisualEvidenceRecorded
            && qualityReview.OriginalityConfirmed && qualityReview.DoNotCopyConfirmed
            && qualityReview.QrVerified && qualityReview.DefectCodes is not null
            && qualityReview.DefectCodes.Count == 0;
        if (!qualityPass) findings.Add("REFERENCE_QUALITY_OR_ORIGINALITY_REVIEW_MISSING_OR_FAILED");

        // Existing GlobalVisualDnaGate is an additional hard stop; it does
        // not manufacture positive source evidence or waive missing proofs.
        if (qualityPass)
        {
            var existing = GlobalVisualDnaGate.Evaluate(
                qualityReview!.ReportedScore, qualityReview.DefectCodes,
                qualityReview.VisualEvidenceRecorded, true,
                qualityReview.OriginalityConfirmed, true, qualityReview.QrVerified);
            qualityPass = existing.Status == GlobalVisualDnaGate.Pass;
        }

        var localizationPass = localeReview is not null
            && Markets.Contains(localeReview.CountryCode)
            && Evidence(localeReview.Language, localeReview.Reviewer, localeReview.ReviewReference)
            && brandDna is not null && Equal(localeReview.Language, brandDna.Language)
            && Equal(localeReview.CountryCode, brandDna.Market)
            && localeReview.LocalLanguageApproved && localeReview.MarketContextVerified
            && localeReview.CastingRightsAndRepresentationReviewed;
        if (!localizationPass) findings.Add("LOCALIZATION_REVIEW_REQUIRED");

        var policyPass = policyReview is not null
            && Evidence(policyReview.ReviewReference, policyReview.Reviewer, "V1")
            && policyReview.AdvertiserAssetRightsConfirmed
            && policyReview.ClaimsSubstantiated && policyReview.SectorRestrictionsReviewed
            && policyReview.SponsorshipDisclosureVerified && policyReview.CreatorApproved
            && policyReview.PlatformPolicyVerified && policyReview.FormatValidated;
        if (!policyPass) findings.Add("CREATIVE_POLICY_APPROVAL_REQUIRED");

        var geometryPass = geometry?.Validated == true
            && !string.IsNullOrWhiteSpace(geometry.GeometryVersion);
        if (!geometryPass) findings.Add("INVENTORY_GEOMETRY_REVIEW_REQUIRED");

        var candidateEligible = brandPass && qualityPass && sameCandidate && localizationPass
            && policyPass && geometryPass;
        return new CommercialCreativeQaDecision(
            "OFFLINE_SYNTHETIC_ONLY",
            brandPass ? Passed : Blocked,
            qualityPass ? Passed : Blocked,
            localizationPass ? Passed : Review,
            policyPass ? Passed : Review,
            geometryPass ? Passed : Review,
            candidateEligible,
            false, false, "NOT_SENT", candidate,
            brandDna is null ? "UNVERIFIED" : brandDna.Id + "/" + brandDna.Version,
            reference?.ReferenceId ?? "UNVERIFIED", findings,
            candidateEligible
                ? "FICTIONAL quality-and-brand evidence passes two independent gates; advertiser review is ONLY simulated. No real asset use, release, purchase or delivery is authorized."
                : "One or more independent Brand DNA, quality parity, locale, policy or inventory gates withheld this fictional candidate. No release or delivery is authorized.");
    }

    private static bool Evidence(string? a, string? b, string? c) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
        && !string.IsNullOrWhiteSpace(c);

    private static bool Equal(string? x, string? y) =>
        !string.IsNullOrWhiteSpace(x) && !string.IsNullOrWhiteSpace(y)
        && string.Equals(x.Trim(), y.Trim(), StringComparison.Ordinal);

    private static bool IsSha256(string? value) =>
        value is not null && value.Length == 64 && value.All(Uri.IsHexDigit);
}
