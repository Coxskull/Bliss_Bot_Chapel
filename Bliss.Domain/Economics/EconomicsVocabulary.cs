namespace Bliss.Domain.Economics;

/// <summary>
/// Vocabulary for the future Economics & Rate Intelligence bounded context.
/// These codes are not rates, not a calculator, and not compensation defaults.
/// </summary>
public static class PricingModelCodes
{
    public const string Cpm = "CPM";
    public const string Cpv = "CPV";
    public const string FlatPlacement = "FLAT_PLACEMENT";
    public const string FixedCampaign = "FIXED_CAMPAIGN";
    public const string Sponsorship = "SPONSORSHIP";
    public const string HostRead = "HOST_READ";
    public const string Cpa = "CPA";
    public const string Cpl = "CPL";
    public const string Cps = "CPS";
    public const string Hybrid = "HYBRID";
}

public static class ObservationVerificationStatuses
{
    public const string Verified = "VERIFIED";
    public const string Estimated = "ESTIMATED";
    public const string Inferred = "INFERRED";
    public const string Unknown = "UNKNOWN";
}

public static class EconomicsConfidenceLevels
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
    public const string Unknown = "UNKNOWN";
}

public static class QuoteOutcomeResponses
{
    public const string Accepted = "ACCEPTED";
    public const string Declined = "DECLINED";
    public const string Negotiated = "NEGOTIATED";
}

public static class CompensationParticipantRoles
{
    public const string Alpha = "ALPHA";
    public const string Creator = "CREATOR";
    public const string OtherAuthorized = "OTHER_AUTHORIZED";
}
