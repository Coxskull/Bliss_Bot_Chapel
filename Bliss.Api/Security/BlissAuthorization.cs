using System.Security.Claims;

namespace Bliss.Api.Security;

public static class BlissAuthorization
{
    public const string WritePolicy = "BlissWrite";
    public const string ReviewPolicy = "BlissReview";

    public static bool CanWrite(ClaimsPrincipal user, BlissAuthenticationOptions options) =>
        !options.Enabled
        || user.IsInRole(options.OperatorRole)
        || user.IsInRole(options.AdminRole);

    public static bool CanReview(ClaimsPrincipal user, BlissAuthenticationOptions options) =>
        !options.Enabled
        || user.IsInRole(options.ReviewerRole)
        || user.IsInRole(options.AdminRole);

    // Task #4 creator self-service is entirely disabled by default and
    // requires one issuer-bound, owner-approved, verified creator identity.
    // Never use an intake email, CRM contact ID, URL parameter or user_metadata
    // as the creator's tenant scope.
    public static bool TryResolveVerifiedCreatorId(
        ClaimsPrincipal user, BlissAuthenticationOptions options, out Guid creatorId)
    {
        creatorId = Guid.Empty;
        if (!options.Enabled || !options.SupabaseEnabled || !options.CreatorSelfServiceRequested
            || user.Identity?.IsAuthenticated != true
            || string.IsNullOrWhiteSpace(options.Authority)
            || string.IsNullOrWhiteSpace(options.CreatorRole)
            || string.IsNullOrWhiteSpace(options.CreatorIdClaimType)
            || string.IsNullOrWhiteSpace(options.RoleClaimType)
            || options.CreatorIdClaimType == options.RoleClaimType)
            return false;

        // The cookie must have exactly one approved subject and exactly one
        // issuer-bound creator role. Mixed/duplicate aliases are not harmless:
        // rejecting them avoids a second authority source being ignored by
        // case-sensitive claim resolution after cookie normalization.
        var subjectClaims = user.Claims.Where(claim =>
            string.Equals(claim.Type, "sub", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (subjectClaims.Length != 1 || subjectClaims[0].Type != "sub"
            || !options.ApprovedCreatorSubjects.Contains(subjectClaims[0].Value, StringComparer.Ordinal)
            || options.ApprovedTestSubjects.Contains(subjectClaims[0].Value, StringComparer.Ordinal))
            return false;

        var roles = user.Claims.Where(claim =>
            string.Equals(claim.Type, options.RoleClaimType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
            || string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase)
            || string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (roles.Length != 1
            || roles[0].Type != options.RoleClaimType
            || roles[0].Value != options.CreatorRole
            || roles[0].Issuer != options.Authority
            || roles[0].OriginalIssuer != options.Authority)
            return false;

        var creatorClaims = user.Claims.Where(claim =>
            string.Equals(claim.Type, options.CreatorIdClaimType, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (creatorClaims.Length != 1
            || creatorClaims[0].Type != options.CreatorIdClaimType
            || creatorClaims[0].Issuer != options.Authority
            || creatorClaims[0].OriginalIssuer != options.Authority
            || !Guid.TryParse(creatorClaims[0].Value, out creatorId)
            || creatorId == Guid.Empty
            || user.HasClaim(claim => claim.Type == options.AdvertiserIdClaimType
                || claim.Type == options.ReviewerWorkspaceClaimType))
        {
            creatorId = Guid.Empty;
            return false;
        }
        return true;
    }

    // An authenticated reviewer is never implicitly assigned to all tenants.
    // The workspace claim must originate in a verified OIDC principal; the
    // request path, advertiser_id claim, or any HTTP header is not an assignment.
    // Administrators retain their pre-existing explicitly privileged scope.
    public static bool CanReviewWorkspace(ClaimsPrincipal user, BlissAuthenticationOptions options, Guid workspaceId)
    {
        if (!options.Enabled) return true; // local Development fixture only
        if (workspaceId == Guid.Empty || user.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole(options.AdminRole)) return true;
        return user.IsInRole(options.ReviewerRole)
            && !string.IsNullOrWhiteSpace(options.ReviewerWorkspaceClaimType)
            && user.FindAll(options.ReviewerWorkspaceClaimType)
                .Any(claim => Guid.TryParse(claim.Value, out var parsed)
                    && parsed != Guid.Empty && parsed == workspaceId);
    }
}

public sealed class OperatorIdentity(BlissAuthenticationOptions options)
{
    public string ResolveLabel(ClaimsPrincipal user, string submittedLabel)
    {
        if (!options.Enabled)
        {
            return submittedLabel;
        }

        var label = user.FindFirst(options.NameClaimType)?.Value
            ?? user.FindFirst("preferred_username")?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException("The authenticated identity has no usable operator label.");
        }

        return label.Trim().Length <= 128
            ? label.Trim()
            : label.Trim()[..128];
    }
}
