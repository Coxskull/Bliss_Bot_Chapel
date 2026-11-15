namespace Bliss.Api.Security;

/// <summary>
/// A newly authenticated advertiser applicant has no workspace role. Keep that
/// session inside the registration flow until a separate issuer-side review.
/// </summary>
public static class AdvertiserRegistrationBoundary
{
    public static bool ShouldDeny(HttpContext context, BlissAuthenticationOptions options)
    {
        if (!BlissAuthorization.IsPendingAdvertiserRegistration(context.User, options)
            || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        if (HttpMethods.IsGet(context.Request.Method)
            && (path.Equals("/api/auth/session", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/supabase-config", StringComparison.OrdinalIgnoreCase)))
            return false;
        // Applicant privacy request intake and own receipts are explicitly scoped.
        // This does not grant access to other advertiser APIs or any workspace.
        if (options.AdvertiserPrivacyRequestsRequested && HttpMethods.IsGet(context.Request.Method)
            && path.Equals("/api/advertisers/privacy-requests", StringComparison.OrdinalIgnoreCase))
            return false;
        if (HttpMethods.IsPost(context.Request.Method)
            && (path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/advertisers/register", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/advertisers/privacy-requests", StringComparison.OrdinalIgnoreCase)))
            return false;
        return true;
    }
}
