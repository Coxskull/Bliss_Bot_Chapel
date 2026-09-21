namespace Bliss.Api.Runtime;

public static class RequestCorrelation
{
    public const string HeaderName = "X-Request-Id";
    public const string ItemKey = "BlissRequestId";

    public static string Resolve(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var existing)
            && existing is string value
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var incoming = context.Request.Headers[HeaderName].ToString();
        var requestId = Guid.TryParse(incoming, out var parsed)
            ? parsed.ToString()
            : Guid.NewGuid().ToString();
        context.Items[ItemKey] = requestId;
        return requestId;
    }

    public static string SafePath(HttpContext context)
    {
        var path = context.Request.Path.HasValue
            ? context.Request.Path.Value!
            : "/";
        return path.Length <= 128 ? path : path[..128];
    }
}
