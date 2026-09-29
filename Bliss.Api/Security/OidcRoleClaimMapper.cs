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
            }

            return Task.CompletedTask;
        };
        options.Events.OnUserInformationReceived = context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity && context.User is not null)
            {
                Apply(identity, context.User.RootElement, authentication.RoleClaimType);
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

        var expanded = identity.FindAll(roleClaimType)
            .SelectMany(claim => Expand(claim.Value))
            .Distinct(StringComparer.Ordinal)
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
            identity.AddClaim(new Claim(roleClaimType, role));
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
