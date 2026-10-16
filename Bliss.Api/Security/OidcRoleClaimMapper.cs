using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Bliss.Api.Security;

public static class OidcRoleClaimMapper
{
    public static void Configure(OpenIdConnectOptions options, BlissAuthenticationOptions authentication)
    {
        options.Events.OnTokenValidated = context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity)
            {
                Normalize(identity, authentication.RoleClaimType);
                // Only this post-signature-validation callback can project a
                // visitor role whose source issuer matches the approved IdP.
                // Never trust a user-info claim or caller-supplied role alone.
                ProjectValidatedVisitorRole(identity, authentication);
                NormalizeReviewerWorkspaceAssignments(identity, authentication.ReviewerWorkspaceClaimType);
            }

            return Task.CompletedTask;
        };
        options.Events.OnUserInformationReceived = context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity && context.User is not null)
            {
                Apply(identity, context.User.RootElement, authentication.RoleClaimType);
                // UserInfo arrives AFTER token validation. An unverified
                // UserInfo role must never preserve a previously projected
                // dedicated visitor identity, even if the role value matches.
                InvalidateVisitorProjectionIfUserInfoContainsRoles(
                    identity, context.User.RootElement, authentication);
                ApplyReviewerWorkspaceAssignments(identity, context.User.RootElement, authentication.ReviewerWorkspaceClaimType);
            }

            return Task.CompletedTask;
        };
    }

    public static void Apply(ClaimsIdentity identity, JsonElement user, string roleClaimType)
    {
        if (identity is null
            || user.ValueKind != JsonValueKind.Object
            || string.IsNullOrWhiteSpace(roleClaimType)
            || !user.TryGetProperty(roleClaimType, out var property))
        {
            return;
        }

        foreach (var role in ReadProperty(property))
        {
            AddRole(identity, roleClaimType, role);
        }
    }

    public static void Normalize(ClaimsIdentity identity, string roleClaimType)
    {
        if (identity is null || string.IsNullOrWhiteSpace(roleClaimType))
        {
            return;
        }

        // Keep issuer provenance from the OIDC token. Reissuing role claims
        // with the default LOCAL AUTHORITY issuer loses the signed identity
        // context and cannot satisfy the visitor issuer-bound preflight.
        var expanded = identity.FindAll(roleClaimType)
            .SelectMany(claim => Expand(claim.Value)
                .Select(role => (
                    Value: role,
                    Issuer: claim.Issuer,
                    OriginalIssuer: claim.OriginalIssuer)))
            .Distinct()
            .ToList();
        if (expanded.Count == 0)
        {
            return;
        }

        foreach (var existing in identity.FindAll(roleClaimType).ToList())
        {
            identity.RemoveClaim(existing);
        }

        foreach (var role in expanded)
        {
            identity.AddClaim(new Claim(roleClaimType, role.Value,
                ClaimValueTypes.String, role.Issuer, role.OriginalIssuer));
        }
    }

    // Call ONLY after ASP.NET's OIDC handler has validated the ID token.
    // This is a role claim projection into the visitor gate's exact
    // ClaimTypes.Role format, NOT an authentication, enrollment or consent
    // decision. UserInfo roles are intentionally never projected here.
    public static void ProjectValidatedVisitorRole(
        ClaimsIdentity identity, BlissAuthenticationOptions authentication)
    {
        // Visitor-role projection is a prerequisite only, and must not
        // create visitor-role claims when the dedicated feature is disabled
        // or the configured authority differs from the visitor issuer.
        if (!authentication.Enabled ||
            !authentication.AskAlphaVisitorEnrollmentRequested ||
            string.IsNullOrWhiteSpace(authentication.ClientId) ||
            !Uri.TryCreate(authentication.Authority, UriKind.Absolute, out var authority) ||
            !Uri.TryCreate(authentication.AskAlphaVisitorIssuer, UriKind.Absolute, out var visitorIssuer) ||
            authority.Scheme != Uri.UriSchemeHttps ||
            visitorIssuer.Scheme != Uri.UriSchemeHttps ||
            authority.UserInfo.Length != 0 || visitorIssuer.UserInfo.Length != 0 ||
            authority.Query.Length != 0 || visitorIssuer.Query.Length != 0 ||
            authority.Fragment.Length != 0 || visitorIssuer.Fragment.Length != 0 ||
            Uri.Compare(authority, visitorIssuer,
                UriComponents.SchemeAndServer | UriComponents.Path,
                UriFormat.Unescaped, StringComparison.Ordinal) != 0 ||
            !identity.IsAuthenticated ||
            string.IsNullOrWhiteSpace(authentication.AskAlphaVisitorIssuer) ||
            string.IsNullOrWhiteSpace(authentication.AskAlphaVisitorRole) ||
            string.IsNullOrWhiteSpace(authentication.RoleClaimType) ||
            authentication.RoleClaimType == ClaimTypes.Role ||
            authentication.AskAlphaVisitorRole == authentication.AdvertiserRole ||
            authentication.AskAlphaVisitorRole == authentication.ViewerRole ||
            authentication.AskAlphaVisitorRole == authentication.OperatorRole ||
            authentication.AskAlphaVisitorRole == authentication.ReviewerRole ||
            authentication.AskAlphaVisitorRole == authentication.AdminRole)
            return;

        var roles = identity.FindAll(authentication.RoleClaimType).ToArray();
        if (roles.Length != 1 ||
            roles[0].Value != authentication.AskAlphaVisitorRole ||
            roles[0].Issuer != authentication.AskAlphaVisitorIssuer ||
            // Do not silently turn a staff or mixed-role principal into a
            // dedicated visitor identity.
            identity.HasClaim(c => c.Type == ClaimTypes.Role ||
                (c.Type == "role" && authentication.RoleClaimType != "role") ||
                (c.Type == "roles" && authentication.RoleClaimType != "roles")))
            return;

        identity.AddClaim(new Claim(ClaimTypes.Role,
            authentication.AskAlphaVisitorRole, ClaimValueTypes.String,
            authentication.AskAlphaVisitorIssuer,
            authentication.AskAlphaVisitorIssuer));
    }

    // Only the signed ID token is eligible to supply a dedicated visitor
    // role. The UserInfo endpoint is not a second issuer-trusted source of
    // role authorization. If it supplies ANY roles property, even a duplicate
    // of the signed role, fail closed by withdrawing the derived projection.
    // Staff/advertiser role claims are never removed here.
    public static void InvalidateVisitorProjectionIfUserInfoContainsRoles(
        ClaimsIdentity identity, JsonElement user, BlissAuthenticationOptions authentication)
    {
        if (identity is null || user.ValueKind != JsonValueKind.Object ||
            string.IsNullOrWhiteSpace(authentication.RoleClaimType) ||
            !user.TryGetProperty(authentication.RoleClaimType, out _))
            return;

        foreach (var projected in identity.FindAll(ClaimTypes.Role)
            .Where(claim => claim.Value == authentication.AskAlphaVisitorRole &&
                claim.Issuer == authentication.AskAlphaVisitorIssuer &&
                claim.OriginalIssuer == authentication.AskAlphaVisitorIssuer)
            .ToArray())
            identity.RemoveClaim(projected);
    }

    // Accept one workspace GUID per verified claim or an array supplied by the
    // identity provider. Never accept wildcards, empty/invalid GUIDs, or user
    // request values as reviewer assignments.
    public static void NormalizeReviewerWorkspaceAssignments(ClaimsIdentity identity, string claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType)) return;
        var authorized = identity.FindAll(claimType)
            .SelectMany(claim => Expand(claim.Value))
            .Select(value => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty)
            .Where(value => value != Guid.Empty)
            .Distinct()
            .ToArray();
        foreach (var existing in identity.FindAll(claimType).ToList()) identity.RemoveClaim(existing);
        foreach (var workspace in authorized) identity.AddClaim(new Claim(claimType, workspace.ToString("D")));
    }

    public static void ApplyReviewerWorkspaceAssignments(ClaimsIdentity identity, JsonElement user, string claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType) || user.ValueKind != JsonValueKind.Object
            || !user.TryGetProperty(claimType, out var property)) return;
        foreach (var value in ReadProperty(property))
        {
            if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
                && !identity.HasClaim(claimType, parsed.ToString("D")))
                identity.AddClaim(new Claim(claimType, parsed.ToString("D")));
        }
    }

    public static ClaimsIdentity Align(ClaimsIdentity identity, string nameClaimType, string roleClaimType)
    {
        var resolvedName = string.IsNullOrWhiteSpace(nameClaimType) ? identity.NameClaimType : nameClaimType.Trim();
        var resolvedRole = string.IsNullOrWhiteSpace(roleClaimType) ? identity.RoleClaimType : roleClaimType.Trim();
        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in identity.Claims)
        {
            if (claim.Type == resolvedRole
                || claim.Type == ClaimTypes.Role
                || claim.Type == "role"
                || claim.Type == "roles")
            {
                foreach (var role in Expand(claim.Value))
                {
                    roles.Add(role);
                }
            }
        }

        var aligned = new ClaimsIdentity(
            identity.AuthenticationType,
            resolvedName,
            resolvedRole);
        foreach (var claim in identity.Claims)
        {
            if (claim.Type == resolvedRole)
            {
                continue;
            }

            aligned.AddClaim(claim);
        }

        foreach (var role in roles)
        {
            AddRole(aligned, resolvedRole, role);
        }

        return aligned;
    }

    public static IReadOnlyList<string> Expand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return document.RootElement.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString()?.Trim() ?? string.Empty)
                        .Where(item => item.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                }
            }
            catch (JsonException)
            {
                // A role value may legally start with '[' without being JSON.
            }
        }

        if (trimmed.Contains(','))
        {
            return trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return [trimmed];
    }

    private static IEnumerable<string> ReadProperty(JsonElement property)
    {
        if (property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    foreach (var role in Expand(item.GetString()))
                    {
                        yield return role;
                    }
                }
            }

            yield break;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            foreach (var role in Expand(property.GetString()))
            {
                yield return role;
            }
        }
    }

    private static void AddRole(ClaimsIdentity identity, string roleClaimType, string role)
    {
        if (role.Length == 0 || identity.HasClaim(roleClaimType, role))
        {
            return;
        }

        identity.AddClaim(new Claim(roleClaimType, role));
    }
}
