using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

public sealed class AskAlphaVisitorClaimGateTests
{
    private const string Issuer = "https://issuer.fixture.invalid";
    private static BlissAuthenticationOptions Options(bool enabled = true) => new()
    {
        Enabled = enabled,
        AskAlphaVisitorIssuer = Issuer
    };
    private static ClaimsPrincipal Principal(Guid session, string issuer = Issuer,
        string role = "bliss.ask_alpha_visitor", bool authenticated = true,
        string subject = "fixture-visitor-001")
    {
        var id = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, role, ClaimValueTypes.String, issuer),
            new Claim("sub", subject, ClaimValueTypes.String, issuer),
            new Claim("ask_alpha_session_id", session.ToString("D"), ClaimValueTypes.String, issuer)
        }, authenticated ? "fixture-oidc" : null);
        return new ClaimsPrincipal(id);
    }
    [Fact]
    public void Exact_matching_issuer_and_claim_binding_remain_unverified_and_non_authorizing()
    {
        var session = Guid.NewGuid();
        var row = AskAlphaVisitorClaimGate.Inspect(Principal(session), Options(), session);
        Assert.Equal("OIDC_CLAIMS_PRESENT_HISTORY_UNVERIFIED", row.Status);
        Assert.True(row.ClaimsMatchIssuerAndSession);
        Assert.False(row.HistoricalSessionOwnershipVerified);
        Assert.False(row.VisitorConsentRecorded);
        Assert.False(row.TranscriptLinked);
        Assert.False(row.TranscriptExportAuthorized);
        Assert.False(row.PaymentAuthorized);
        Assert.False(row.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT", row.Delivery);
    }

    [Fact]
    public void Disabled_auth_missing_issuer_or_missing_session_scope_fails_closed()
    {
        var session = Guid.NewGuid();
        foreach (var config in new[] {
            Options(false),
            new BlissAuthenticationOptions { Enabled = true, AskAlphaVisitorIssuer = string.Empty }
        })
        {
            var row = AskAlphaVisitorClaimGate.Inspect(Principal(session), config, session);
            Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING", row.Status);
            Assert.False(row.ClaimsMatchIssuerAndSession);
        }
        Assert.False(AskAlphaVisitorClaimGate.Inspect(Principal(session), Options(), Guid.NewGuid()).ClaimsMatchIssuerAndSession);
        Assert.False(AskAlphaVisitorClaimGate.Inspect(Principal(session), Options(), Guid.Empty).ClaimsMatchIssuerAndSession);
        Assert.False(AskAlphaVisitorClaimGate.Inspect(Principal(session, authenticated:false), Options(), session).ClaimsMatchIssuerAndSession);
        Assert.False(AskAlphaVisitorClaimGate.Inspect(null, Options(), session).ClaimsMatchIssuerAndSession);
    }

    [Theory]
    [InlineData("bliss.advertiser")]
    [InlineData("bliss.operator")]
    [InlineData("bliss.admin")]
    [InlineData("bliss.reviewer")]
    [InlineData("bliss.viewer")]
    public void Advertiser_staff_reviewer_viewer_roles_cannot_satisfy_visitor_gate(string role)
    {
        var session = Guid.NewGuid();
        var result = AskAlphaVisitorClaimGate.Inspect(Principal(session,role:role), Options(), session);
        Assert.False(result.ClaimsMatchIssuerAndSession);
    }

    [Fact]
    public void Foreign_issuer_visitor_role_must_not_pair_with_trusted_subject_and_session()
    {
        var session=Guid.NewGuid();
        var principal=Principal(session);
        var identity=(ClaimsIdentity)principal.Identity!;
        var trusted=Assert.Single(identity.FindAll(ClaimTypes.Role));
        identity.RemoveClaim(trusted);
        identity.AddClaim(new Claim(ClaimTypes.Role,"bliss.ask_alpha_visitor",
            ClaimValueTypes.String,"https://foreign-idp.example.invalid"));
        Assert.False(AskAlphaVisitorClaimGate.Inspect(principal,Options(),session)
            .ClaimsMatchIssuerAndSession);
    }

    [Theory]
    [InlineData("bliss.viewer")]
    [InlineData("bliss.operator")]
    [InlineData("brand.new_staff_role")]
    public void A_second_staff_or_unknown_role_denies_claim_readiness(string extraRole)
    {
        var session=Guid.NewGuid();
        var principal=Principal(session);
        ((ClaimsIdentity)principal.Identity!).AddClaim(
            new Claim(ClaimTypes.Role,extraRole,ClaimValueTypes.String,Issuer));
        var result=AskAlphaVisitorClaimGate.Inspect(principal,Options(),session);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",result.Status);
        Assert.False(result.ClaimsMatchIssuerAndSession);
        Assert.False(result.HistoricalSessionOwnershipVerified);
        Assert.False(result.TranscriptExportAuthorized);
        Assert.Equal("NOT_SENT",result.Delivery);
    }

    [Fact]
    public void Multiple_identities_must_not_be_stitched_into_a_visitors_session_binding()
    {
        var session=Guid.NewGuid();
        var valid=Principal(session);
        valid.AddIdentity(new ClaimsIdentity(new[]
        {
            new Claim("source","unsigned-identity")
        }));
        Assert.False(AskAlphaVisitorClaimGate.Inspect(valid,Options(),session)
            .ClaimsMatchIssuerAndSession);

        var authenticatedRole=new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role,"bliss.ask_alpha_visitor",
                ClaimValueTypes.String,Issuer)
        },"signed-test-cookie");
        var unauthenticatedSubjectAndSession=new ClaimsIdentity(new[]
        {
            new Claim("sub","fixture-visitor-001",ClaimValueTypes.String,Issuer),
            new Claim("ask_alpha_session_id",session.ToString("D"),
                ClaimValueTypes.String,Issuer)
        });
        var assembled=new ClaimsPrincipal(new[]{
            authenticatedRole,unauthenticatedSubjectAndSession
        });
        Assert.False(AskAlphaVisitorClaimGate.Inspect(assembled,Options(),session)
            .ClaimsMatchIssuerAndSession);

        var secondAuthenticated=Principal(session);
        secondAuthenticated.AddIdentity(new ClaimsIdentity(new[]
        {
            new Claim("source","other-authenticated-person")
        },"another-test-cookie"));
        Assert.False(AskAlphaVisitorClaimGate.Inspect(secondAuthenticated,Options(),session)
            .ClaimsMatchIssuerAndSession);
    }

    [Fact]
    public void Conflicting_role_configuration_is_denied_even_with_matching_claims()
    {
        var session=Guid.NewGuid();
        var options=Options();
        options.AdvertiserRole=options.AskAlphaVisitorRole;
        var result=AskAlphaVisitorClaimGate.Inspect(Principal(session),options,session);
        Assert.False(result.ClaimsMatchIssuerAndSession);
        Assert.False(result.VisitorConsentRecorded);
        Assert.False(result.CampaignDeliveryAuthorized);
    }

    [Fact]
    public void Forged_issuer_duplicate_claims_and_invalid_sub_are_rejected()
    {
        var session = Guid.NewGuid();
        Assert.False(AskAlphaVisitorClaimGate.Inspect(Principal(session, issuer:"local-untrusted"),
            Options(), session).ClaimsMatchIssuerAndSession);
        Assert.False(AskAlphaVisitorClaimGate.Inspect(Principal(session, subject:"not a valid subject"),
            Options(), session).ClaimsMatchIssuerAndSession);

        var principal = Principal(session);
        ((ClaimsIdentity)principal.Identity!).AddClaim(
            new Claim("ask_alpha_session_id", session.ToString("D"), ClaimValueTypes.String, Issuer));
        Assert.False(AskAlphaVisitorClaimGate.Inspect(principal, Options(), session).ClaimsMatchIssuerAndSession);
        var duplicateSubject = Principal(session);
        ((ClaimsIdentity)duplicateSubject.Identity!).AddClaim(
            new Claim("sub", "fixture-visitor-002", ClaimValueTypes.String, Issuer));
        Assert.False(AskAlphaVisitorClaimGate.Inspect(duplicateSubject, Options(), session).ClaimsMatchIssuerAndSession);
    }
}
