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
        // The whole ClaimsPrincipal may aggregate several identities. Never
        // combine one identity's authentication flag with another's issuer-
        // controlled role, subject or session claim; reject foreign roles.
        // Still only a claim-shape check, NOT actual historic ownership proof.
        if (expectedSessionId == Guid.Empty || !options.Enabled ||
            user is null || user.Identities.Count() != 1 ||
            user.Identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(options.AskAlphaVisitorIssuer) ||
            string.IsNullOrWhiteSpace(options.AskAlphaVisitorSessionClaimType) ||
            string.IsNullOrWhiteSpace(options.AskAlphaVisitorRole) ||
            options.AskAlphaVisitorRole == options.AdvertiserRole ||
            options.AskAlphaVisitorRole == options.OperatorRole ||
            options.AskAlphaVisitorRole == options.AdminRole ||
            options.AskAlphaVisitorRole == options.ReviewerRole ||
            options.AskAlphaVisitorRole == options.ViewerRole ||
            // One exclusive, correctly issued visitor role. No extra role,
            // including one that is unknown to today's staff role registry.
            user.FindAll(ClaimTypes.Role).Count() != 1 ||
            user.FindAll(ClaimTypes.Role).Any(role =>
                role.Value != options.AskAlphaVisitorRole ||
                role.Issuer != options.AskAlphaVisitorIssuer ||
                role.OriginalIssuer != options.AskAlphaVisitorIssuer) ||
            !user.IsInRole(options.AskAlphaVisitorRole))
            return blocked;

        // Require one issuer-controlled subject AND one issuer-controlled
        // session binding, not arbitrary request/form/header values.
        var subjects = user.FindAll("sub").ToArray();
        var bindings = user.FindAll(options.AskAlphaVisitorSessionClaimType).ToArray();
        if (subjects.Length != 1 || bindings.Length != 1 ||
            subjects[0].Issuer != options.AskAlphaVisitorIssuer ||
            subjects[0].OriginalIssuer != options.AskAlphaVisitorIssuer ||
            bindings[0].Issuer != options.AskAlphaVisitorIssuer ||
            bindings[0].OriginalIssuer != options.AskAlphaVisitorIssuer ||
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
