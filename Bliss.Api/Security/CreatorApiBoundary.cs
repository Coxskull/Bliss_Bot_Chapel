using System.Security.Claims;

namespace Bliss.Api.Security;

// Task #4: an authenticated creator session is NOT a general authenticated
// marketplace session. DefaultPolicy admits authenticated principals to many
// legacy read APIs, so this independent fail-closed boundary applies before
// endpoint authorization. It never provisions accounts, changes roles or
// bypasses RestrictedPreviewGate.
public static class CreatorApiBoundary
{
    public static bool ShouldDeny(HttpContext context, BlissAuthenticationOptions auth)
    {
        // Keep this restriction active even AFTER enrollment is switched
        // off: an already-issued cookie may still contain a creator role.
        // Disabling enrollment must never widen that stale cookie's API scope.
        if (!auth.Enabled
            || context.User.Identity?.IsAuthenticated != true
            || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var creatorRole = auth.CreatorRole;
        if (string.IsNullOrWhiteSpace(creatorRole))
            return false; // malformed config cannot mint a creator role

        // One creator label in a mixed or case-aliased set still restricts
        // the session. Exact proof of creator ownership remains the separate
        // issuer-bound GetMyVerifiedCreator authorization check.
        var creatorLabeled = context.User.Claims.Any(claim =>
            (string.Equals(claim.Type, auth.RoleClaimType, StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase))
            && string.Equals(claim.Value, creatorRole, StringComparison.Ordinal));
        if (!creatorLabeled)
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method)
            && (path.Equals("/api/auth/session", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/supabase-config", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/creators/me", StringComparison.OrdinalIgnoreCase)))
            return false;

        // Logout is the only mutating endpoint permitted for a creator; the
        // existing global antiforgery check remains mandatory for POST.
        if (HttpMethods.IsPost(method)
            && path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
