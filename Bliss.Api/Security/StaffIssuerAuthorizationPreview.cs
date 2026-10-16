using System.Security.Claims;

namespace Bliss.Api.Security;

/// <summary>
/// NON-ACTIVE reference gate for AGM-COMM-015 issuer-bound staff authorization.
/// This class is NOT wired to ASP.NET authorization policies or routes.
/// Approval of the real identity provider, staff role source and tenant claim
/// contract is required before any production integration.
///
/// Claims.Issuer is metadata, NOT proof that a real token signature was
/// verified. Actual authorization also requires trusted OIDC middleware,
/// session/cookie controls and provider-specific security approval.
/// </summary>
public static class StaffIssuerAuthorizationPreview
{
    public static bool CanWrite(
        ClaimsPrincipal user, BlissAuthenticationOptions options, string approvedIssuer) =>
        TrustedIdentity(user, options, approvedIssuer) is { } identity
        && (HasRole(identity, options.RoleClaimType, options.AdminRole, approvedIssuer)
            || HasRole(identity, options.RoleClaimType, options.OperatorRole, approvedIssuer));

    public static bool CanReview(
        ClaimsPrincipal user, BlissAuthenticationOptions options, string approvedIssuer) =>
        TrustedIdentity(user, options, approvedIssuer) is { } identity
        && (HasRole(identity, options.RoleClaimType, options.AdminRole, approvedIssuer)
            || HasRole(identity, options.RoleClaimType, options.ReviewerRole, approvedIssuer));

    public static bool CanReviewWorkspace(
        ClaimsPrincipal user, BlissAuthenticationOptions options,
        string approvedIssuer, Guid workspaceId)
    {
        if (workspaceId == Guid.Empty
            || TrustedIdentity(user, options, approvedIssuer) is not { } identity)
            return false;

        if (HasRole(identity, options.RoleClaimType, options.AdminRole, approvedIssuer))
            return true;

        return HasRole(identity, options.RoleClaimType, options.ReviewerRole, approvedIssuer)
            && !string.IsNullOrWhiteSpace(options.ReviewerWorkspaceClaimType)
            && identity.FindAll(options.ReviewerWorkspaceClaimType).Any(claim =>
                HasOrigin(claim, approvedIssuer)
                && Guid.TryParse(claim.Value, out var parsed)
                && parsed != Guid.Empty
                && parsed == workspaceId);
    }

    private static ClaimsIdentity? TrustedIdentity(
        ClaimsPrincipal? user, BlissAuthenticationOptions? options, string? approvedIssuer)
    {
        // There is intentionally NO Development bypass and NO policy fallback.
        // An unapproved provider is never silently treated as trusted.
        if (user is null || options is null || !options.Enabled
            || string.IsNullOrWhiteSpace(options.RoleClaimType)
            || string.IsNullOrWhiteSpace(options.ReviewerWorkspaceClaimType)
            || string.Equals(options.RoleClaimType, options.ReviewerWorkspaceClaimType, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(options.ClientId)
            || !DistinctConfiguredRoles(options)
            || string.IsNullOrWhiteSpace(options.Authority)
            || string.IsNullOrWhiteSpace(approvedIssuer)
            || !string.Equals(options.Authority, approvedIssuer, StringComparison.Ordinal)
            || !Uri.TryCreate(approvedIssuer, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0
            || uri.Fragment.Length != 0)
            return null;

        // Deny claim stitching across authenticated/unauthenticated identities.
        var identities = user.Identities.ToArray();
        return identities.Length == 1 && identities[0].IsAuthenticated
            ? identities[0] : null;
    }

    // A preview of staff grants cannot assume startup posture checks were
    // executed. A missing or overlapping role label could make a visitor,
    // viewer, or reviewer accidentally qualify as a privileged staff role.
    // This is an in-memory preflight, NOT IdP client or signature validation.
    private static bool DistinctConfiguredRoles(BlissAuthenticationOptions options)
    {
        var roles = new[]
        {
            options.ViewerRole, options.OperatorRole, options.ReviewerRole,
            options.AdminRole, options.AdvertiserRole, options.AskAlphaVisitorRole
        };
        return roles.All(role => !string.IsNullOrWhiteSpace(role))
            && roles.Distinct(StringComparer.Ordinal).Count() == roles.Length;
    }

    private static bool HasRole(
        ClaimsIdentity identity, string roleClaimType, string? role, string approvedIssuer) =>
        !string.IsNullOrWhiteSpace(role)
        && identity.FindAll(roleClaimType).Any(claim =>
            claim.Value == role && HasOrigin(claim, approvedIssuer));

    private static bool HasOrigin(Claim claim, string approvedIssuer) =>
        string.Equals(claim.Issuer, approvedIssuer, StringComparison.Ordinal)
        && string.Equals(claim.OriginalIssuer, approvedIssuer, StringComparison.Ordinal);
}
