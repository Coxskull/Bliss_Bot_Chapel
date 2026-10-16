using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: fictional negative/positive fixtures for signed visitor-role
// projection. Test claims represent an already OIDC-validated callback;
// their issuer strings do NOT prove a cryptographically validated token.
// No real visitor enrollment, consent or host approval is implied.
public sealed class AskAlphaVisitorOriginalIssuerProjectionTests
{
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Foreign = "https://foreign.example.invalid/agm";
    private const string VisitorRole = "bliss.ask_alpha_visitor";

    private static BlissAuthenticationOptions ApprovedFixture() => new()
    {
        Enabled = true,
        Authority = Trusted,
        ClientId = "fictional-test-client",
        RoleClaimType = "roles",
        AskAlphaVisitorIssuer = Trusted,
        AskAlphaVisitorRole = VisitorRole,
        AskAlphaVisitorEnrollmentRequested = true
    };

    private static ClaimsIdentity Identity(string issuer = Trusted,
        string? originalIssuer = null) =>
        new(new[]
        {
            new Claim("roles", VisitorRole, ClaimValueTypes.String,
                issuer, originalIssuer ?? issuer)
        }, "fictional-validated-oidc", "name", "roles");

    [Fact]
    public void Approved_original_issuer_projects_one_trusted_visitor_role()
    {
        var options = ApprovedFixture();
        var identity = Identity();
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        var projected = Assert.Single(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(VisitorRole, projected.Value);
        Assert.Equal(Trusted, projected.Issuer);
        Assert.Equal(Trusted, projected.OriginalIssuer);
    }

    [Fact]
    public void Foreign_original_issuer_with_trusted_issuer_must_never_project()
    {
        var options = ApprovedFixture();
        var identity = Identity(Trusted, Foreign);
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        var signed = Assert.Single(identity.FindAll(options.RoleClaimType));
        Assert.Equal(Trusted, signed.Issuer);
        Assert.Equal(Foreign, signed.OriginalIssuer);
    }

    [Fact]
    public void Foreign_current_issuer_with_trusted_original_must_never_project()
    {
        var options = ApprovedFixture();
        var identity = Identity(Foreign, Trusted);
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Cookie_alignment_preserves_a_foreign_original_issuer_without_elevation()
    {
        var options = ApprovedFixture();
        var identity = Identity(Trusted, Foreign);
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);
        var cookie = OidcRoleClaimMapper.Align(identity, "name", options.RoleClaimType);

        var role = Assert.Single(cookie.FindAll(options.RoleClaimType));
        Assert.Equal(VisitorRole, role.Value);
        Assert.Equal(Foreign, role.OriginalIssuer);
        Assert.Empty(cookie.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Valid_visitor_projection_cookie_alignment_preserves_trusted_origin()
    {
        var options = ApprovedFixture();
        var identity = Identity();
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);
        var cookie = OidcRoleClaimMapper.Align(identity, "name", options.RoleClaimType);

        var role = Assert.Single(cookie.FindAll(options.RoleClaimType));
        Assert.Equal(VisitorRole, role.Value);
        Assert.Equal(Trusted, role.Issuer);
        Assert.Equal(Trusted, role.OriginalIssuer);
    }

    [Fact]
    public void UserInfo_role_property_revokes_projection_even_when_value_matches()
    {
        var options = ApprovedFixture();
        var identity = Identity();
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);
        Assert.Single(identity.FindAll(ClaimTypes.Role));
        using var userInfo = JsonDocument.Parse(
            """{"roles":["bliss.ask_alpha_visitor"]}""");

        OidcRoleClaimMapper.IgnoreUntrustedUserInfoAuthorizationClaims(
            identity, userInfo.RootElement, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(Trusted, Assert.Single(identity.FindAll("roles")).OriginalIssuer);
    }

    [Fact]
    public void Disabled_feature_does_not_project_even_with_matching_origins()
    {
        var options = ApprovedFixture();
        options.AskAlphaVisitorEnrollmentRequested = false;
        var identity = Identity();
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);
        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Staff_role_mixed_with_visitor_is_not_projected()
    {
        var options = ApprovedFixture();
        var identity = Identity();
        identity.AddClaim(new Claim("roles", "bliss.admin",
            ClaimValueTypes.String, Trusted, Trusted));
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }
}
