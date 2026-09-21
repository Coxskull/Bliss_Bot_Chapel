namespace Bliss.Api.Runtime;

public sealed class BlissRuntimeOptions
{
    public const string SectionName = "Runtime";

    public string DataProtectionKeysPath { get; set; } = string.Empty;
    public string[] KnownProxies { get; set; } = [];
    public int WriteRateLimitPermitLimit { get; set; } = 30;
    public int AuthenticationRateLimitPermitLimit { get; set; } = 10;
    public int RateLimitWindowSeconds { get; set; } = 60;
}

public static class BlissRateLimitPolicies
{
    public const string Authentication = "authentication";
    public const string Writes = "writes";
}
