namespace Bliss.Api.Runtime;

// This gate runs BEFORE static files and controllers. Preview cannot expose
// business records, generated media, authentication actions or any write route.
public static class RestrictedPreviewGate
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".html", ".css", ".js", ".jpg", ".jpeg", ".png", ".svg", ".ico", ".woff", ".woff2" };

    public static bool IsInterfacePath(PathString path) =>
        !path.StartsWithSegments("/api")
        && !path.StartsWithSegments("/operations/generated")
        && !path.StartsWithSegments("/source-media")
        && !path.StartsWithSegments("/operations/financial-review")
        && (path == "/" || path == "/operations" || path == "/health/live"
            || Extensions.Contains(Path.GetExtension(path.Value ?? "")));

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
