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
