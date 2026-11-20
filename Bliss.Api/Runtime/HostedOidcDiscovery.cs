using System.Text.Json;

namespace Bliss.Api.Runtime;

// A non-mutating, one-shot hosted issuer preflight. This verifies that Railway
// can reach the exact configured Supabase OIDC discovery document and its public
// signature keys. It neither exchanges codes nor creates Auth/users/DB records.
public static class HostedOidcDiscovery
{
    public const string Pass = "PASS_METADATA_AND_JWKS_NOT_CUSTOMER_LOGIN";

    public static async Task<string> RunAsync(string? authority, bool authEnabled, bool supabaseEnabled,
        string? clientId, CancellationToken cancellationToken = default)
    {
        if (!authEnabled || !supabaseEnabled || string.IsNullOrWhiteSpace(clientId)
            || !TryValidateAuthority(authority, out var issuer))
            return "DENIED_UNCONFIGURED_AUTH";

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(12),
            MaxResponseContentBufferSize = 65536
        };
        try
        {
            // No credentials or public account data are sent; allow only this
            // fixed provider discovery path and an in-scope issuer JWKS URL.
            using var metadata = await http.GetAsync(
                issuer.AbsoluteUri.TrimEnd('/') + "/.well-known/openid-configuration",
                deadline.Token);
            if (!metadata.IsSuccessStatusCode) return "FAILED_DISCOVERY_HTTP";
            var body = await metadata.Content.ReadAsStringAsync(deadline.Token);
            if (!TryValidateMetadata(issuer, body, out var jwks))
                return "FAILED_DISCOVERY_IDENTITY";
            using var keys = await http.GetAsync(jwks, deadline.Token);
            if (!keys.IsSuccessStatusCode) return "FAILED_SIGNING_KEYS_HTTP";
            var keyBody = await keys.Content.ReadAsStringAsync(deadline.Token);
            if (!HasOidcSigningKeys(keyBody)) return "FAILED_SIGNING_KEYS_UNUSABLE";
            return Pass;
        }
        catch (OperationCanceledException) { return "FAILED_DISCOVERY_TIMEOUT"; }
        catch (HttpRequestException) { return "FAILED_DISCOVERY_NETWORK"; }
        catch (JsonException) { return "FAILED_DISCOVERY_JSON"; }
        catch (ArgumentException) { return "FAILED_DISCOVERY_INVALID_URL"; }
    }

    public static bool TryValidateAuthority(string? text, out Uri authority)
    {
        authority = null!;
        if (string.IsNullOrWhiteSpace(text)
            || !Uri.TryCreate(text.TrimEnd('/'), UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps || parsed.Port != 443
            || !parsed.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase)
            || parsed.Host.Equals("supabase.co", StringComparison.OrdinalIgnoreCase)
            || parsed.AbsolutePath != "/auth/v1"
            || !string.IsNullOrEmpty(parsed.Query) || !string.IsNullOrEmpty(parsed.Fragment)
            || !string.IsNullOrEmpty(parsed.UserInfo))
            return false;
        authority = parsed;
        return true;
    }

    public static bool TryValidateMetadata(Uri issuer, string body, out Uri jwks)
    {
        jwks = null!;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("issuer", out var issuerProp)
                || issuerProp.ValueKind != JsonValueKind.String
                || issuerProp.GetString() != issuer.AbsoluteUri.TrimEnd('/')
                || !root.TryGetProperty("token_endpoint", out var tokenProp)
                || tokenProp.ValueKind != JsonValueKind.String
                || tokenProp.GetString() != issuer.AbsoluteUri.TrimEnd('/') + "/oauth/token"
                || !root.TryGetProperty("jwks_uri", out var jwksProp)
                || jwksProp.ValueKind != JsonValueKind.String
                || !Uri.TryCreate(jwksProp.GetString(), UriKind.Absolute, out var candidate)
                || candidate.Scheme != Uri.UriSchemeHttps
                || candidate.Host != issuer.Host || candidate.Port != issuer.Port
                || !candidate.AbsolutePath.StartsWith(issuer.AbsolutePath + "/", StringComparison.Ordinal)
                || !string.IsNullOrEmpty(candidate.Query)
                || !string.IsNullOrEmpty(candidate.Fragment)
                || !string.IsNullOrEmpty(candidate.UserInfo))
                return false;
            jwks = candidate;
            return true;
        }
        catch (JsonException) { return false; }
    }

    public static bool HasOidcSigningKeys(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("keys", out var keys)
                || keys.ValueKind != JsonValueKind.Array) return false;
            return keys.EnumerateArray().Any(key =>
                key.ValueKind == JsonValueKind.Object
                && key.TryGetProperty("kid", out var kid)
                && kid.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(kid.GetString())
                && key.TryGetProperty("kty", out var type)
                && type.ValueKind == JsonValueKind.String
                && (type.GetString() is "EC" or "RSA")
                && (!key.TryGetProperty("use", out var use)
                    || use.ValueKind == JsonValueKind.String && use.GetString() == "sig")
                && (!key.TryGetProperty("alg", out var alg)
                    || alg.ValueKind == JsonValueKind.String
                        && (alg.GetString() is "ES256" or "RS256")));
        }
        catch (JsonException) { return false; }
    }
}
