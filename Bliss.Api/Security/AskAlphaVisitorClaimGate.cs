using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Bliss.Api.Security;

// AGM-LAUNCH-015 Stage A: deny-by-default prerequisite only.
// Claim checks alone CANNOT prove ownership of historical Ask Alpha conversations.
// Do not use this result as consent, identity proof for payment, or delivery authority.
public sealed record AskAlphaVisitorIdentityReadiness(
    string Status,
    Guid RequestedSessionId,
    bool ClaimsMatchIssuerAndSession = false,
    bool HistoricalSessionOwnershipVerified = false,
    bool VisitorConsentRecorded = false,
    bool TranscriptLinked = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public sealed class AskAlphaVisitorClaimGate
{
    public static AskAlphaVisitorIdentityReadiness Inspect(
        ClaimsPrincipal? user,
        BlissAuthenticationOptions options,
        Guid expectedSessionId)
    {
        var blocked = new AskAlphaVisitorIdentityReadiness("VISITOR_IDENTITY_PREREQUISITES_MISSING", expectedSessionId);
        if (expectedSessionId == Guid.Empty || !options.Enabled ||
            user?.Identity?.IsAuthenticated != true ||
            !user.IsInRole(options.AskAlphaVisitorRole) ||
            user.IsInRole(options.AdvertiserRole) ||
            user.IsInRole(options.OperatorRole) ||
            user.IsInRole(options.AdminRole) ||
            user.IsInRole(options.ReviewerRole) ||
            string.IsNullOrWhiteSpace(options.AskAlphaVisitorIssuer) ||
            string.IsNullOrWhiteSpace(options.AskAlphaVisitorSessionClaimType) ||
            string.IsNullOrWhiteSpace(options.AskAlphaVisitorRole))
            return blocked;

        // Require one issuer-controlled subject AND one issuer-controlled
        // session binding, not arbitrary request/form/header values.
        var subjects = user.FindAll("sub").ToArray();
        var bindings = user.FindAll(options.AskAlphaVisitorSessionClaimType).ToArray();
        if (subjects.Length != 1 || bindings.Length != 1 ||
            subjects[0].Issuer != options.AskAlphaVisitorIssuer ||
            bindings[0].Issuer != options.AskAlphaVisitorIssuer ||
            !Regex.IsMatch(subjects[0].Value, @"\A[A-Za-z0-9._:@-]{8,160}\z",
                RegexOptions.CultureInvariant) ||
            !Guid.TryParseExact(bindings[0].Value, "D", out var boundSession) ||
            boundSession != expectedSessionId)
            return blocked;

        return blocked with
        {
            // This means only matching claims, NOT identity verified against a
            // stored conversation, consent or ownership of its author.
            Status = "OIDC_CLAIMS_PRESENT_HISTORY_UNVERIFIED",
            ClaimsMatchIssuerAndSession = true
        };
    }
}
