namespace Bliss.Api.Security;

/// <summary>
/// A newly authenticated advertiser applicant has no workspace role. Keep that
/// session inside the registration flow until a separate issuer-side review.
/// </summary>
public static class AdvertiserRegistrationBoundary
{
    public static bool ShouldDeny(HttpContext context, BlissAuthenticationOptions options)
    {
        if (!BlissAuthorization.IsPendingAdvertiserIdentity(context.User, options)
            || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        if (HttpMethods.IsGet(context.Request.Method)
            && (path.Equals("/api/auth/session", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/supabase-config", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/advertisers/registration-status", StringComparison.OrdinalIgnoreCase)))
            return false;
        // The profile controller still requires a live ACTIVE membership and
        // exact original receipt on every request. No other API is opened.
        if (HttpMethods.IsGet(context.Request.Method)
            && path.Equals("/api/advertisers/me", StringComparison.OrdinalIgnoreCase)
            && BlissAuthorization.CanResolveAdvertiserMembershipProfile(context.User, options))
            return false;
        if (options.AdvertiserCampaignDraftsRequested
            && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsPost(context.Request.Method))
            && path.Equals("/api/advertisers/my-campaign-drafts", StringComparison.OrdinalIgnoreCase)
            && BlissAuthorization.CanResolveAdvertiserMembershipProfile(context.User, options))
            return false;
        if (options.AdvertiserCampaignDraftsRequested && options.AdvertiserCreativeHandoffPreviewRequested
            && HttpMethods.IsPost(context.Request.Method)
            && path.Equals("/api/advertisers/my-campaign-drafts/creative-handoff-preview", StringComparison.OrdinalIgnoreCase)
            && BlissAuthorization.CanResolveAdvertiserMembershipProfile(context.User, options))
            return false;
        // Applicant privacy request intake and own receipts are explicitly scoped.
        // This does not grant access to other advertiser APIs or any workspace.
        if (options.AdvertiserPrivacyRequestsRequested && HttpMethods.IsGet(context.Request.Method)
            && path.Equals("/api/advertisers/privacy-requests", StringComparison.OrdinalIgnoreCase))
            return false;
        if (HttpMethods.IsPost(context.Request.Method)
            && (path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
                || (options.AdvertiserRegistrationRequested && path.Equals("/api/advertisers/register", StringComparison.OrdinalIgnoreCase))
                || (options.AdvertiserPrivacyRequestsRequested
                    && path.Equals("/api/advertisers/privacy-requests", StringComparison.OrdinalIgnoreCase))))
            return false;
        return true;
    }
}
