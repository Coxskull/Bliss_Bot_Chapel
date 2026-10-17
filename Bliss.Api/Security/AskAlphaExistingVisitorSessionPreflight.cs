using System.Security.Claims;

namespace Bliss.Api.Security;

// This is a read-only claim-shape prerequisite for a *returning* fictional
// visitor. It NEVER proves that the IDP signed this principal or that an
// application-generated session belongs to the user. No OIDC configuration,
// cookies, consent, ownership, storage, or commercial permissions are minted.
public static class AskAlphaExistingVisitorSessionPreflight
{
    public static bool MatchesUnverifiedClaimShape(
        ClaimsPrincipal? principal, BlissAuthenticationOptions options)
    {
        // The new-enrollment declaration validates HTTPS Authority/Issuer,
        // feature opt-in and dedicated visitor role configuration. Its
        // "pending" state is not sufficient for an already-issued session,
        // so intentionally inspect only its IDP declaration boolean here.
        if (!AskAlphaVisitorEnrollmentGate.Inspect(principal, options)
                .IdpConfigurationDeclared ||
            principal is null ||
            principal.Identities.Count() != 1 ||
            principal.Identity?.IsAuthenticated != true)
            return false;

        // Fail closed on casing variants or two differently-cased session
        // claims: the downstream claim gate reads an exact claim type.
        var sessions = principal.Claims.Where(c => string.Equals(
            c.Type, options.AskAlphaVisitorSessionClaimType,
            StringComparison.OrdinalIgnoreCase)).ToArray();
        if (sessions.Length != 1 ||
            sessions[0].Type != options.AskAlphaVisitorSessionClaimType ||
            !Guid.TryParseExact(sessions[0].Value, "D", out var sessionId) ||
            sessionId == Guid.Empty ||
            // A visitor cannot carry staff/advertiser workspace scope while
            // presenting a visitor session, even if type casing is altered.
            principal.Claims.Any(c => string.Equals(
                c.Type, options.AdvertiserIdClaimType,
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, options.ReviewerWorkspaceClaimType,
                StringComparison.OrdinalIgnoreCase)))
            return false;

        // Reuse the exclusive visitor role, subject and session checks:
        // Issuer AND OriginalIssuer, one identity, single role/sub/session.
        // A positive result is only *claim shape*, never ownership evidence.
        return AskAlphaVisitorClaimGate.Inspect(principal, options, sessionId)
            .ClaimsMatchIssuerAndSession;
    }
}
