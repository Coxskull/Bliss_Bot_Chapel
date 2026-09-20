namespace Bliss.Domain.Common;

/// <summary>
/// String status values used by Phase 1 persistence. These are not an intelligence engine.
/// </summary>
public static class EntityStatuses
{
    public const string Active = "ACTIVE";
    public const string Created = "CREATED";
    public const string Unknown = "UNKNOWN";
    public const string Approved = "APPROVED";
    public const string Draft = "DRAFT";
    public const string Completed = "COMPLETED";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public const string Ineligible = "INELIGIBLE";
    public const string Evaluated = "EVALUATED";
    public const string Planned = "PLANNED";
}

public static class EligibilityReasonCodes
{
    public const string GeoNotEligible = "GEO_NOT_ELIGIBLE";
    public const string GeoUnknown = "GEO_UNKNOWN";
    public const string MinimumAudienceNotMet = "MINIMUM_AUDIENCE_NOT_MET";
    public const string MinimumAudienceUnknown = "MINIMUM_AUDIENCE_UNKNOWN";
    public const string LanguageNotEligible = "LANGUAGE_NOT_ELIGIBLE";
    public const string LanguageUnknown = "LANGUAGE_UNKNOWN";
    public const string OpportunityInactive = "OPPORTUNITY_INACTIVE";
    public const string CategoryProhibited = "CATEGORY_PROHIBITED";
}

public static class ConfidenceLevels
{
    public const string Unknown = "UNKNOWN";
}

public static class InventorySlotTypes
{
    public const string PreRoll = "PRE_ROLL";
    public const string MidRoll = "MID_ROLL";
    public const string LowerThird = "LOWER_THIRD";
    public const string PostRoll = "POST_ROLL";
    public const string PerimeterOverlay = "PERIMETER_OVERLAY";
    public const string CornerOverlay = "CORNER_OVERLAY";
    public const string RotatingOverlay = "ROTATING_OVERLAY";
    public const string SponsoredSegment = "SPONSORED_SEGMENT";
}
