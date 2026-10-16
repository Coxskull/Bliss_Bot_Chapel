using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: the cookie signer must not invent trusted provenance for
// claims that entered the principal through a different issuer or alias.
// These are isolated, fictional claims - not real OIDC hosted acceptance.
public sealed class AskAlphaCookieRoleIssuerProvenanceTests
{
    private const string Trusted="https://issuer.example.invalid/agm";
    private const string Foreign="https://other.example.invalid/agm";

    private static Claim Role(string type,string value,string issuer=Trusted,
        string? originalIssuer=null) =>
        new(type,value,ClaimValueTypes.String,issuer,originalIssuer ?? issuer);

    private static ClaimsIdentity Identity(params Claim[] claims) =>
        new(claims,"fictional-signed-callback","name","roles");

    [Fact]
    public void Signed_framework_role_keeps_issuer_through_cookie_alignment()
    {
        var source=Identity(
            Role(ClaimTypes.Role,"bliss.operator"),
            Role("name","Fictional Operator"));
        var aligned=OidcRoleClaimMapper.Align(source,"name","roles");
        var role=Assert.Single(aligned.FindAll("roles"));

        Assert.Equal("bliss.operator",role.Value);
        Assert.Equal(Trusted,role.Issuer);
        Assert.Equal(Trusted,role.OriginalIssuer);
        Assert.True(new ClaimsPrincipal(aligned).IsInRole("bliss.operator"));
        Assert.Equal("fictional-signed-callback",aligned.AuthenticationType);
    }

    [Fact]
    public void Foreign_role_must_not_be_reissued_with_local_or_trusted_issuer()
    {
        var source=Identity(Role("role","bliss.reviewer",Foreign));
        var aligned=OidcRoleClaimMapper.Align(source,"name","roles");
        var role=Assert.Single(aligned.FindAll("roles"));

        Assert.Equal(Foreign,role.Issuer);
        Assert.Equal(Foreign,role.OriginalIssuer);
        Assert.NotEqual(Trusted,role.Issuer);
        Assert.NotEqual("LOCAL AUTHORITY",role.Issuer);
    }

    [Fact]
    public void Identical_role_values_from_two_issuers_do_not_collapse()
    {
        var source=Identity(
            Role("roles","bliss.viewer",Trusted),
            Role("role","bliss.viewer",Foreign));
        var aligned=OidcRoleClaimMapper.Align(source,"name","roles");

        var mapped=aligned.FindAll("roles").ToArray();
        Assert.Equal(2,mapped.Length);
        Assert.Contains(mapped,claim => claim.Issuer == Trusted);
        Assert.Contains(mapped,claim => claim.Issuer == Foreign);
        // Retaining issuer does NOT make staff authorization issuer-aware.
        // An approved provider-role policy is still needed before acceptance.
    }

    [Fact]
    public void Json_array_from_one_issuer_expands_without_losing_provenance()
    {
        var source=Identity(Role("roles","""["bliss.viewer","bliss.reviewer"]""",
            Trusted,"https://original.example.invalid"));
        var aligned=OidcRoleClaimMapper.Align(source,"name","roles");
        var roles=aligned.FindAll("roles").ToArray();

        Assert.Equal(2,roles.Length);
        Assert.All(roles,role => Assert.Equal(Trusted,role.Issuer));
        Assert.All(roles,role => Assert.Equal("https://original.example.invalid",role.OriginalIssuer));
        Assert.Contains(roles,claim => claim.Value == "bliss.viewer");
        Assert.Contains(roles,claim => claim.Value == "bliss.reviewer");
    }

    [Fact]
    public void Duplicate_role_same_origin_is_normalized_once_without_issuer_loss()
    {
        var source=Identity(
            Role("roles","bliss.operator"),
            Role("role","bliss.operator"));
        var aligned=OidcRoleClaimMapper.Align(source,"name","roles");
        var role=Assert.Single(aligned.FindAll("roles"));

        Assert.Equal("bliss.operator",role.Value);
        Assert.Equal(Trusted,role.Issuer);
        Assert.Equal(Trusted,role.OriginalIssuer);
    }

    [Fact]
    public void Framework_visitor_role_not_reissued_as_local_authority()
    {
        var source=Identity(
            Role(ClaimTypes.Role,"bliss.ask_alpha_visitor"),
            Role("sub","fictional-visitor-id"));
        var aligned=OidcRoleClaimMapper.Align(source,"name","roles");
        var projected=Assert.Single(aligned.FindAll(ClaimTypes.Role));
        var mapped=Assert.Single(aligned.FindAll("roles"));

        Assert.Equal(Trusted,projected.Issuer);
        Assert.Equal(Trusted,mapped.Issuer);
        Assert.Equal(Trusted,mapped.OriginalIssuer);
    }
}
