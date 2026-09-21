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
