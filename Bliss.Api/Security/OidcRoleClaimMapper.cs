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
                // UserInfo is a profile source, never a second source of
                // staff roles or reviewer workspace authorization. Only
                // token-validated claims may grant these scopes.
                IgnoreUntrustedUserInfoAuthorizationClaims(
                    identity, context.User.RootElement, authentication);
            }

            return Task.CompletedTask;
        };
    }

    // Compatibility boundary only. Caller-supplied UserInfo JSON is not
    // cryptographically verified authorization evidence. Earlier behavior
    // silently created staff/advertiser/visitor role claims with the default
    // LOCAL AUTHORITY origin. Deliberately deny that grant path while keeping
    // the signature so older callers cannot accidentally restore escalation.
    [Obsolete("Untrusted UserInfo cannot issue role claims. Use token-validated Normalize instead.")]
    public static void Apply(ClaimsIdentity identity, JsonElement user, string roleClaimType)
    {
        // Deliberately no-op: never alter any role claims from UserInfo.
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
            authentication.AskAlphaVisitorRole == authentication.AdminRole ||
            // Visitor ownership is NEW and server-issued. The incoming
            // identity must not also carry privileged tenant/advertiser
            // assignments or an existing visitor session identifier.
            // Presence alone denies, including blank or malformed values.
            string.IsNullOrWhiteSpace(authentication.AdvertiserIdClaimType) ||
            string.IsNullOrWhiteSpace(authentication.ReviewerWorkspaceClaimType) ||
            string.IsNullOrWhiteSpace(authentication.AskAlphaVisitorSessionClaimType) ||
            HasPreexistingVisitorAuthorityClaims(identity, authentication))
            return;

        var roles = identity.FindAll(authentication.RoleClaimType).ToArray();
        if (roles.Length != 1 ||
            roles[0].Value != authentication.AskAlphaVisitorRole ||
            roles[0].Issuer != authentication.AskAlphaVisitorIssuer ||
            // Issuer alone is insufficient: a role may retain an original
            // foreign claim source after normalization/cookie alignment.
            // Fail closed rather than silently projecting it as a trusted
            // ClaimTypes.Role visitor grant. This does not activate enrollment.
            roles[0].OriginalIssuer != authentication.AskAlphaVisitorIssuer ||
            // A case-variant claim-type alias (ROLES/Role) is still a
            // second role source, even if its value is not in the configured
            // role-claim namespace. Deny mixed visitors before projection.
            // Do not trust claim issuer text as proof of token signature.
            identity.Claims.Any(claim =>
                !string.Equals(claim.Type, authentication.RoleClaimType, StringComparison.Ordinal)
                && (string.Equals(claim.Type, "role", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))))
            return;

        identity.AddClaim(new Claim(ClaimTypes.Role,
            authentication.AskAlphaVisitorRole, ClaimValueTypes.String,
            authentication.AskAlphaVisitorIssuer,
            authentication.AskAlphaVisitorIssuer));
    }

    // Fail-closed token prerequisite only; NOT an ownership or consent grant.
    // Treat issuer-supplied claim names case-insensitively, but never infer
    // authenticated tenant authority from their values or origin metadata.
    private static bool HasPreexistingVisitorAuthorityClaims(
        ClaimsIdentity identity, BlissAuthenticationOptions authentication) =>
        identity.Claims.Any(claim =>
            string.Equals(claim.Type, authentication.AdvertiserIdClaimType,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, authentication.ReviewerWorkspaceClaimType,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, authentication.AskAlphaVisitorSessionClaimType,
                StringComparison.OrdinalIgnoreCase));

    // Only the signed ID token is eligible to supply a dedicated visitor
    // role. The UserInfo endpoint must not supply a second role authority.
    // Reject the configured role claim AND common OIDC role aliases regardless
    // of JSON property casing. A duplicate signed role in UserInfo cannot
    // preserve a visitor projection; unrelated profile fields remain allowed.
    // Staff/advertiser role claims are never removed here.
    public static void InvalidateVisitorProjectionIfUserInfoContainsRoles(
        ClaimsIdentity identity, JsonElement user, BlissAuthenticationOptions authentication)
    {
        if (identity is null || authentication is null ||
            user.ValueKind != JsonValueKind.Object ||
            !user.EnumerateObject().Any(property =>
                string.Equals(property.Name, authentication.RoleClaimType,
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "role", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "roles", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)))
            return;

        foreach (var projected in identity.FindAll(ClaimTypes.Role)
            .Where(claim => claim.Value == authentication.AskAlphaVisitorRole &&
                claim.Issuer == authentication.AskAlphaVisitorIssuer &&
                claim.OriginalIssuer == authentication.AskAlphaVisitorIssuer)
            .ToArray())
            identity.RemoveClaim(projected);
    }

    // Run ONLY on the later UserInfo callback. UserInfo may carry display
    // profile fields but must not add operator/admin/reviewer role grants or
    // tenant workspace access to the authenticated cookie. Preserve roles
    // already derived from validated signed claims. A UserInfo roles field
    // additionally invalidates the dedicated Ask Alpha visitor projection.
    public static void IgnoreUntrustedUserInfoAuthorizationClaims(
        ClaimsIdentity identity, JsonElement user, BlissAuthenticationOptions authentication)
    {
        InvalidateVisitorProjectionIfUserInfoContainsRoles(identity, user, authentication);
        // Deliberately do NOT call Apply() or ApplyReviewerWorkspaceAssignments().
        // A future IdP-specific authorization policy requires independent
        // security approval before changing this default-denied boundary.
    }

    // Accept one workspace GUID per verified claim or an array supplied by the
    // identity provider. Never accept wildcards, empty/invalid GUIDs, or user
    // request values as reviewer assignments.
    public static void NormalizeReviewerWorkspaceAssignments(ClaimsIdentity identity, string claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType)) return;
        // Token-validated workspace claims must retain their actual issuer
        // provenance. Reissuing every scope as LOCAL AUTHORITY hides
        // cross-issuer collisions when reviewers audit cookie principals.
        // Preserving the origin is NOT a substitute for a future approved
        // issuer-aware staff/tenant authorization policy.
        var authorized = identity.FindAll(claimType)
            .SelectMany(claim => Expand(claim.Value).Select(value => (
                Value: value,
                Issuer: claim.Issuer,
                OriginalIssuer: claim.OriginalIssuer)))
            .Select(entry => (
                Workspace: Guid.TryParse(entry.Value, out var parsed) ? parsed : Guid.Empty,
                entry.Issuer,
                entry.OriginalIssuer))
            .Where(entry => entry.Workspace != Guid.Empty)
            .Distinct()
            .ToArray();
        foreach (var existing in identity.FindAll(claimType).ToList()) identity.RemoveClaim(existing);
        foreach (var entry in authorized)
            identity.AddClaim(new Claim(claimType, entry.Workspace.ToString("D"),
                ClaimValueTypes.String, entry.Issuer, entry.OriginalIssuer));
    }

    // Compatibility entry point only. UserInfo is not token-validated role
    // evidence, so it must never mint reviewer workspace grants. In particular,
    // a plain new Claim() would default to LOCAL AUTHORITY and conceal that
    // the input originated at an untrusted UserInfo endpoint. Keep the method
    // callable for existing clients while deliberately denying the mutation.
    [Obsolete("Untrusted UserInfo cannot issue reviewer workspace claims. Use token-validated NormalizeReviewerWorkspaceAssignments instead.")]
    public static void ApplyReviewerWorkspaceAssignments(ClaimsIdentity identity, JsonElement user, string claimType)
    {
        // Deliberately no-op: do not mutate the signed principal.
    }

    public static ClaimsIdentity Align(ClaimsIdentity identity, string nameClaimType, string roleClaimType)
    {
        var resolvedName = string.IsNullOrWhiteSpace(nameClaimType) ? identity.NameClaimType : nameClaimType.Trim();
        var resolvedRole = string.IsNullOrWhiteSpace(roleClaimType) ? identity.RoleClaimType : roleClaimType.Trim();
        // Cookie sign-in aligns different role-claim type aliases but must
        // NEVER re-issue their role values as LOCAL AUTHORITY or discard a
        // conflicting issuer. A role's claimed origin is part of the evidence,
        // not an authorization decision: provider approval is still required.
        var roles = identity.Claims
            .Where(claim => claim.Type == resolvedRole
                || claim.Type == ClaimTypes.Role
                || claim.Type == "role"
                || claim.Type == "roles")
            .SelectMany(claim => Expand(claim.Value)
                .Select(role => (
                    Value: role,
                    Issuer: claim.Issuer,
                    OriginalIssuer: claim.OriginalIssuer)))
            .Distinct()
            .ToArray();

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
            aligned.AddClaim(new Claim(resolvedRole, role.Value,
                ClaimValueTypes.String, role.Issuer, role.OriginalIssuer));
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


}
