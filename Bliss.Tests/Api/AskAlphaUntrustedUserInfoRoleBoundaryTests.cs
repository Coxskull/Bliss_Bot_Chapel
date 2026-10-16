using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015. Fictional UserInfo cannot be used as authority for role grants.
// Real IdP signature, hosted consent and production authorization are NOT tested.
public sealed class AskAlphaUntrustedUserInfoRoleBoundaryTests
{
    private const string Roles = "roles";
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Foreign = "https://foreign.example.invalid/agm";

    private static ClaimsIdentity Principal(params Claim[] claims) =>
        new(claims, "fictional-signed-oidc", "name", Roles);

    private static Claim Role(string role, string issuer = Trusted,
        string? originalIssuer = null) =>
        new(Roles, role, ClaimValueTypes.String, issuer, originalIssuer ?? issuer);

    // Test only: exercising the legacy compatibility API is intentional.
#pragma warning disable CS0618
    [Theory]
    [InlineData("""{"roles":["bliss.admin","bliss.operator"]}""")]
    [InlineData("""{"roles":"bliss.reviewer"}""")]
    [InlineData("""{"roles":"bliss.advertiser,bliss.admin"}""")]
    public void UserInfo_roles_never_create_a_staff_or_advertiser_grant(string json)
    {
        var identity = Principal();
        using var profile = JsonDocument.Parse(json);

        OidcRoleClaimMapper.Apply(identity, profile.RootElement, Roles);

        Assert.Empty(identity.FindAll(Roles));
        Assert.DoesNotContain(identity.Claims, claim => claim.Issuer == "LOCAL AUTHORITY");
    }

    [Fact]
    public void UserInfo_does_not_append_an_admin_role_to_signed_reviewer()
    {
        var identity = Principal(Role("bliss.reviewer"));
        using var profile = JsonDocument.Parse("""{"roles":["bliss.admin"]}""");

        OidcRoleClaimMapper.Apply(identity, profile.RootElement, Roles);

        var role = Assert.Single(identity.FindAll(Roles));
        Assert.Equal("bliss.reviewer", role.Value);
        Assert.Equal(Trusted, role.Issuer);
        Assert.Equal(Trusted, role.OriginalIssuer);
    }

    [Fact]
    public void UserInfo_does_not_rewrite_a_foreign_issuer_or_original_issuer()
    {
        var identity = Principal(Role("bliss.operator", Foreign, Trusted));
        using var profile = JsonDocument.Parse("""{"roles":"bliss.operator"}""");

        OidcRoleClaimMapper.Apply(identity, profile.RootElement, Roles);

        var role = Assert.Single(identity.FindAll(Roles));
        Assert.Equal(Foreign, role.Issuer);
        Assert.Equal(Trusted, role.OriginalIssuer);
    }

    [Fact]
    public void UserInfo_cannot_create_a_visitor_role_when_feature_is_disabled()
    {
        var identity = Principal();
        using var profile = JsonDocument.Parse(
            """{"roles":["bliss.ask_alpha_visitor"]}""");

        OidcRoleClaimMapper.Apply(identity, profile.RootElement, Roles);

        Assert.Empty(identity.Claims);
    }

    [Fact]
    public void Multiple_UserInfo_calls_remain_inert()
    {
        var identity = Principal(Role("bliss.reviewer"));
        using var profile = JsonDocument.Parse("""{"roles":["bliss.admin"]}""");

        OidcRoleClaimMapper.Apply(identity, profile.RootElement, Roles);
        OidcRoleClaimMapper.Apply(identity, profile.RootElement, Roles);

        Assert.Single(identity.FindAll(Roles));
        Assert.DoesNotContain(identity.Claims, claim => claim.Issuer == "LOCAL AUTHORITY");
    }
#pragma warning restore CS0618

    [Fact]
    public void Signed_token_role_normalization_remains_available()
    {
        var identity = Principal(Role("""["bliss.reviewer","bliss.operator"]"""));

        OidcRoleClaimMapper.Normalize(identity, Roles);

        var roles = identity.FindAll(Roles).ToArray();
        Assert.Equal(2, roles.Length);
        Assert.All(roles, role => Assert.Equal(Trusted, role.Issuer));
        Assert.All(roles, role => Assert.Equal(Trusted, role.OriginalIssuer));
        Assert.Contains(roles, role => role.Value == "bliss.reviewer");
        Assert.Contains(roles, role => role.Value == "bliss.operator");
    }
}
