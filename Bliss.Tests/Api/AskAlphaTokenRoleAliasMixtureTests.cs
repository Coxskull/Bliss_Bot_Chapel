using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: fictional, token-callback-side role isolation only.
// Does not validate a signed token, enable real registration or grant consent.
public sealed class AskAlphaTokenRoleAliasMixtureTests
{
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Foreign = "https://foreign.example.invalid/agm";
    private const string Visitor = "bliss.ask_alpha_visitor";

    private static BlissAuthenticationOptions Config(string roleClaimType = "roles") => new()
    {
        Enabled = true,
        Authority = Trusted,
        ClientId = "fictional-test-client",
        AskAlphaVisitorIssuer = Trusted,
        AskAlphaVisitorRole = Visitor,
        AskAlphaVisitorEnrollmentRequested = true,
        RoleClaimType = roleClaimType
    };

    private static ClaimsIdentity Token(BlissAuthenticationOptions options) =>
        new(new[]
        {
            new Claim(options.RoleClaimType, Visitor, ClaimValueTypes.String, Trusted, Trusted)
        }, "fictional-validated-token", "name", options.RoleClaimType);

    private static void Project(ClaimsIdentity identity, BlissAuthenticationOptions options)
    {
        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);
    }

    [Theory]
    [InlineData("ROLES")]
    [InlineData("Roles")]
    [InlineData("ROLE")]
    [InlineData("Role")]
    [InlineData("role")]
    public void Alternate_role_alias_denies_visitor_projection_without_mutating_existing_claim(
        string alias)
    {
        var options = Config();
        var identity = Token(options);
        var extra = new Claim(alias, "bliss.admin", ClaimValueTypes.String, Foreign, Foreign);
        identity.AddClaim(extra);

        Project(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        var untouched = Assert.Single(identity.FindAll(alias));
        Assert.Equal("bliss.admin", untouched.Value);
        Assert.Equal(Foreign, untouched.OriginalIssuer);
        Assert.Equal(Trusted, Assert.Single(identity.FindAll(options.RoleClaimType)).Issuer);
    }

    [Fact]
    public void Framework_role_claim_does_not_allow_visitor_role_projection()
    {
        var options = Config();
        var identity = Token(options);
        identity.AddClaim(new Claim(ClaimTypes.Role, "bliss.reviewer",
            ClaimValueTypes.String, Foreign, Foreign));

        Project(identity, options);

        var role = Assert.Single(identity.FindAll(ClaimTypes.Role));
        Assert.Equal("bliss.reviewer", role.Value);
        Assert.Equal(Foreign, role.Issuer);
        Assert.DoesNotContain(identity.Claims, claim =>
            claim.Type == ClaimTypes.Role && claim.Value == Visitor);
    }

    [Fact]
    public void Canonical_token_only_visitor_still_projects_with_origin_preserved()
    {
        var options = Config();
        var identity = Token(options);

        Project(identity, options);

        var projection = Assert.Single(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(Visitor, projection.Value);
        Assert.Equal(Trusted, projection.Issuer);
        Assert.Equal(Trusted, projection.OriginalIssuer);
    }

    [Fact]
    public void Custom_exact_role_claim_type_still_projects_valid_visitor()
    {
        var options = Config("member_permissions");
        var identity = Token(options);

        Project(identity, options);

        Assert.Equal(Visitor, Assert.Single(identity.FindAll(ClaimTypes.Role)).Value);
    }

    [Fact]
    public void Custom_role_claim_type_cannot_ignore_mixed_standard_alias()
    {
        var options = Config("member_permissions");
        var identity = Token(options);
        identity.AddClaim(new Claim("ROLES", "bliss.admin",
            ClaimValueTypes.String, Trusted, Trusted));

        Project(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        Assert.Equal("bliss.admin", Assert.Single(identity.FindAll("ROLES")).Value);
    }

    [Fact]
    public void Exact_case_sensitive_custom_alias_can_remain_the_configured_source()
    {
        var options = Config("ROLES");
        var identity = Token(options);

        Project(identity, options);

        var visitor = Assert.Single(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(Visitor, visitor.Value);
        Assert.Equal(Trusted, visitor.OriginalIssuer);
    }

    [Fact]
    public void Matching_type_but_second_admin_value_also_denies_projection()
    {
        var options = Config();
        var identity = Token(options);
        identity.AddClaim(new Claim("roles", "bliss.admin",
            ClaimValueTypes.String, Trusted, Trusted));

        Project(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(2, identity.FindAll("roles").Count());
    }

    [Fact]
    public void Feature_off_never_projects_even_without_mixed_roles()
    {
        var options = Config();
        options.AskAlphaVisitorEnrollmentRequested = false;
        var identity = Token(options);

        Project(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }
}
