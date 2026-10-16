using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015 consolidated negative coverage. These tests do not
// enable enrollment, create visitor sessions or grant transcript access.
public sealed class AskAlphaVisitorConsentBoundaryRegressionTests
{
    private const string Issuer = "https://idp.example.invalid/realms/agm";

    private static BlissAuthenticationOptions Options() => new()
    {
        Enabled = true,
        Authority = Issuer,
        AskAlphaVisitorIssuer = Issuer,
        AskAlphaVisitorEnrollmentRequested = true,
        ClientId = "fictional-visitor-client"
    };

    private static ClaimsPrincipal Principal(params Claim[] additional)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "bliss.ask_alpha_visitor", ClaimValueTypes.String, Issuer),
            new("sub", "visitor-subject-12345", ClaimValueTypes.String, Issuer)
        };
        claims.AddRange(additional);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "fictional-cookie-auth"));
    }

    private static AskAlphaVisitorConsentCommand Intent() =>
        new("CREATIVE_PREVIEW_ONLY", "AGM-CONSENT-PREVIEW-V1", true, "fixture_key_12345");

    [Theory]
    [InlineData("bliss.advertiser")]
    [InlineData("bliss.admin")]
    [InlineData("bliss.operator")]
    [InlineData("bliss.reviewer")]
    [InlineData("bliss.viewer")]
    public void Mixed_privileged_and_visitor_roles_never_authorize_consent(string otherRole)
    {
        var principal = Principal(new Claim(ClaimTypes.Role, otherRole, ClaimValueTypes.String, Issuer));
        var result = AskAlphaVisitorConsentCommandGate.Inspect(principal, Options(), Intent(), false);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING", result.Status);
        Assert.False(result.AuthenticatedVisitorConsentRecorded);
        Assert.False(result.TranscriptExportAuthorized);
        Assert.Equal("NOT_SENT", result.Delivery);
    }

    [Fact]
    public void Duplicate_subject_claims_cannot_bootstrap_consent()
    {
        var principal = Principal(new Claim("sub", "visitor-subject-99999", ClaimValueTypes.String, Issuer));
        var result = AskAlphaVisitorConsentCommandGate.Inspect(principal, Options(), Intent(), false);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING", result.Status);
        Assert.False(result.AuthoritativeOwnershipVerified);
        Assert.False(result.AuthenticatedVisitorConsentRecorded);
    }

    [Fact]
    public void Client_or_historical_session_claim_cannot_bootstrap_new_ownership()
    {
        var options = Options();
        var principal = Principal(new Claim(options.AskAlphaVisitorSessionClaimType,
            Guid.NewGuid().ToString("D"), ClaimValueTypes.String, Issuer));
        var readiness = AskAlphaVisitorEnrollmentGate.Inspect(principal, options);
        Assert.False(readiness.IssuerSubjectClaimsConsistent);
        var result = AskAlphaVisitorConsentCommandGate.Inspect(principal, options, Intent(), false);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING", result.Status);
        Assert.False(result.AuthenticatedVisitorConsentRecorded);
    }

    [Fact]
    public void Advertiser_binding_cannot_be_recast_as_a_visitor_session()
    {
        var options = Options();
        var principal = Principal(new Claim(options.AdvertiserIdClaimType,
            Guid.NewGuid().ToString("D"), ClaimValueTypes.String, Issuer));
        var result = AskAlphaVisitorConsentCommandGate.Inspect(principal, options, Intent(), false);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING", result.Status);
        Assert.False(result.TranscriptExportAuthorized);
        Assert.Equal("NOT_SENT", result.Delivery);
    }

    [Fact]
    public void No_visitor_consent_backend_is_ever_implied_by_matching_claims()
    {
        var result = AskAlphaVisitorConsentCommandGate.Inspect(Principal(), Options(), Intent(), false);
        Assert.Equal("AUTHENTICATED_OPT_IN_STORAGE_UNAVAILABLE", result.Status);
        Assert.Equal("NOT_RECORDED", result.VisitorConsentStatus);
        Assert.False(result.AuthenticatedVisitorConsentRecorded);
        Assert.False(result.PaymentAuthorized);
        Assert.False(result.CreatorPayoutAuthorized);
        Assert.False(result.CampaignDeliveryAuthorized);
    }
}
