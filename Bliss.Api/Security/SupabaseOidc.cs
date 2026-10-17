using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bliss.Api.Security;

// Supabase's ID token authenticates the person; a separately verified access
// token carries server-owned app_metadata. UserInfo/user_metadata never grant roles.
public static class SupabaseOidc
{
    public static void Configure(OpenIdConnectOptions options, BlissAuthenticationOptions auth)
    {
        options.GetClaimsFromUserInfoEndpoint = false;
        options.Events.OnAuthorizationCodeReceived = async context =>
        {
            var config = await options.ConfigurationManager!.GetConfigurationAsync(context.HttpContext.RequestAborted);
            if (config.TokenEndpoint != auth.Authority.TrimEnd('/') + "/oauth/token")
            {
                context.Fail("Unexpected Supabase token endpoint.");
                return;
            }
            var request = context.TokenEndpointRequest!;
            using var message = new HttpRequestMessage(HttpMethod.Post, config.TokenEndpoint);
            var credentials = Uri.EscapeDataString(auth.ClientId) + ":" + Uri.EscapeDataString(auth.ClientSecret);
            message.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials)));
            message.Content = new FormUrlEncodedContent(request.Parameters
                .Where(p => p.Key != "client_secret" && p.Key != "client_id"));
            using var response = await options.Backchannel.SendAsync(message, context.HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                context.Fail("Supabase rejected the authorization code exchange.");
                return;
            }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
            var root = json.RootElement;
            context.HandleCodeRedemption(new OpenIdConnectMessage
            {
                AccessToken = root.GetProperty("access_token").GetString(),
                IdToken = root.GetProperty("id_token").GetString(),
                TokenType = "Bearer"
            });
        };
        var existing = options.Events.OnTokenValidated;
        options.Events.OnTokenValidated = async context =>
        {
            try
            {
                var config = await options.ConfigurationManager!.GetConfigurationAsync(context.HttpContext.RequestAborted);
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                var access = handler.ValidateToken(context.TokenEndpointResponse!.AccessToken,
                    new TokenValidationParameters
                    {
                        ValidIssuer = auth.Authority.TrimEnd('/'),
                        ValidAudience = "authenticated",
                        IssuerSigningKeys = config.SigningKeys,
                        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
                        RequireExpirationTime = true, ClockSkew = TimeSpan.FromSeconds(30)
                        , ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.RsaSha256]
                    }, out _);
                ApplyVerifiedAccessToken((ClaimsIdentity)context.Principal!.Identity!, access, auth);
                await existing(context);
            }
            catch (Exception error) when (error is SecurityTokenException or JsonException or InvalidOperationException)
            {
                // Do not emit raw tokens, provider payloads, or secrets in errors.
                context.Fail("Supabase identity or test-user authorization was rejected.");
            }
        };
    }

    // Caller must first cryptographically verify both tokens. Kept separate so
    // identity binding and role isolation can be tested without a hosted provider.
    public static void ApplyVerifiedAccessToken(ClaimsIdentity identity, ClaimsPrincipal access, BlissAuthenticationOptions auth)
    {
        var subject = identity.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject) || subject != access.FindFirst("sub")?.Value
            || access.FindFirst("client_id")?.Value != auth.ClientId
            || !auth.ApprovedTestSubjects.Contains(subject, StringComparer.Ordinal))
            throw new SecurityTokenValidationException("Unapproved test identity.");

        foreach (var claim in identity.Claims.Where(c => c.Type == auth.RoleClaimType
            || c.Type == ClaimTypes.Role || c.Type == auth.AdvertiserIdClaimType
            || c.Type == auth.ReviewerWorkspaceClaimType).ToArray()) identity.RemoveClaim(claim);

        var metadataClaim = access.FindFirst("app_metadata");
        if (metadataClaim is null) return;
        using var document = JsonDocument.Parse(metadataClaim.Value);
        var metadata = document.RootElement;
        var allowedRoles = new[] { auth.ViewerRole, auth.OperatorRole, auth.ReviewerRole, auth.AdminRole, auth.AdvertiserRole };
        if (metadata.TryGetProperty("bliss_roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
            foreach (var role in roles.EnumerateArray())
                if (role.ValueKind == JsonValueKind.String && allowedRoles.Contains(role.GetString(), StringComparer.Ordinal))
                    identity.AddClaim(new Claim(auth.RoleClaimType, role.GetString()!, ClaimValueTypes.String, auth.Authority));
        if (metadata.TryGetProperty("bliss_advertiser_id", out var advertiser)
            && advertiser.ValueKind == JsonValueKind.String && Guid.TryParse(advertiser.GetString(), out var advertiserId)
            && advertiserId != Guid.Empty)
            identity.AddClaim(new Claim(auth.AdvertiserIdClaimType, advertiserId.ToString(), ClaimValueTypes.String, auth.Authority));
        if (metadata.TryGetProperty("bliss_review_workspace_ids", out var workspaces) && workspaces.ValueKind == JsonValueKind.Array)
            foreach (var workspace in workspaces.EnumerateArray())
                if (workspace.ValueKind == JsonValueKind.String && Guid.TryParse(workspace.GetString(), out var id) && id != Guid.Empty)
                    identity.AddClaim(new Claim(auth.ReviewerWorkspaceClaimType, id.ToString(), ClaimValueTypes.String, auth.Authority));
    }
}
