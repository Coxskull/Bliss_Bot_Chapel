using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Bliss.Api.Security;

// Stage I: server-side command shape and fail-closed consent gate.
// A matching ClaimsPrincipal is NOT proof of cryptographic OIDC validation.
// No authoritative ownership/consent ledger exists in the application DB.
// All paths DENY consent writes, transcript export, payouts and ad delivery.
public sealed record AskAlphaVisitorConsentCommand(
    string? Purpose, string? ConsentVersion,
    bool ExplicitlySelected, string? RequestKey);

public sealed record AskAlphaVisitorConsentDecision(
    string Status,
    string VisitorConsentStatus = "NOT_RECORDED",
    bool AuthoritativeOwnershipVerified = false,
    bool AuthenticatedVisitorConsentRecorded = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public static class AskAlphaVisitorConsentCommandGate
{
    public const string Purpose = "CREATIVE_PREVIEW_ONLY";
    public const string PreviewVersion = "AGM-CONSENT-PREVIEW-V1";

    public static AskAlphaVisitorConsentDecision Inspect(
        ClaimsPrincipal? principal,
        BlissAuthenticationOptions authentication,
        AskAlphaVisitorConsentCommand? request,
        bool withdrawal)
    {
        var denied = new AskAlphaVisitorConsentDecision(
            "REAL_VISITOR_CONSENT_BACKEND_NOT_CONFIGURED");
        // This only examines prerequisites. Authoritative new-session ownership
        // and personally submitted consent must still be independently proven.
        // A returning visitor legitimately has a session binding, which the
        // *new* enrollment gate refuses. Accept either unverified claim shape
        // only for command validation; neither can issue real consent.
        // The existing-session path MUST NOT be mistaken for DB ownership
        // verification, signed identity proof, or activation authority.
        if (AskAlphaVisitorEnrollmentGate.Inspect(principal, authentication)
                .Status != "NEW_VISITOR_OIDC_CLAIMS_DECLARED_SERVER_CREATION_PENDING" &&
            !AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
                principal, authentication))
            return denied with { Status = "VISITOR_IDENTITY_PREREQUISITES_MISSING" };

        if (request is null ||
            request.Purpose != Purpose ||
            request.ConsentVersion != PreviewVersion ||
            string.IsNullOrEmpty(request.RequestKey) ||
            request.RequestKey.Length is < 8 or > 100 ||
            !Regex.IsMatch(request.RequestKey, @"\A[A-Za-z0-9_-]+\z",
                RegexOptions.CultureInvariant) ||
            (!withdrawal && !request.ExplicitlySelected) ||
            (withdrawal && request.ExplicitlySelected))
            return denied with { Status = "CONSENT_COMMAND_INPUT_INVALID" };

        // CRITICAL: Neither a valid principal shape nor an unchecked client
        // assertion of consent/version is an actual signed consent receipt.
        // This always refuses the write until app DB + identity + privacy
        // infrastructure is independently approved and implemented.
        return denied with {
            Status = withdrawal
                ? "AUTHENTICATED_WITHDRAWAL_STORAGE_UNAVAILABLE"
                : "AUTHENTICATED_OPT_IN_STORAGE_UNAVAILABLE"
        };
    }
}
