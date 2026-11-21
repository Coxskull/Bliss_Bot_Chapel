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
        // Supabase custom OAuth consent screen follows the verified 302 challenge.
        // These are only static sign-in assets, not additional business APIs.
        "/oauth-consent.html", "/oauth-consent.css", "/oauth-consent.bundle.js",
        "/api/auth/supabase-config", "/api/auth/session", "/api/auth/login",
        "/signin-oidc", "/signout-callback-oidc"
    };

    private static readonly HashSet<string> PublicPostPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/advertisers/register", "/api/auth/logout",
        // OIDC uses the framework-protected code/nonce/state callback. Some
        // providers send the authorization response as a form POST.
        "/signin-oidc", "/signout-callback-oidc"
    };

    public static bool IsAllowed(string? method, PathString path)
    {
        var value = path.Value ?? "";
        return (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            ? PublicGetPaths.Contains(value)
            : string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
                && PublicPostPaths.Contains(value);
    }

    public static async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        context.Response.Headers["X-Bliss-Mode"] = "advertiser-registration-only";
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path == "/preview-status")
        {
            await context.Response.WriteAsJsonAsync(new
            {
                restrictedPreview = true,
                operational = false,
                advertiserRegistrationOpen = true
            });
            return;
        }

        if (IsAllowed(context.Request.Method, context.Request.Path))
        {
            await next();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            status = "Unavailable",
            mode = "advertiser-registration-only",
            operational = false,
            error = "This service is limited to advertiser registration. Other operations remain unavailable."
        });
    }
}
