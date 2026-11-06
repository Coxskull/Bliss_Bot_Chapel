using System.Security.Claims;

namespace Bliss.Api.Security;

// An advertiser login must never inherit the legacy catch-all authenticated
// marketplace APIs. Applies to issued cookies even when enrollment is OFF.
public static class AdvertiserApiBoundary
{
    public static bool ShouldDeny(HttpContext context, BlissAuthenticationOptions auth)
    {
        if (!auth.Enabled || context.User.Identity?.IsAuthenticated != true
            || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(auth.AdvertiserRole))
            return false;

        var labeled = context.User.Claims.Any(claim =>
            (string.Equals(claim.Type, auth.RoleClaimType, StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase)
             || string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase))
            && string.Equals(claim.Value, auth.AdvertiserRole, StringComparison.Ordinal));
        if (!labeled) return false;

        var path = context.Request.Path.Value ?? "";
        if (HttpMethods.IsGet(context.Request.Method)
            && (path.Equals("/api/auth/session", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/supabase-config", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/advertisers/me", StringComparison.OrdinalIgnoreCase)))
            return false;
        if (HttpMethods.IsPost(context.Request.Method)
            && path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }
}
