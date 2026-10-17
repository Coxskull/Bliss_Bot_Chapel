using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: fictional claim provenance and scope only.
// These checks do not validate tokens, establish real ownership, or grant consent.
public sealed class AskAlphaVisitorClaimOriginAndScopeTests
{
    private const string Issuer = "https://idp.fixture.invalid/realms/agm";
    private const string Foreign = "https://other-idp.fixture.invalid/realms/agm";
    private const string Role = "bliss.ask_alpha_visitor";

    private static BlissAuthenticationOptions Options() => new()
    {
        Enabled = true,
        Authority = Issuer,
        ClientId = "fictional-visitor-client",
        AskAlphaVisitorIssuer = Issuer,
        AskAlphaVisitorEnrollmentRequested = true
    };

    private static ClaimsPrincipal Principal(Guid session, string? originalRole = null,
        string? originalSubject = null, string? originalSession = null)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, Role, ClaimValueTypes.String,
                Issuer, originalRole ?? Issuer),
            new Claim("sub", "fictional-visitor-12345", ClaimValueTypes.String,
                Issuer, originalSubject ?? Issuer),
            new Claim("ask_alpha_session_id", session.ToString("D"),
                ClaimValueTypes.String, Issuer, originalSession ?? Issuer)
        }, "fictional-cookie", "name", ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("subject")]
    [InlineData("session")]
    public void Historical_session_readiness_denies_foreign_original_issuer(
        string field)
    {
        var id = Guid.NewGuid();
        var principal = Principal(id,
            originalRole: field == "role" ? Foreign : null,
            originalSubject: field == "subject" ? Foreign : null,
            originalSession: field == "session" ? Foreign : null);

        var decision = AskAlphaVisitorClaimGate.Inspect(principal, Options(), id);

        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING", decision.Status);
        Assert.False(decision.ClaimsMatchIssuerAndSession);
        Assert.False(decision.HistoricalSessionOwnershipVerified);
        Assert.False(decision.TranscriptExportAuthorized);
        Assert.Equal("NOT_SENT", decision.Delivery);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("subject")]
    public void New_enrollment_gate_denies_foreign_original_issuer(string field)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, Role, ClaimValueTypes.String, Issuer,
                field == "role" ? Foreign : Issuer),
            new Claim("sub", "fictional-visitor-12345", ClaimValueTypes.String,
                Issuer, field == "subject" ? Foreign : Issuer)
        }, "fictional-cookie", "name", ClaimTypes.Role);
        var reading = AskAlphaVisitorEnrollmentGate.Inspect(
            new ClaimsPrincipal(identity), Options());

        Assert.Equal("PROVIDER_DECLARED_NEW_VISITOR_ENROLLMENT_NOT_ACTIVE",
            reading.Status);
        Assert.False(reading.IssuerSubjectClaimsConsistent);
        Assert.False(reading.NewVisitorSessionCreated);
        Assert.False(reading.VisitorConsentRecorded);
    }

    [Theory]
    [InlineData("ASK_ALPHA_SESSION_ID")]
    [InlineData("ADVERTISER_ID")]
    [InlineData("BLISS_REVIEW_WORKSPACE_ID")]
    public void New_enrollment_rejects_case_variant_existing_scope_claims(
        string claimType)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, Role, ClaimValueTypes.String, Issuer, Issuer),
            new Claim("sub", "fictional-visitor-12345", ClaimValueTypes.String,
                Issuer, Issuer),
            new Claim(claimType, "existing-but-not-visitor-owned",
                ClaimValueTypes.String, Foreign, Foreign)
        }, "fictional-cookie", "name", ClaimTypes.Role);

        var result = AskAlphaVisitorEnrollmentGate.Inspect(
            new ClaimsPrincipal(identity), Options());

        Assert.False(result.IssuerSubjectClaimsConsistent);
        Assert.False(result.NewVisitorSessionCreated);
        Assert.False(result.RealVisitorIdentityAccepted);
    }

    [Fact]
    public void Matching_origin_claims_retain_only_non_authorizing_readiness()
    {
        var session = Guid.NewGuid();
        var reading = AskAlphaVisitorClaimGate.Inspect(
            Principal(session), Options(), session);
        Assert.True(reading.ClaimsMatchIssuerAndSession);
        Assert.Equal("OIDC_CLAIMS_PRESENT_HISTORY_UNVERIFIED", reading.Status);
        Assert.False(reading.HistoricalSessionOwnershipVerified);
        Assert.False(reading.VisitorConsentRecorded);
        Assert.False(reading.TranscriptExportAuthorized);
        Assert.False(reading.CampaignDeliveryAuthorized);
    }

    [Fact]
    public void Clean_enrollment_prerequisite_still_never_grants_real_identity()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, Role, ClaimValueTypes.String, Issuer, Issuer),
            new Claim("sub", "fictional-visitor-12345", ClaimValueTypes.String,
                Issuer, Issuer)
        }, "fictional-cookie", "name", ClaimTypes.Role);
        var result = AskAlphaVisitorEnrollmentGate.Inspect(
            new ClaimsPrincipal(identity), Options());

        Assert.Equal("NEW_VISITOR_OIDC_CLAIMS_DECLARED_SERVER_CREATION_PENDING",
            result.Status);
        Assert.True(result.IssuerSubjectClaimsConsistent);
        Assert.False(result.RealVisitorIdentityAccepted);
        Assert.False(result.NewVisitorSessionCreated);
        Assert.False(result.VisitorConsentRecorded);
        Assert.Equal("NOT_SENT", result.Delivery);
    }
}
