using System.Text.Json;
using Bliss.Api.Runtime;

namespace Bliss.Tests.Api;

public sealed class HostedOidcDiscoveryTests
{
    private const string Authority = "https://fictional-tenant.supabase.co/auth/v1";

    private static string Discovery(string issuer = Authority, string? token = null, string? jwks = null)
        => JsonSerializer.Serialize(new
        {
            issuer,
            token_endpoint = token ?? Authority + "/oauth/token",
            jwks_uri = jwks ?? Authority + "/.well-known/jwks.json"
        });

    [Theory]
    [InlineData("https://fictional-tenant.supabase.co/auth/v1", true)]
    [InlineData("https://fictional-tenant.supabase.co/auth/v1/", true)]
    [InlineData("https://fictional-tenant.supabase.co", false)]
    [InlineData("http://fictional-tenant.supabase.co/auth/v1", false)]
    [InlineData("https://127.0.0.1/auth/v1", false)]
    [InlineData("https://fictional-tenant.supabase.co.evil.test/auth/v1", false)]
    [InlineData("https://name:password@fictional-tenant.supabase.co/auth/v1", false)]
    [InlineData("https://fictional-tenant.supabase.co/auth/v1?to=other", false)]
    [InlineData("https://fictional-tenant.supabase.co:444/auth/v1", false)]
    [InlineData("", false)]
    public void Authority_validation_is_host_restricted_and_tls_only(string input, bool expected)
        => Assert.Equal(expected, HostedOidcDiscovery.TryValidateAuthority(input, out _));

    [Fact]
    public void Exact_provider_metadata_with_internal_https_jwks_passes()
    {
        Assert.True(HostedOidcDiscovery.TryValidateAuthority(Authority, out var uri));
        Assert.True(HostedOidcDiscovery.TryValidateMetadata(uri, Discovery(), out var jwks));
        Assert.Equal(Authority + "/.well-known/jwks.json", jwks.AbsoluteUri);
    }

    [Theory]
    [InlineData("https://other-tenant.supabase.co/auth/v1", null, null)]
    [InlineData(null, "https://fictional-tenant.supabase.co/auth/v1/token", null)]
    [InlineData(null, null, "https://attacker.invalid/jwks.json")]
    [InlineData(null, null, "http://fictional-tenant.supabase.co/auth/v1/.well-known/jwks.json")]
    [InlineData(null, null, "https://fictional-tenant.supabase.co/auth/v1/.well-known/jwks.json?private=yes")]
    [InlineData(null, null, "https://fictional-tenant.supabase.co.evil.test/auth/v1/.well-known/jwks.json")]
    [InlineData(null, null, "https://fictional-tenant.supabase.co/not-our-issuer/jwks.json")]
    public void Different_issuer_token_endpoint_or_redirected_jwks_is_refused(
        string? issuer, string? token, string? jwks)
    {
        Assert.True(HostedOidcDiscovery.TryValidateAuthority(Authority, out var uri));
        Assert.False(HostedOidcDiscovery.TryValidateMetadata(uri,
            Discovery(issuer ?? Authority, token, jwks), out _));
    }

    [Theory]
    [InlineData("{\"keys\":[{\"kid\":\"test-key\",\"kty\":\"EC\",\"alg\":\"ES256\",\"use\":\"sig\"}]}", true)]
    [InlineData("{\"keys\":[{\"kid\":\"test-key\",\"kty\":\"RSA\",\"alg\":\"RS256\"}]}", true)]
    [InlineData("{\"keys\":[]}", false)]
    [InlineData("{\"keys\":[{\"kid\":\"\",\"kty\":\"EC\",\"alg\":\"ES256\"}]}", false)]
    [InlineData("{\"keys\":[{\"kid\":\"test-key\",\"kty\":\"oct\",\"alg\":\"HS256\"}]}", false)]
    [InlineData("{\"keys\":[{\"kid\":\"test-key\",\"kty\":\"EC\",\"use\":\"enc\"}]}", false)]
    [InlineData("{\"keys\":[{\"kid\":\"test-key\",\"kty\":\"EC\",\"alg\":\"none\"}]}", false)]
    [InlineData("not-json", false)]
    public void Public_oidc_signing_keys_are_required(string json, bool expected)
        => Assert.Equal(expected, HostedOidcDiscovery.HasOidcSigningKeys(json));

    [Fact]
    public async Task Disabled_or_missing_configuration_is_denied_without_any_web_request()
    {
        Assert.Equal("DENIED_UNCONFIGURED_AUTH",
            await HostedOidcDiscovery.RunAsync(Authority, false, true, "client"));
        Assert.Equal("DENIED_UNCONFIGURED_AUTH",
            await HostedOidcDiscovery.RunAsync(Authority, true, false, "client"));
        Assert.Equal("DENIED_UNCONFIGURED_AUTH",
            await HostedOidcDiscovery.RunAsync(Authority, true, true, ""));
        Assert.Equal("DENIED_UNCONFIGURED_AUTH",
            await HostedOidcDiscovery.RunAsync("http://127.0.0.1", true, true, "client"));
    }
}
