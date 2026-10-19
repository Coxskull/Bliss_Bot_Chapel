namespace Bliss.Api.Runtime;

// This is a different, explicit trust boundary from a fixed peer-IP allowlist.
// Railway sanitizes public-edge headers, but same-environment services can send
// arbitrary headers over private networking. Enable only after that boundary is approved.
public sealed class RailwayIngressOptions
{
    public bool Enabled { get; set; }
    public bool AllEnvironmentServicesTrusted { get; set; }
    public string ExpectedProjectId { get; set; } = string.Empty;
    public string ExpectedEnvironmentId { get; set; } = string.Empty;
}

public static class RailwayIngressTrust
{
    public static bool IsConfigured(BlissRuntimeOptions runtime, IConfiguration? configuration)
    {
        var policy = runtime.RailwayIngress;
        if (!policy.Enabled) return false;
        if (!policy.AllEnvironmentServicesTrusted
            || !Guid.TryParse(policy.ExpectedProjectId, out var project) || project == Guid.Empty
            || !Guid.TryParse(policy.ExpectedEnvironmentId, out var environment) || environment == Guid.Empty
            || configuration is null
            || !string.Equals(configuration["RAILWAY_PROJECT_ID"], policy.ExpectedProjectId, StringComparison.Ordinal)
            || !string.Equals(configuration["RAILWAY_ENVIRONMENT_ID"], policy.ExpectedEnvironmentId, StringComparison.Ordinal)
            || runtime.KnownProxies.Length != 0
            || !string.Equals(runtime.ForwardedForHeaderName, "X-Real-IP", StringComparison.Ordinal))
            throw new InvalidOperationException("Railway ingress requires explicit environment-wide trust approval, exact deployment identity, X-Real-IP and no mixed proxy allowlist.");
        return true;
    }
}
