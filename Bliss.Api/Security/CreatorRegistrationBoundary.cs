namespace Bliss.Api.Security;

/// <summary>Limits a newly verified creator identity to its own registration lane.</summary>
public static class CreatorRegistrationBoundary
{
    public static bool ShouldDeny(HttpContext context, BlissAuthenticationOptions options)
    {
        if (!BlissAuthorization.IsPendingCreatorIdentity(context.User, options)
            || !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = context.Request.Path.Value ?? string.Empty;
        if (HttpMethods.IsGet(context.Request.Method)
            && (path.Equals("/api/auth/session", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/auth/supabase-config", StringComparison.OrdinalIgnoreCase)
                || (options.CreatorRegistrationRequested
                    && (path.Equals("/api/creators/registration", StringComparison.OrdinalIgnoreCase)
                        || (options.CreatorCampaignInvitationRequested && path.Equals("/api/creators/campaign-invitations", StringComparison.OrdinalIgnoreCase))))))
            return false;
        if (HttpMethods.IsPost(context.Request.Method)
            && (path.Equals("/api/auth/logout", StringComparison.OrdinalIgnoreCase)
                || (options.CreatorRegistrationRequested
                    && (path.Equals("/api/creators/register", StringComparison.OrdinalIgnoreCase)
                        || (options.CreatorInventoryReadbackRequested && options.CreatorInventoryPauseRequested
                            && path.Equals("/api/creators/inventory/pause", StringComparison.OrdinalIgnoreCase))
                        || (options.CreatorInventoryReadbackRequested && options.CreatorInventoryDraftRequested
                            && path.Equals("/api/creators/inventory/draft", StringComparison.OrdinalIgnoreCase))
                        || (options.CreatorCampaignInvitationRequested && path.Equals("/api/creators/campaign-invitations", StringComparison.OrdinalIgnoreCase))))))
            return false;
        return true;
    }
}
