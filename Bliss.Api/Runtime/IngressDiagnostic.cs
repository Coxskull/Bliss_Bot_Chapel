using Microsoft.AspNetCore.Http;

namespace Bliss.Api.Runtime;

public sealed record IngressObservation(Guid ProbeId, string PeerAddress, bool TransportHttps);

public static class IngressDiagnostic
{
    public static IngressObservation? Observe(HttpContext context, BlissRuntimeOptions options)
    {
        if (!options.RestrictedPreview || !options.IngressDiagnostics
            || !HttpMethods.IsGet(context.Request.Method)
            || context.Request.Path != "/health/live"
            || !Guid.TryParse(context.Request.Query["bliss-ingress-probe"], out var probe)) return null;
        // Called before forwarded-header middleware. Never derive trust from
        // caller-controlled X-Forwarded-For, X-Real-IP or other claimed identity.
        return new(probe, context.Connection.RemoteIpAddress?.ToString() ?? "unknown", context.Request.IsHttps);
    }
}
