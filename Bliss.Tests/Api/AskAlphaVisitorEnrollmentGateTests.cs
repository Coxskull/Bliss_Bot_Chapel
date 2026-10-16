using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

public sealed class AskAlphaVisitorEnrollmentGateTests
{
    private const string Issuer = "https://idp.example.invalid/realms/agm";
    private static BlissAuthenticationOptions Options(bool requested=true) => new()
    {
        Enabled = true, Authority = Issuer, ClientId = "agm-visitor-client",
        AskAlphaVisitorIssuer = Issuer,
        AskAlphaVisitorEnrollmentRequested = requested
    };
    private static ClaimsPrincipal Principal(string role="bliss.ask_alpha_visitor",
        string issuer=Issuer, bool authenticated=true, string subject="visit-subject-12345")
    {
        var id=new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role,role,ClaimValueTypes.String,issuer),
            new Claim("sub",subject,ClaimValueTypes.String,issuer)
        },authenticated?"fixture-signed-cookie":null);
        return new ClaimsPrincipal(id);
    }

    [Fact]
    public void Fully_declared_synthetic_oidc_prerequisites_do_not_authorize_creation_consent_or_payment()
    {
        var reading=AskAlphaVisitorEnrollmentGate.Inspect(Principal(),Options());
        Assert.Equal("NEW_VISITOR_OIDC_CLAIMS_DECLARED_SERVER_CREATION_PENDING",reading.Status);
        Assert.True(reading.IdpConfigurationDeclared);
        Assert.True(reading.IssuerSubjectClaimsConsistent);
        Assert.False(reading.NewVisitorSessionCreated);
        Assert.False(reading.HistoricalSessionOwnershipVerified);
        Assert.False(reading.RealVisitorIdentityAccepted);
        Assert.False(reading.VisitorConsentRecorded);
        Assert.False(reading.TranscriptLinked);
        Assert.False(reading.TranscriptExportAuthorized);
        Assert.False(reading.PaymentAuthorized);
        Assert.False(reading.CreatorPayoutAuthorized);
        Assert.False(reading.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",reading.Delivery);
    }

    [Fact]
    public void Enrollment_is_default_off_and_must_have_https_exact_identity_provider_settings()
    {
        Assert.Equal("NEW_VISITOR_ENROLLMENT_NOT_AUTHORIZED",
            AskAlphaVisitorEnrollmentGate.Inspect(Principal(),new BlissAuthenticationOptions()).Status);
        Assert.Equal("NEW_VISITOR_ENROLLMENT_NOT_AUTHORIZED",
            AskAlphaVisitorEnrollmentGate.Inspect(Principal(),Options(false)).Status);
        var c=Options();
        c.Authority="http://insecure.example.invalid";
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(),c).IdpConfigurationDeclared);
        c=Options();
        c.AskAlphaVisitorIssuer="https://other-idp.example.invalid/realms/agm";
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(),c).IdpConfigurationDeclared);
        c=Options();
        c.Authority="https://idp.example.invalid/realms/another";
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(),c).IdpConfigurationDeclared);
        c=Options();
        c.Authority="https://evil@idp.example.invalid/realms/agm";
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(),c).IdpConfigurationDeclared);
        c=Options();
        c.AskAlphaVisitorIssuer=Issuer+"?origin=forged";
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(),c).IdpConfigurationDeclared);
        c=Options();
        c.ClientId="";
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(),c).IdpConfigurationDeclared);
        c=Options();
        c.Enabled=false;
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(),c).IdpConfigurationDeclared);
    }

    [Theory]
    [InlineData("bliss.admin")]
    [InlineData("bliss.operator")]
    [InlineData("bliss.reviewer")]
    [InlineData("bliss.advertiser")]
    [InlineData("bliss.viewer")]
    public void Staff_and_advertiser_accounts_cannot_enroll_as_visitors(string role)
    {
        var reading=AskAlphaVisitorEnrollmentGate.Inspect(Principal(role),Options());
        Assert.True(reading.IdpConfigurationDeclared);
        Assert.False(reading.IssuerSubjectClaimsConsistent);
        Assert.False(reading.NewVisitorSessionCreated);
    }

    [Fact]
    public void Principal_with_extra_unauthenticated_identity_is_denied()
    {
        var valid=Principal();
        valid.AddIdentity(new ClaimsIdentity(new[]
        {
            new Claim("source", "unverified-external-principal")
        }));
        var reading=AskAlphaVisitorEnrollmentGate.Inspect(valid,Options());
        Assert.True(reading.IdpConfigurationDeclared);
        Assert.False(reading.IssuerSubjectClaimsConsistent);
        Assert.False(reading.NewVisitorSessionCreated);
        Assert.False(reading.VisitorConsentRecorded);
        Assert.Equal("NOT_SENT",reading.Delivery);
    }

    [Fact]
    public void Principal_with_second_authenticated_identity_is_denied()
    {
        var valid=Principal();
        valid.AddIdentity(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role,"bliss.viewer",ClaimValueTypes.String,Issuer)
        },"fixture-other-cookie"));
        var reading=AskAlphaVisitorEnrollmentGate.Inspect(valid,Options());
        Assert.False(reading.IssuerSubjectClaimsConsistent);
        Assert.False(reading.RealVisitorIdentityAccepted);
        Assert.False(reading.TranscriptExportAuthorized);
    }

    [Fact]
    public void Subject_and_visitor_role_cannot_be_combined_across_identities()
    {
        var roleIdentity=new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role,"bliss.ask_alpha_visitor",
                ClaimValueTypes.String,Issuer)
        },"fixture-signed-cookie");
        var subjectIdentity=new ClaimsIdentity(new[]
        {
            new Claim("sub","visit-subject-12345",ClaimValueTypes.String,Issuer)
        });
        var principal=new ClaimsPrincipal(new[]{roleIdentity,subjectIdentity});
        var reading=AskAlphaVisitorEnrollmentGate.Inspect(principal,Options());
        Assert.False(reading.IssuerSubjectClaimsConsistent);
        Assert.False(reading.NewVisitorSessionCreated);
        Assert.False(reading.VisitorConsentRecorded);
    }

    [Fact]
    public void Malformed_forged_duplicate_or_existing_session_claims_are_rejected()
    {
        var cfg=Options();
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(issuer:"LOCAL AUTHORITY"),cfg).IssuerSubjectClaimsConsistent);
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(authenticated:false),cfg).IssuerSubjectClaimsConsistent);
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(null,cfg).IssuerSubjectClaimsConsistent);
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(Principal(subject:"bad subject"),cfg).IssuerSubjectClaimsConsistent);
        var duplicate=Principal();
        ((ClaimsIdentity)duplicate.Identity!).AddClaim(new Claim("sub","new-visitor-sub-456",ClaimValueTypes.String,Issuer));
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(duplicate,cfg).IssuerSubjectClaimsConsistent);
        var assigned=Principal();
        ((ClaimsIdentity)assigned.Identity!).AddClaim(new Claim(
            cfg.AskAlphaVisitorSessionClaimType, Guid.NewGuid().ToString("D"), ClaimValueTypes.String,Issuer));
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(assigned,cfg).IssuerSubjectClaimsConsistent);
        var advertiser=Principal();
        ((ClaimsIdentity)advertiser.Identity!).AddClaim(new Claim(
            cfg.AdvertiserIdClaimType,Guid.NewGuid().ToString("D"),ClaimValueTypes.String,Issuer));
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(advertiser,cfg).IssuerSubjectClaimsConsistent);
        var mixed=Principal();
        ((ClaimsIdentity)mixed.Identity!).AddClaim(new Claim(ClaimTypes.Role,cfg.AdminRole));
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(mixed,cfg).IssuerSubjectClaimsConsistent);
    }
}
