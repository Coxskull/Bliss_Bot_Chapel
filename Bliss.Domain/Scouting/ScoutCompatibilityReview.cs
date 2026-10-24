namespace Bliss.Domain.Scouting;

/// <summary>
/// A source-backed prospect supplied for a REVIEW ONLY compatibility preview.
/// These fields are assertions by the caller, not proof of verification by Bliss.
/// </summary>
public sealed record ScoutReviewProspect(
    string RecordId,
    string Name,
    string CountryCode,
    string City,
    string Category,
    string PublicSourceUrl,
    string VerificationMethod,
    string VerificationStatus,
    string GeographicVerification,
    string EvidenceId,
    string? Language = null,
    string? ContentFormat = null);

public sealed record ScoutReviewPair(ScoutReviewProspect Creator, ScoutReviewProspect Advertiser);

public sealed record ScoutCompatibilityFinding(
    string CreatorRecordId,
    string AdvertiserRecordId,
    string ReviewStatus,
    string GeographicCompatibility,
    string LanguageCompatibility,
    string CategoryCompatibility,
    string AdvertisingFormatCompatibility,
    string BrandSuitability,
    string InventoryAvailability,
    string SourceVerification,
    string Delivery,
    bool IsCommercialMatch,
    bool IsOutreachAuthorized,
    string[] EvidenceIds,
    string[] SourceUrls);

/// <summary>
/// Pure deterministic compatibility preflight. No external requests, persistence,
/// artificial pricing, inventory creation, outreach, or commercial commitments.
/// </summary>
public static class ScoutCompatibilityReview
{
    public static ScoutCompatibilityFinding Evaluate(ScoutReviewPair request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var creator = request.Creator ?? throw new InvalidOperationException("Creator is required.");
        var advertiser = request.Advertiser ?? throw new InvalidOperationException("Advertiser is required.");
        Validate(creator);
        Validate(advertiser);
        if (string.Equals(creator.RecordId, advertiser.RecordId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Creator and advertiser identities must differ.");

        var sameCountry = string.Equals(creator.CountryCode.Trim(), advertiser.CountryCode.Trim(), StringComparison.OrdinalIgnoreCase);
        var sameCity = string.Equals(creator.City.Trim(), advertiser.City.Trim(), StringComparison.OrdinalIgnoreCase);
        var hasSupportedLocation =
            creator.GeographicVerification == "PUBLIC_SOURCE_SUPPORTED"
            && advertiser.GeographicVerification == "PUBLIC_SOURCE_SUPPORTED";
        var hasSubmittedSourceEvidence =
            IsVerifiedClaim(creator) && IsVerifiedClaim(advertiser);
        var geographicCandidate = sameCountry && sameCity && hasSupportedLocation && hasSubmittedSourceEvidence;

        var language = !string.IsNullOrWhiteSpace(creator.Language)
            && !string.IsNullOrWhiteSpace(advertiser.Language)
            && string.Equals(creator.Language.Trim(), advertiser.Language.Trim(), StringComparison.OrdinalIgnoreCase)
                ? "CLAIMED_OVERLAP_REQUIRES_REVIEW" : "UNKNOWN_REQUIRES_REVIEW";

        return new ScoutCompatibilityFinding(
            creator.RecordId.Trim(),
            advertiser.RecordId.Trim(),
            geographicCandidate ? "GEOGRAPHIC_CANDIDATE_REVIEW_ONLY" : "INSUFFICIENT_EVIDENCE_REVIEW_ONLY",
            geographicCandidate ? "CLAIMED_SAME_CITY_REQUIRES_INDEPENDENT_CHECK" :
                !sameCountry ? "COUNTRY_MISMATCH" :
                !sameCity ? "CITY_MISMATCH" : "LOCATION_OR_SOURCE_EVIDENCE_MISSING",
            language,
            "POTENTIAL_CATEGORY_FIT_REQUIRES_HUMAN_REVIEW",
            "UNKNOWN_ADVERTISER_FORMAT_REQUIREMENTS",
            "NOT_ASSESSED",
            "NOT_VERIFIED",
            "CALLER_SUBMITTED_SOURCE_CLAIMS_NOT_ATTESTED",
            "NOT_SENT",
            false,
            false,
            [creator.EvidenceId.Trim(), advertiser.EvidenceId.Trim()],
            [creator.PublicSourceUrl.Trim(), advertiser.PublicSourceUrl.Trim()]);
    }

    private static bool IsVerifiedClaim(ScoutReviewProspect p) =>
        p.VerificationStatus is "SOURCE_VERIFIED" or "VERIFIED"
        && p.VerificationMethod is "INDEPENDENT_PUBLIC_WEB_REVIEW" or "DETERMINISTIC_WORKER";

    private static void Validate(ScoutReviewProspect p)
    {
        if (string.IsNullOrWhiteSpace(p.RecordId) || p.RecordId.Length > 128 ||
            string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 256 ||
            string.IsNullOrWhiteSpace(p.Category) || p.Category.Length > 128 ||
            string.IsNullOrWhiteSpace(p.City) || p.City.Length > 128 ||
            string.IsNullOrWhiteSpace(p.EvidenceId) || p.EvidenceId.Length > 128 ||
            string.IsNullOrWhiteSpace(p.CountryCode) || p.CountryCode.Length != 2)
            throw new InvalidOperationException("Required prospect identity, market, category or evidence is missing.");

        if (!Uri.TryCreate(p.PublicSourceUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.Query) && uri.Query.Length > 512)
            throw new InvalidOperationException("A valid public HTTPS source URL without credentials or fragments is required.");
    }
}
