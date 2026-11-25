using Microsoft.AspNetCore.Http;

namespace Bliss.Api.Runtime;

/// <summary>
/// Narrow public advertiser onboarding surface while all other Bliss business
/// APIs and pages stay in restricted-preview mode. Separate from the Factory STOP.
/// Only activate with ProductionPostureEvaluator PASS and explicit opt-in flags.
/// </summary>
public static class PublicAdvertiserRegistrationGate
{
    private static readonly HashSet<string> PublicGetPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/index.html", "/health/live",
        "/preview-status",
        "/wedding-planner.css", "/wedding-planner.js", "/preview-lock.js",
        "/assets/bliss-chapel-hero.jpg", "/favicon.ico",
        "/advertiser-registration.html", "/advertiser-registration.js",
        "/advertiser-account-privacy.html", "/advertiser-account-terms.html",
        "/advertiser-login.html", "/alpha-intake.css", "/contact.html",
        // Static dashboard shell; exact read-only endpoints enforce issuer/subject authorization.
        "/advertiser-dashboard.html", "/advertiser-dashboard.js", "/creator-dashboard.css",
        "/api/advertisers/registration-status", "/api/advertisers/me",
        "/api/advertisers/my-campaign-drafts", "/advertiser-campaign-drafts.js",
        "/advertiser-operator-review.html", "/advertiser-operator-review.js",
        "/api/advertisers/operator-review",
        // Supabase custom OAuth consent screen follows the verified 302 challenge.
        // These are only static sign-in assets, not additional business APIs.
        "/oauth-consent.html", "/oauth-consent.css", "/oauth-consent.bundle.js",
        "/api/auth/supabase-config", "/api/auth/session", "/api/auth/login",
        "/signin-oidc", "/signout-callback-oidc"
    };

    private static readonly HashSet<string> PublicPostPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/advertisers/register", "/api/auth/logout",
        "/api/advertisers/my-campaign-drafts",
        "/api/advertisers/my-campaign-drafts/creative-handoff-preview",
        "/api/advertisers/operator-review",
        // OIDC uses the framework-protected code/nonce/state callback. Some
        // providers send the authorization response as a form POST.
        "/signin-oidc", "/signout-callback-oidc"
    };

    // The creator addition never changes the advertiser allowlist; each new
    // route is gated independently and public business APIs stay closed.
    private static readonly HashSet<string> CreatorGetPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/creator-registration.html", "/creator-registration.js",
        "/creator-dashboard.html", "/creator-dashboard.js",
        "/api/creators/registration", "/api/creators/operator-review",
        "/creator-operator-review.html", "/creator-operator-review.js",
        "/alpha-privacy.html", "/alpha-terms.html",
        "/creator-account-privacy.html", "/creator-account-terms.html",
        "/join-creator.html", "/alpha-intake.js"
    };
    private static readonly HashSet<string> CreatorPostPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/creators/register", "/api/creators/operator-review", "/api/creators/operator-review/verify-channel"
    };

    public static bool IsAllowed(string? method, PathString path, bool creatorRegistrationEnabled = false, bool staffCreativeReadinessEnabled = false)
    {
        var value = path.Value ?? "";
        if (staffCreativeReadinessEnabled && string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            && string.Equals(value, "/api/internal/creative-readiness", StringComparison.OrdinalIgnoreCase)) return true;
        if (creatorRegistrationEnabled)
        {
            if ((string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
                && CreatorGetPaths.Contains(value)) return true;
            if (string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                && CreatorPostPaths.Contains(value)) return true;
        }
        return (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            ? PublicGetPaths.Contains(value)
            : string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                && PublicPostPaths.Contains(value);
    }

    public static async Task InvokeAsync(HttpContext context, Func<Task> next,
        bool creatorRegistrationEnabled = false, bool staffCreativeReadinessEnabled = false)
    {
        var releaseMode = creatorRegistrationEnabled
            ? "advertiser-and-creator-registration" : "advertiser-registration-only";
        context.Response.Headers["X-Bliss-Mode"] = releaseMode;
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path == "/preview-status")
        {
            await context.Response.WriteAsJsonAsync(new
            {
                restrictedPreview = true,
                operational = false,
                advertiserRegistrationOpen = true,
                creatorRegistrationOpen = creatorRegistrationEnabled
            });
            return;
        }

        if (IsAllowed(context.Request.Method, context.Request.Path, creatorRegistrationEnabled, staffCreativeReadinessEnabled))
        {
            await next();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            status = "Unavailable",
            mode = releaseMode,
            operational = false,
            error = "This service is limited to advertiser registration. Other operations remain unavailable."
        });
    }
}
