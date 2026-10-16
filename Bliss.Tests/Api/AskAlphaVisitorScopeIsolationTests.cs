using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: fictional OIDC callback fixtures. These do not authenticate
// tokens, create visitor sessions, capture consent or change hosted settings.
public sealed class AskAlphaVisitorScopeIsolationTests
{
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Foreign = "https://foreign.example.invalid/agm";
    private const string VisitorRole = "bliss.ask_alpha_visitor";
    private const string VisitorClaim = "roles";

    private static BlissAuthenticationOptions Config() => new()
    {
        Enabled = true,
        Authority = Trusted,
        ClientId = "fictional-public-client",
        RoleClaimType = VisitorClaim,
        AskAlphaVisitorIssuer = Trusted,
        AskAlphaVisitorRole = VisitorRole,
        AskAlphaVisitorEnrollmentRequested = true
    };

    private static ClaimsIdentity Identity() =>
        new(new[]
        {
            new Claim(VisitorClaim, VisitorRole, ClaimValueTypes.String, Trusted, Trusted),
            new Claim("name", "Fictional Visitor", ClaimValueTypes.String, Trusted, Trusted)
        }, "fictional-validated-oidc", "name", VisitorClaim);

    [Theory]
    [InlineData("advertiser_id", "33333333-3333-3333-3333-333333333333")]
    [InlineData("ADVERTISER_ID", "")]
    [InlineData("bliss_review_workspace_id", "11111111-1111-1111-1111-111111111111")]
    [InlineData("BLISS_REVIEW_WORKSPACE_ID", "not-a-uuid")]
    [InlineData("ask_alpha_session_id", "22222222-2222-2222-2222-222222222222")]
    [InlineData("ASK_ALPHA_SESSION_ID", "")]
    public void Existing_tenant_or_session_claim_denies_visitor_projection(
        string claimType, string claimValue)
    {
        var identity = Identity();
        identity.AddClaim(new Claim(claimType, claimValue,
            ClaimValueTypes.String, Foreign, Foreign));
        var options = Config();

        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        Assert.Contains(identity.Claims, claim =>
            claim.Type == claimType && claim.Value == claimValue &&
            claim.Issuer == Foreign && claim.OriginalIssuer == Foreign);
    }

    [Fact]
    public void Even_trusted_issuer_workspace_claim_denies_visitor_projection()
    {
        var identity = Identity();
        identity.AddClaim(new Claim("bliss_review_workspace_id",
            "11111111-1111-1111-1111-111111111111",
            ClaimValueTypes.String, Trusted, Trusted));

        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, Config());

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }

    [Theory]
    [InlineData("AdvertiserIdClaimType")]
    [InlineData("ReviewerWorkspaceClaimType")]
    [InlineData("AskAlphaVisitorSessionClaimType")]
    public void Missing_privilege_claim_configuration_fails_closed(string property)
    {
        var options = Config();
        switch (property)
        {
            case "AdvertiserIdClaimType": options.AdvertiserIdClaimType = " "; break;
            case "ReviewerWorkspaceClaimType": options.ReviewerWorkspaceClaimType = " "; break;
            case "AskAlphaVisitorSessionClaimType": options.AskAlphaVisitorSessionClaimType = " "; break;
        }
        var identity = Identity();

        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Canonical_visitor_with_ordinary_profile_fields_still_projects()
    {
        var identity = Identity();
        identity.AddClaim(new Claim("email", "visitor@example.invalid",
            ClaimValueTypes.String, Trusted, Trusted));

        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, Config());

        var projected = Assert.Single(identity.FindAll(ClaimTypes.Role));
        Assert.Equal(VisitorRole, projected.Value);
        Assert.Equal(Trusted, projected.Issuer);
        Assert.Equal(Trusted, projected.OriginalIssuer);
    }

    [Fact]
    public void Disabled_enrollment_never_uses_signed_visitor_role()
    {
        var options = Config();
        options.AskAlphaVisitorEnrollmentRequested = false;
        var identity = Identity();

        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public void Untrusted_staff_role_does_not_become_a_valid_visitor_principal()
    {
        var options = Config();
        var identity = Identity();
        identity.AddClaim(new Claim("roles", "bliss.admin",
            ClaimValueTypes.String, Foreign, Foreign));

        OidcRoleClaimMapper.Normalize(identity, options.RoleClaimType);
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity, options);

        Assert.Empty(identity.FindAll(ClaimTypes.Role));
    }
}
