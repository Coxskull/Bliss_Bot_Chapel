namespace Bliss.Api.Runtime;

// This gate runs BEFORE static files and controllers. Preview cannot expose
// business records, generated media, authentication actions or any write route.
public static class RestrictedPreviewGate
{
    // Explicitly expose only the public Bliss landing page and its reviewed assets.
    // A filename extension does not grant publication rights: other public-folder
    // HTML pages and /operations can contain internal workflows and must stay closed.
    private static readonly HashSet<string> PublicInterfacePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/", "/index.html", "/health/live",
        "/wedding-planner.css", "/wedding-planner.js", "/preview-lock.js",
        "/assets/bliss-chapel-hero.jpg", "/favicon.ico"
    };

    public static bool IsInterfacePath(PathString path) =>
        PublicInterfacePaths.Contains(path.Value ?? string.Empty);

    public static async Task InvokeAsync(HttpContext context, Func<Task> next)
    {
        context.Response.Headers["X-Bliss-Mode"] = "restricted-preview";
        context.Response.Headers.CacheControl = "no-store";
        if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/preview-status")
        {
            await context.Response.WriteAsJsonAsync(new { restrictedPreview = true, operational = false });
            return;
        }
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && IsInterfacePath(context.Request.Path))
        {
            await next();
            return;
        }
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            status = "Unavailable", mode = "restricted-preview", operational = false,
            error = "Interface preview only. Data, login, writes and commercial actions are locked until setup is complete."
        });
    }
}
