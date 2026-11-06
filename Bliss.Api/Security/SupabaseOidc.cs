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
            || access.FindFirst("client_id")?.Value != auth.ClientId)
            throw new SecurityTokenValidationException("Unapproved identity.");

        var approvedTest = auth.ApprovedTestSubjects.Contains(subject, StringComparer.Ordinal);
        var approvedCreator = auth.Enabled && auth.SupabaseEnabled && auth.CreatorSelfServiceRequested
            && auth.ApprovedCreatorSubjects.Contains(subject, StringComparer.Ordinal);
        var approvedAdvertiser = auth.Enabled && auth.SupabaseEnabled && auth.AdvertiserSelfServiceRequested
            && auth.ApprovedAdvertiserSubjects.Contains(subject, StringComparer.Ordinal);

        // The creator lane is deliberately disjoint from privileged test/staff
        // subjects. A missing allowlist, disabled flag, or accidental overlap
        // cannot silently turn a CRM lead into an authenticated account.
        if ((approvedTest ? 1 : 0) + (approvedCreator ? 1 : 0) + (approvedAdvertiser ? 1 : 0) != 1)
            throw new SecurityTokenValidationException("Unapproved identity.");

        foreach (var claim in identity.Claims.Where(c => c.Type == auth.RoleClaimType
            || c.Type == ClaimTypes.Role || c.Type == auth.AdvertiserIdClaimType
            || c.Type == auth.ReviewerWorkspaceClaimType
            || ((approvedCreator || approvedAdvertiser) && (c.Type == auth.CreatorIdClaimType
                || c.Type == "role" || c.Type == "roles"))).ToArray())
            identity.RemoveClaim(claim);

        var metadataClaim = access.FindFirst("app_metadata");
        if (approvedCreator)
        {
            // No browser/user_metadata, CRM label or public-interest receipt may
            // grant an identity. The validated provider access token must carry
            // one staff-provisioned role, ID and explicit verification mark.
            if (metadataClaim is null || string.IsNullOrWhiteSpace(auth.CreatorRole)
                || string.IsNullOrWhiteSpace(auth.CreatorIdClaimType)
                || auth.CreatorRole == auth.AdminRole
                || auth.CreatorRole == auth.OperatorRole
                || auth.CreatorRole == auth.ReviewerRole
                || auth.CreatorRole == auth.AdvertiserRole
                || auth.CreatorRole == auth.ViewerRole
                || auth.CreatorRole == auth.AskAlphaVisitorRole
                || identity.FindAll("email_verified").Count() != 1
                || !string.Equals(identity.FindFirst("email_verified")?.Value,
                    "true", StringComparison.OrdinalIgnoreCase))
                throw new SecurityTokenValidationException("Creator verification is incomplete.");

            using var creatorDocument = JsonDocument.Parse(metadataClaim.Value);
            var creatorMetadata = creatorDocument.RootElement;
            if (creatorMetadata.ValueKind != JsonValueKind.Object
                || !creatorMetadata.TryGetProperty("bliss_roles", out var creatorRoles)
                || creatorRoles.ValueKind != JsonValueKind.Array
                || creatorRoles.GetArrayLength() != 1
                || creatorRoles[0].ValueKind != JsonValueKind.String
                || creatorRoles[0].GetString() != auth.CreatorRole
                || !creatorMetadata.TryGetProperty("bliss_creator_verified", out var verified)
                || verified.ValueKind != JsonValueKind.True
                || !creatorMetadata.TryGetProperty("bliss_creator_id", out var creatorValue)
                || creatorValue.ValueKind != JsonValueKind.String
                || !Guid.TryParse(creatorValue.GetString(), out var creatorId)
                || creatorId == Guid.Empty
                || creatorMetadata.TryGetProperty("bliss_advertiser_id", out _)
                || creatorMetadata.TryGetProperty("bliss_review_workspace_ids", out _))
                throw new SecurityTokenValidationException("Creator verification is incomplete.");

            identity.AddClaim(new Claim(auth.RoleClaimType, auth.CreatorRole,
                ClaimValueTypes.String, auth.Authority));
            identity.AddClaim(new Claim(auth.CreatorIdClaimType, creatorId.ToString("D"),
                ClaimValueTypes.String, auth.Authority));
            return;
        }

        if (approvedAdvertiser)
        {
            // Only issuer-validated staff-owned app_metadata can activate a
            // customer account. No public intake/CRM data or editable
            // user_metadata is ever consulted.
            if (metadataClaim is null || string.IsNullOrWhiteSpace(auth.AdvertiserRole)
                || string.IsNullOrWhiteSpace(auth.AdvertiserIdClaimType)
                || auth.AdvertiserRole == auth.CreatorRole
                || auth.AdvertiserRole == auth.AdminRole
                || auth.AdvertiserRole == auth.OperatorRole
                || auth.AdvertiserRole == auth.ReviewerRole
                || auth.AdvertiserRole == auth.ViewerRole
                || identity.FindAll("email_verified").Count() != 1
                || !string.Equals(identity.FindFirst("email_verified")?.Value, "true", StringComparison.OrdinalIgnoreCase))
                throw new SecurityTokenValidationException("Advertiser verification is incomplete.");

            using var advertiserDocument = JsonDocument.Parse(metadataClaim.Value);
            var advertiserMetadata = advertiserDocument.RootElement;
            if (advertiserMetadata.ValueKind != JsonValueKind.Object
                || !advertiserMetadata.TryGetProperty("bliss_roles", out var advertiserRoles)
                || advertiserRoles.ValueKind != JsonValueKind.Array
                || advertiserRoles.GetArrayLength() != 1
                || advertiserRoles[0].ValueKind != JsonValueKind.String
                || advertiserRoles[0].GetString() != auth.AdvertiserRole
                || !advertiserMetadata.TryGetProperty("bliss_advertiser_verified", out var businessVerified)
                || businessVerified.ValueKind != JsonValueKind.True
                || !advertiserMetadata.TryGetProperty("bliss_advertiser_id", out var advertiserValue)
                || advertiserValue.ValueKind != JsonValueKind.String
                || !Guid.TryParse(advertiserValue.GetString(), out var advertiserId)
                || advertiserId == Guid.Empty
                || advertiserMetadata.TryGetProperty("bliss_creator_id", out _)
                || advertiserMetadata.TryGetProperty("bliss_review_workspace_ids", out _))
                throw new SecurityTokenValidationException("Advertiser verification is incomplete.");

            identity.AddClaim(new Claim(auth.RoleClaimType, auth.AdvertiserRole,
                ClaimValueTypes.String, auth.Authority));
            identity.AddClaim(new Claim(auth.AdvertiserIdClaimType, advertiserId.ToString("D"),
                ClaimValueTypes.String, auth.Authority));
            return;
        }

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
