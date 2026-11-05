namespace Bliss.Api.Runtime;

// COM-261-004: keep an engineering-only fictional form outside HTTP publication.
// The Vercel builder already excludes these files; Railway's public static-file
// middleware could otherwise publish them when RestrictedPreview is turned off.
// Apply this guard in EVERY runtime mode, BEFORE UseStaticFiles.
public static class CreatorRegistrationPreviewIsolation
{
    public static bool IsBlocked(PathString path)
    {
        var value = path.Value;
        return string.Equals(value, "/creator-registration-preview.html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "/creator-registration-preview.js", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        if (IsBlocked(context.Request.Path))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    }
}
