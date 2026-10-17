using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Tests.Api;

// All subjects, sessions, issuers and consent commands below are FICTIONAL.
// Passing shape checks never creates sessions, consent, export or payments.
public sealed class AskAlphaExistingVisitorSessionPreflightTests
{
    private const string Issuer = "https://idp.fixture.invalid/realm/agm";
    private const string Foreign = "https://foreign.fixture.invalid/realm/agm";

    private static BlissAuthenticationOptions Options(bool enabled = true) => new()
    {
        Enabled = enabled,
        Authority = Issuer,
        AskAlphaVisitorIssuer = Issuer,
        AskAlphaVisitorEnrollmentRequested = enabled,
        ClientId = "fictional-visitor-client"
    };

    private static ClaimsPrincipal ReturningPrincipal(
        Guid session,
        string sessionType = "ask_alpha_session_id",
        string roleIssuer = Issuer, string roleOrigin = Issuer,
        string subjectOrigin = Issuer, string sessionOrigin = Issuer,
        string? additionalType = null)
    {
        var claims = new List<Claim> {
            new(ClaimTypes.Role, "bliss.ask_alpha_visitor", ClaimValueTypes.String,
                roleIssuer, roleOrigin),
            new("sub", "fictional-visitor-subject-123", ClaimValueTypes.String,
                Issuer, subjectOrigin),
            new(sessionType, session.ToString("D"), ClaimValueTypes.String,
                Issuer, sessionOrigin)
        };
        if (additionalType is not null)
            claims.Add(new Claim(additionalType, "foreign-scope",
                ClaimValueTypes.String, Foreign, Foreign));
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims, "fictional-cookie", "name", ClaimTypes.Role));
    }

    private static AskAlphaVisitorConsentCommand Command(bool selected) =>
        new("CREATIVE_PREVIEW_ONLY", "AGM-CONSENT-PREVIEW-V1",
            selected, "fixture_existing_session_retry_123");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Returning_visitor_command_is_shaped_but_always_storage_denied(
        bool withdraw)
    {
        var principal = ReturningPrincipal(Guid.NewGuid());
        Assert.True(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
        // The enrollment prerequisite rejects already-issued session claims.
        Assert.False(AskAlphaVisitorEnrollmentGate.Inspect(principal, Options())
            .IssuerSubjectClaimsConsistent);

        var result = AskAlphaVisitorConsentCommandGate.Inspect(
            principal, Options(), Command(!withdraw), withdraw);
        Assert.Equal(withdraw
            ? "AUTHENTICATED_WITHDRAWAL_STORAGE_UNAVAILABLE"
            : "AUTHENTICATED_OPT_IN_STORAGE_UNAVAILABLE", result.Status);
        Assert.Equal("NOT_RECORDED", result.VisitorConsentStatus);
        Assert.False(result.AuthoritativeOwnershipVerified);
        Assert.False(result.AuthenticatedVisitorConsentRecorded);
        Assert.False(result.TranscriptExportAuthorized);
        Assert.False(result.PaymentAuthorized);
        Assert.False(result.CreatorPayoutAuthorized);
        Assert.False(result.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT", result.Delivery);

        var controller = new AskAlphaVisitorConsentController(Options())
        {
            ControllerContext = new ControllerContext {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
        var action = withdraw
            ? controller.Withdraw(Command(false))
            : controller.OptIn(Command(true));
        var conflict = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.False(Assert.IsType<AskAlphaVisitorConsentDecision>(
            conflict.Value).AuthenticatedVisitorConsentRecorded);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("subject")]
    [InlineData("session")]
    public void Foreign_claim_original_issuer_is_not_returning_visitor_authority(
        string field)
    {
        var principal = ReturningPrincipal(Guid.NewGuid(),
            roleOrigin: field == "role" ? Foreign : Issuer,
            subjectOrigin: field == "subject" ? Foreign : Issuer,
            sessionOrigin: field == "session" ? Foreign : Issuer);
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            AskAlphaVisitorConsentCommandGate.Inspect(
                principal, Options(), Command(false), withdrawal: true).Status);
    }

    [Theory]
    [InlineData("advertiser_id")]
    [InlineData("ADVERTISER_ID")]
    [InlineData("bliss_review_workspace_id")]
    [InlineData("BLISS_REVIEW_WORKSPACE_ID")]
    public void Advertiser_and_reviewer_scopes_deny_returning_visitor(
        string claimType)
    {
        var principal = ReturningPrincipal(Guid.NewGuid(), additionalType: claimType);
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            AskAlphaVisitorConsentCommandGate.Inspect(
                principal, Options(), Command(false), withdrawal: true).Status);
    }

    [Fact]
    public void Case_variant_session_type_cannot_impersonate_exact_session_claim()
    {
        var principal = ReturningPrincipal(Guid.NewGuid(),
            sessionType: "ASK_ALPHA_SESSION_ID");
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
    }

    [Fact]
    public void Case_variant_duplicate_session_claim_fails_closed()
    {
        var principal = ReturningPrincipal(Guid.NewGuid());
        var identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        identity.AddClaim(new Claim("ASK_ALPHA_SESSION_ID", Guid.NewGuid().ToString("D"),
            ClaimValueTypes.String, Foreign, Foreign));
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
    }

    [Fact]
    public void Zero_and_malformed_session_ids_are_denied()
    {
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            ReturningPrincipal(Guid.Empty), Options()));
        var principal = ReturningPrincipal(Guid.NewGuid());
        var identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        var session = identity.FindFirst("ask_alpha_session_id")!;
        identity.RemoveClaim(session);
        identity.AddClaim(new Claim("ask_alpha_session_id", "not-a-guid",
            ClaimValueTypes.String, Issuer, Issuer));
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
    }

    [Fact]
    public void Disabled_enrollment_or_mixed_identities_cannot_pass_preflight()
    {
        var principal = ReturningPrincipal(Guid.NewGuid());
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options(false)));
        principal.AddIdentity(new ClaimsIdentity(new[] {
            new Claim("sub", "attacker") }, "other"));
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
    }

    [Fact]
    public void Additional_role_claim_is_rejected_even_if_unknown_to_staff_registry()
    {
        var principal = ReturningPrincipal(Guid.NewGuid());
        var identity = Assert.IsType<ClaimsIdentity>(principal.Identity);
        identity.AddClaim(new Claim(ClaimTypes.Role, "fictional.privileged-role",
            ClaimValueTypes.String, Issuer, Issuer));
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
    }

    [Fact]
    public void New_enrollment_shape_keeps_existing_no_consent_behavior()
    {
        var identity = new ClaimsIdentity(new[] {
            new Claim(ClaimTypes.Role, "bliss.ask_alpha_visitor", ClaimValueTypes.String,
                Issuer, Issuer),
            new Claim("sub", "fictional-visitor-subject-123", ClaimValueTypes.String,
                Issuer, Issuer)
        }, "fictional-cookie", "name", ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        Assert.False(AskAlphaExistingVisitorSessionPreflight.MatchesUnverifiedClaimShape(
            principal, Options()));
        var result = AskAlphaVisitorConsentCommandGate.Inspect(
            principal, Options(), Command(true), false);
        Assert.Equal("AUTHENTICATED_OPT_IN_STORAGE_UNAVAILABLE", result.Status);
        Assert.False(result.AuthenticatedVisitorConsentRecorded);
    }
}
