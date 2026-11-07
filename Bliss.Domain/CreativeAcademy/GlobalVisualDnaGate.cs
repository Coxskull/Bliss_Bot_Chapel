namespace Bliss.Domain.CreativeAcademy;

public sealed record GlobalVisualDnaDecision(
    string Status,
    int ReportedScore,
    IReadOnlyList<string> BlockingDefects,
    bool VisualEvidenceRecorded,
    bool BrandDnaCompliant,
    bool OriginalityConfirmed,
    bool InventoryGeometryVerified,
    bool QrVerified,
    bool CampaignReady,
    string Delivery,
    string Notice);

/// <summary>
/// Deterministic release gate for visual QA evidence. A numeric score is
/// informational only; it cannot override a hard failure or missing evidence.
/// </summary>
public static class GlobalVisualDnaGate
{
    public const string Pass = "PASS";
    public const string Revise = "REVISE";
    public const string Fail = "FAIL";
    public const string Withheld = "WITHHELD";

    private static readonly HashSet<string> HardFailures = new(StringComparer.Ordinal)
    {
        "ANATOMY_FAILURE",
        "PRODUCT_REALISM_FAILURE",
        "MATERIAL_REALISM_FAILURE",
        "UNREADABLE_TYPOGRAPHY",
        "BRAND_DNA_VIOLATION",
        "REFERENCE_TOO_SIMILAR",
        "INVENTORY_GEOMETRY_FAILURE",
        "QR_FAILURE"
    };

    public static GlobalVisualDnaDecision Evaluate(
        int reportedScore,
        IEnumerable<string>? defectCodes,
        bool visualEvidenceRecorded,
        bool brandDnaCompliant,
        bool originalityConfirmed,
        bool inventoryGeometryVerified,
        bool qrVerified)
    {
        if (reportedScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(reportedScore), "Reported score must be between 0 and 100.");
        }

        var defects = (defectCodes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var unknown = defects.Where(code => !ReferenceLibrary.DoNotProduce.Contains(code, StringComparer.Ordinal)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                "Unknown visual defect code(s): " + string.Join(", ", unknown) + ". None were invented.");
        }

        var blocking = defects;

        var status = !visualEvidenceRecorded
            ? Withheld
            : !brandDnaCompliant || !originalityConfirmed || !inventoryGeometryVerified || !qrVerified
                || defects.Any(HardFailures.Contains)
                ? Fail
                : defects.Length > 0
                    ? Revise
                    : Pass;

        var notice = status switch
        {
            Pass => "All required evidence and deterministic checks passed. The score is informational and does not authorize delivery.",
            Revise => "Visual defects remain. Repair and reassess before approval. The score cannot waive defects.",
            Fail => "A hard release condition failed. Automatic approval is refused regardless of score.",
            _ => "Visual evidence is missing. Approval is withheld regardless of score."
        };

        return new GlobalVisualDnaDecision(
            status,
            reportedScore,
            blocking,
            visualEvidenceRecorded,
            brandDnaCompliant,
            originalityConfirmed,
            inventoryGeometryVerified,
            qrVerified,
            false,
            "NOT_SENT",
            notice);
    }
    /// <summary>
    /// Two independent, human-evidenced axes. A reference image cannot certify
    /// either actual customer brand rights or subjective visual craftsmanship.
    /// No generated artwork is accepted merely by sharing a template or palette.
    /// </summary>
    public static IndependentCreativeAssessment EvaluateIndependent(
        BrandComplianceEvidence brand,
        ReferenceQualityEvidence quality,
        LocalizationEvidence localization)
    {
        ArgumentNullException.ThrowIfNull(brand);
        ArgumentNullException.ThrowIfNull(quality);
        ArgumentNullException.ThrowIfNull(localization);

        var brandStatus = !brand.ApprovedBrandSnapshot
            ? Withheld
            : brand.ApprovedLogoRights && brand.ColorsMatch && brand.CopyMatches
              && brand.TypographyMatches && brand.ProductsMatch && brand.LanguageMatches
              && brand.ClaimsSubstantiated && brand.MediaRightsConfirmed
                ? Pass : Fail;

        var qualityStatus = !quality.HumanVisualReviewRecorded || !quality.SourceArtworkPresent
            ? Withheld
            : quality.OriginalComposition && quality.CommercialPhotography
              && quality.Lighting && quality.RichColorContrast
              && quality.HighlightShadowControl && quality.DimensionalDepth
              && quality.MaterialRealism && quality.TextureFidelity
              && quality.HeroProductDominance && quality.VisualHierarchy
              && quality.LegibleTypography && quality.PremiumPolish
                ? Pass : Fail;

        var approvedMarket = new[] { "DO", "PA", "CO", "PH", "MY", "ID" }
            .Contains((localization.CountryCode ?? "").Trim().ToUpperInvariant(), StringComparer.Ordinal);
        var localizationStatus = !approvedMarket
            ? "LOCALIZATION_REVIEW_REQUIRED"
            : localization.MarketResearchReviewed && localization.AdvertiserApprovedLanguage
              && localization.AdvertiserApprovedSetting && localization.CastingRightsReviewed
                ? Pass : "LOCALIZATION_REVIEW_REQUIRED";

        return new IndependentCreativeAssessment(
            brandStatus, qualityStatus, localizationStatus,
            brandStatus == Pass && qualityStatus == Pass && localizationStatus == Pass,
            false, "NOT_SENT", 0,
            "Human evidence is required for each axis. Quality references are not reusable customer templates.");
    }

}

public sealed record BrandComplianceEvidence(
    bool ApprovedBrandSnapshot,
    bool ApprovedLogoRights,
    bool ColorsMatch,
    bool CopyMatches,
    bool TypographyMatches,
    bool ProductsMatch,
    bool LanguageMatches,
    bool ClaimsSubstantiated,
    bool MediaRightsConfirmed);

public sealed record ReferenceQualityEvidence(
    bool HumanVisualReviewRecorded,
    bool SourceArtworkPresent,
    bool OriginalComposition,
    bool CommercialPhotography,
    bool Lighting,
    bool RichColorContrast,
    bool HighlightShadowControl,
    bool DimensionalDepth,
    bool MaterialRealism,
    bool TextureFidelity,
    bool HeroProductDominance,
    bool VisualHierarchy,
    bool LegibleTypography,
    bool PremiumPolish);

public sealed record LocalizationEvidence(
    string? CountryCode,
    bool MarketResearchReviewed,
    bool AdvertiserApprovedLanguage,
    bool AdvertiserApprovedSetting,
    bool CastingRightsReviewed);

public sealed record IndependentCreativeAssessment(
    string BrandComplianceStatus,
    string ReferenceQualityStatus,
    string LocalizationStatus,
    bool EligibleForAdvertiserReview,
    bool CampaignReady,
    string Delivery,
    int ModelCalls,
    string Notice);
