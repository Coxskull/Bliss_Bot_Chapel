using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Bliss.Api.Runtime;

public static class TrustedForwarding
{
    public static void Configure(ForwardedHeadersOptions options, BlissRuntimeOptions runtime, IConfiguration? configuration = null)
    {
        var header = runtime.ForwardedForHeaderName.Trim();
        if (!string.Equals(header, "X-Forwarded-For", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(header, "X-Real-IP", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Runtime:ForwardedForHeaderName must be X-Forwarded-For or X-Real-IP.");
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        // Railway documents X-Real-IP as its edge-owned client-address header.
        // Selecting it does not change the requirement for explicit trusted peers.
        options.ForwardedForHeaderName = header;
        if (RailwayIngressTrust.IsConfigured(runtime, configuration))
        {
            // Provider contract: public edge overwrites both headers. Private
            // peers remain trusted by the separately approved environment policy.
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();
            options.RequireHeaderSymmetry = true;
            return;
        }
        foreach (var value in runtime.KnownProxies)
        {
            if (!IPAddress.TryParse(value, out var address))
                throw new InvalidOperationException("Runtime:KnownProxies contains an invalid IP address.");
            if (!options.KnownProxies.Contains(address)) options.KnownProxies.Add(address);
        }
    }
}
