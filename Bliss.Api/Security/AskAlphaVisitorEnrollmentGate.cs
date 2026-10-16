using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Bliss.Api.Security;

// Stage D - preregistration security review only.
// No route, session issuance, consent grant, or provider setup is triggered.
// ClaimsPrincipal here is trusted ONLY after the ASP.NET OIDC and cookie
// middleware independently authenticate it: this class does not validate JWTs.
public sealed record AskAlphaEnrollmentReadiness(
    string Status,
    bool IdpConfigurationDeclared = false,
    bool IssuerSubjectClaimsConsistent = false,
    bool NewVisitorSessionCreated = false,
    bool HistoricalSessionOwnershipVerified = false,
    bool RealVisitorIdentityAccepted = false,
    bool VisitorConsentRecorded = false,
    bool TranscriptLinked = false,
    bool TranscriptExportAuthorized = false,
    bool PaymentAuthorized = false,
    bool CreatorPayoutAuthorized = false,
    bool CampaignDeliveryAuthorized = false,
    string Delivery = "NOT_SENT");

public static class AskAlphaVisitorEnrollmentGate
{
    public static AskAlphaEnrollmentReadiness Inspect(
        ClaimsPrincipal? principal, BlissAuthenticationOptions config)
    {
        var blocked = new AskAlphaEnrollmentReadiness(
            "NEW_VISITOR_ENROLLMENT_NOT_AUTHORIZED");
        // Explicit opt-in to inspect prerequisites is NOT an authorization
        // to expose an endpoint or mint a visitor-owned database session.
        if (!config.AskAlphaVisitorEnrollmentRequested ||
            !config.Enabled ||
            string.IsNullOrWhiteSpace(config.ClientId) ||
            string.IsNullOrWhiteSpace(config.Authority) ||
            string.IsNullOrWhiteSpace(config.AskAlphaVisitorIssuer) ||
            string.IsNullOrWhiteSpace(config.AskAlphaVisitorRole) ||
            string.IsNullOrWhiteSpace(config.AskAlphaVisitorSessionClaimType) ||
            config.AskAlphaVisitorRole == config.AdvertiserRole ||
            config.AskAlphaVisitorRole == config.AdminRole ||
            config.AskAlphaVisitorRole == config.OperatorRole ||
            config.AskAlphaVisitorRole == config.ReviewerRole ||
            config.AskAlphaVisitorRole == config.ViewerRole)
            return blocked;

        if (!Uri.TryCreate(config.Authority, UriKind.Absolute, out var authority) ||
            !Uri.TryCreate(config.AskAlphaVisitorIssuer, UriKind.Absolute, out var visitorIssuer) ||
            !IsAllowedIssuer(authority) || !IsAllowedIssuer(visitorIssuer) ||
            !Uri.Compare(authority,visitorIssuer,
                UriComponents.SchemeAndServer | UriComponents.Path,
                UriFormat.Unescaped,StringComparison.Ordinal).Equals(0))
            return blocked;

        var declared = blocked with
        {
            Status = "PROVIDER_DECLARED_NEW_VISITOR_ENROLLMENT_NOT_ACTIVE",
            IdpConfigurationDeclared = true
        };
        if (principal?.Identity?.IsAuthenticated != true ||
            // A visitor is a dedicated identity: no extra or foreign-issuer roles.
            // Never accept a visitor claim grafted onto a staff identity.
            principal.FindAll(ClaimTypes.Role).Count() != 1 ||
            principal.FindAll(ClaimTypes.Role).Any(role =>
                role.Value != config.AskAlphaVisitorRole ||
                role.Issuer != config.AskAlphaVisitorIssuer) ||
            !principal.IsInRole(config.AskAlphaVisitorRole) ||
            principal.IsInRole(config.AdvertiserRole) ||
            principal.IsInRole(config.OperatorRole) ||
            principal.IsInRole(config.AdminRole) ||
            principal.IsInRole(config.ReviewerRole) ||
            principal.IsInRole(config.ViewerRole))
            return declared;

        var subjects = principal.FindAll("sub").ToArray();
        if (subjects.Length != 1 ||
            subjects[0].Issuer != config.AskAlphaVisitorIssuer ||
            !Regex.IsMatch(subjects[0].Value, @"\A[A-Za-z0-9._:@-]{8,160}\z",
                RegexOptions.CultureInvariant) ||
            // A previously assigned historic session claim cannot be used to
            // bootstrap a new session, even if its UUID is syntactically valid.
            principal.HasClaim(x => x.Type == config.AskAlphaVisitorSessionClaimType) ||
            principal.HasClaim(x => x.Type == config.AdvertiserIdClaimType))
            return declared;

        return declared with
        {
            Status = "NEW_VISITOR_OIDC_CLAIMS_DECLARED_SERVER_CREATION_PENDING",
            IssuerSubjectClaimsConsistent = true
        };
    }

    private static bool IsAllowedIssuer(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(uri.Host) &&
        uri.UserInfo.Length == 0 && uri.Query.Length == 0 &&
        uri.Fragment.Length == 0;
}
