using System.Diagnostics;

namespace Bliss.Api.Runtime;

public static class RequestCompletionLog
{
    public static async Task InvokeAsync(HttpContext context, ILogger logger, RequestDelegate next)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation(
                "Request completed {Method} {Path} {StatusCode} {RequestId} {ElapsedMilliseconds}",
                context.Request.Method,
                RequestCorrelation.SafePath(context),
                context.Response.StatusCode,
                RequestCorrelation.Resolve(context),
                Math.Round(watch.Elapsed.TotalMilliseconds, 2));
        }
    }
}
