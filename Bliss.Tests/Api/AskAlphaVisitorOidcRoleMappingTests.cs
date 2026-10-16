using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// No hosted identity provider and no real visitor or consent writes.
// These are fail-closed post-signature-callback role mapping regressions.
public sealed class AskAlphaVisitorOidcRoleMappingTests
{
    private const string Issuer="https://idp.example.invalid/realms/agm";
    private const string Role="bliss.ask_alpha_visitor";

    private static BlissAuthenticationOptions Configuration() => new()
    {
        Enabled=true,
        Authority=Issuer,
        ClientId="fixture-visitor-app",
        RoleClaimType="roles",
        AskAlphaVisitorIssuer=Issuer,
        AskAlphaVisitorRole=Role,
        AskAlphaVisitorEnrollmentRequested=true
    };

    private static ClaimsIdentity SignedIdentity(params Claim[] roleClaims)
    {
        var identity=new ClaimsIdentity("fixture-validated-id-token","name","roles");
        identity.AddClaim(new Claim("sub","fixture-visitor-subject-001",
            ClaimValueTypes.String,Issuer));
        foreach(var role in roleClaims) identity.AddClaim(role);
        return identity;
    }

    private static Claim SignedRole(string role=Role,string issuer=Issuer) =>
        new("roles",role,ClaimValueTypes.String,issuer,issuer);

    [Fact]
    public void Validated_single_visitor_role_keeps_issuer_and_matches_only_prerequisites()
    {
        var id=SignedIdentity(SignedRole("[\"bliss.ask_alpha_visitor\"]"));
        OidcRoleClaimMapper.Normalize(id,"roles");
        var normalized=Assert.Single(id.FindAll("roles"));
        Assert.Equal(Issuer,normalized.Issuer);
        Assert.Equal(Role,normalized.Value);

        OidcRoleClaimMapper.ProjectValidatedVisitorRole(id,Configuration());
        var projected=Assert.Single(id.FindAll(ClaimTypes.Role));
        Assert.Equal(Role,projected.Value);
        Assert.Equal(Issuer,projected.Issuer);

        var readiness=AskAlphaVisitorEnrollmentGate.Inspect(
            new ClaimsPrincipal(id),Configuration());
        Assert.True(readiness.IdpConfigurationDeclared);
        Assert.True(readiness.IssuerSubjectClaimsConsistent);
        Assert.False(readiness.RealVisitorIdentityAccepted);
        Assert.False(readiness.NewVisitorSessionCreated);
        Assert.False(readiness.VisitorConsentRecorded);
        Assert.False(readiness.TranscriptExportAuthorized);
        Assert.False(readiness.PaymentAuthorized);
        Assert.False(readiness.CreatorPayoutAuthorized);
        Assert.False(readiness.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",readiness.Delivery);
    }

    [Theory]
    [InlineData("https://untrusted.example.invalid")]
    [InlineData("LOCAL AUTHORITY")]
    public void Untrusted_source_issuer_cannot_be_reissued_as_a_trusted_visitor(string issuer)
    {
        var id=SignedIdentity(SignedRole(issuer:issuer));
        OidcRoleClaimMapper.Normalize(id,"roles");
        Assert.Equal(issuer,Assert.Single(id.FindAll("roles")).Issuer);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(id,Configuration());
        Assert.Empty(id.FindAll(ClaimTypes.Role));
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(
            new ClaimsPrincipal(id),Configuration()).IssuerSubjectClaimsConsistent);
    }

    [Fact]
    public void Userinfo_supplied_role_without_validated_issuer_never_projects()
    {
        var id=SignedIdentity();
        using var info=JsonDocument.Parse("{\"roles\":[\"bliss.ask_alpha_visitor\"]}");
        OidcRoleClaimMapper.Apply(id,info.RootElement,"roles");
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(id,Configuration());
        Assert.Empty(id.FindAll(ClaimTypes.Role));
    }

    [Theory]
    [InlineData("bliss.operator")]
    [InlineData("bliss.viewer")]
    [InlineData("brand.unregistered_staff_role")]
    public void Mixed_known_or_unknown_roles_cannot_be_projected_as_a_dedicated_visitor(
        string extra)
    {
        var id=SignedIdentity(SignedRole(),SignedRole(extra));
        OidcRoleClaimMapper.Normalize(id,"roles");
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(id,Configuration());
        Assert.Empty(id.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Roles_with_identical_values_from_distinct_issuers_remain_distinct()
    {
        var id=SignedIdentity(SignedRole(),SignedRole(issuer:"https://foreign.example.invalid"));
        OidcRoleClaimMapper.Normalize(id,"roles");
        Assert.Equal(2,id.FindAll("roles").Count());
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(id,Configuration());
        Assert.Empty(id.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Preexisting_staff_role_or_conflicted_config_never_gets_a_visitor_projection()
    {
        var id=SignedIdentity(SignedRole());
        id.AddClaim(new Claim(ClaimTypes.Role,"bliss.admin",
            ClaimValueTypes.String,Issuer));
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(id,Configuration());
        Assert.Single(id.FindAll(ClaimTypes.Role));
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(
            new ClaimsPrincipal(id),Configuration()).IssuerSubjectClaimsConsistent);

        var other=SignedIdentity(SignedRole());
        var conflict=Configuration();
        conflict.AdvertiserRole=Role;
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(other,conflict);
        Assert.Empty(other.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Unauthenticated_identity_never_receives_projected_visitor_role()
    {
        var id=new ClaimsIdentity(new[]
        {
            SignedRole(),
            new Claim("sub","fixture-visitor-subject-001",
                ClaimValueTypes.String,Issuer)
        });
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(id,Configuration());
        Assert.Empty(id.FindAll(ClaimTypes.Role));
    }
}
