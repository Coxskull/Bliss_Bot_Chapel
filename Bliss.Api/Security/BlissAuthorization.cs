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
