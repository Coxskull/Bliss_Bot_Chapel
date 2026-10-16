using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: fictional callback behavior, not hosted OIDC or real consent.
// The signed visitor prerequisite is never a session/consent authorization.
public sealed class AskAlphaUserInfoRoleAliasRevocationTests
{
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Visitor = "bliss.ask_alpha_visitor";

    private static BlissAuthenticationOptions Config(string claimType = "roles") => new()
    {
        Enabled = true,
        Authority = Trusted,
        ClientId = "fictional-client",
        RoleClaimType = claimType,
        AskAlphaVisitorIssuer = Trusted,
        AskAlphaVisitorRole = Visitor,
        AskAlphaVisitorEnrollmentRequested = true
    };

    private static ClaimsIdentity Project(BlissAuthenticationOptions options)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(options.RoleClaimType, Visitor, ClaimValueTypes.String,
                Trusted, Trusted)
        }, "fictional-validated-token", "name", options.RoleClaimType);
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);
        Assert.Single(identity.FindAll(ClaimTypes.Role));
        return identity;
    }

    [Theory]
    [InlineData("""{"role":"bliss.ask_alpha_visitor"}""")]
    [InlineData("""{"ROLES":["bliss.ask_alpha_visitor"]}""")]
    [InlineData("""{"Role":null}""")]
    [InlineData("""{"roles":[]}""")]
    public void Any_common_UserInfo_role_alias_revokes_visitor_projection(string json)
    {
        var options = Config();
        var identity = Project(options);
        using var userInfo = JsonDocument.Parse(json);

        OidcRoleClaimMapper.IgnoreUntrustedUserInfoAuthorizationClaims(
            identity, userInfo.RootElement, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(Trusted, Assert.Single(identity.FindAll(options.RoleClaimType)).OriginalIssuer);
    }

    [Fact]
    public void Framework_role_claim_alias_is_also_denied()
    {
        var options = Config();
        var identity = Project(options);
        using var profile = JsonDocument.Parse(
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [ClaimTypes.Role] = new[] { Visitor }
            }));

        OidcRoleClaimMapper.IgnoreUntrustedUserInfoAuthorizationClaims(
            identity, profile.RootElement, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Configured_nonstandard_role_name_is_case_insensitively_denied()
    {
        var options = Config("member_permissions");
        var identity = Project(options);
        using var profile = JsonDocument.Parse(
            """{"MEMBER_PERMISSIONS":["bliss.ask_alpha_visitor"]}""");

        OidcRoleClaimMapper.IgnoreUntrustedUserInfoAuthorizationClaims(
            identity, profile.RootElement, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(Trusted,
            Assert.Single(identity.FindAll("member_permissions")).Issuer);
    }

    [Fact]
    public void Profile_only_UserInfo_preserves_token_projected_visitor()
    {
        var options = Config();
        var identity = Project(options);
        using var profile = JsonDocument.Parse(
            """{"name":"Fictional Visitor","bliss_review_workspace_id":"not-an-authorization-grant"}""");

        OidcRoleClaimMapper.IgnoreUntrustedUserInfoAuthorizationClaims(
            identity, profile.RootElement, options);

        var claim = Assert.Single(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(Visitor, claim.Value);
        Assert.Equal(Trusted, claim.Issuer);
        Assert.DoesNotContain(identity.Claims, c => c.Type == options.ReviewerWorkspaceClaimType);
    }

    [Fact]
    public void UserInfo_cannot_remove_an_unrelated_role_with_different_origin()
    {
        var options = Config();
        var identity = Project(options);
        identity.AddClaim(new Claim(ClaimTypes.Role, "bliss.reviewer",
            ClaimValueTypes.String, "https://foreign.example.invalid/agm"));
        using var profile = JsonDocument.Parse("""{"role":"bliss.ask_alpha_visitor"}""");

        OidcRoleClaimMapper.IgnoreUntrustedUserInfoAuthorizationClaims(
            identity, profile.RootElement, options);

        var retained = Assert.Single(identity.FindAll(ClaimTypes.Role));
        Assert.Equal("bliss.reviewer", retained.Value);
        Assert.Equal("https://foreign.example.invalid/agm", retained.Issuer);
    }
}
