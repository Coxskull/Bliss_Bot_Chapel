using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Tests.Api;

// AGM-COMM-015: direct-controller non-production negative regressions.
// These tests do not test middleware token signatures, grant consent or
// attempt to authenticate any actual user.
public sealed class AskAlphaVisitorConsentControllerDenyAllTests
{
    private const string Issuer="https://idp.example.invalid/realms/agm";

    private static BlissAuthenticationOptions Declared() => new()
    {
        Enabled=true,
        Authority=Issuer,
        AskAlphaVisitorIssuer=Issuer,
        AskAlphaVisitorEnrollmentRequested=true,
        ClientId="fictional-client-id"
    };

    private static ClaimsPrincipal FictionalPrincipal() => new(
        new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role,"bliss.ask_alpha_visitor",
                ClaimValueTypes.String,Issuer),
            new Claim("sub","fixture-visitor-12345",ClaimValueTypes.String,Issuer)
        },"fictional-test-cookie"));

    private static AskAlphaVisitorConsentController Controller(
        BlissAuthenticationOptions config, ClaimsPrincipal? principal) => new(config)
    {
        ControllerContext=new ControllerContext
        {
            HttpContext=new DefaultHttpContext
            {
                User=principal ?? new ClaimsPrincipal()
            }
        }
    };

    private static AskAlphaVisitorConsentDecision Denied(IActionResult action)
    {
        var objectResult=Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status409Conflict,objectResult.StatusCode);
        var result=Assert.IsType<AskAlphaVisitorConsentDecision>(objectResult.Value);
        Assert.Equal("NOT_RECORDED",result.VisitorConsentStatus);
        Assert.False(result.AuthoritativeOwnershipVerified);
        Assert.False(result.AuthenticatedVisitorConsentRecorded);
        Assert.False(result.TranscriptExportAuthorized);
        Assert.False(result.PaymentAuthorized);
        Assert.False(result.CreatorPayoutAuthorized);
        Assert.False(result.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",result.Delivery);
        return result;
    }

    private static AskAlphaVisitorConsentCommand Intent(bool selected=true) =>
        new("CREATIVE_PREVIEW_ONLY","AGM-CONSENT-PREVIEW-V1",
            selected,"fictional_test_request_123");

    [Fact]
    public void Readiness_never_claims_backend_or_real_consent()
    {
        var controller=Controller(Declared(),FictionalPrincipal());
        var response=controller.Readiness();
        var ok=Assert.IsType<OkObjectResult>(response.Result);
        var decision=Assert.IsType<AskAlphaVisitorConsentDecision>(ok.Value);
        Assert.Equal("REAL_VISITOR_CONSENT_BACKEND_NOT_CONFIGURED",decision.Status);
        Assert.False(decision.AuthoritativeOwnershipVerified);
        Assert.False(decision.AuthenticatedVisitorConsentRecorded);
        Assert.False(decision.TranscriptExportAuthorized);
        Assert.False(decision.PaymentAuthorized);
        Assert.False(decision.CreatorPayoutAuthorized);
        Assert.False(decision.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",decision.Delivery);
    }

    [Fact]
    public void Fictional_fully_declared_visitor_still_cannot_opt_in_or_withdraw()
    {
        var controller=Controller(Declared(),FictionalPrincipal());
        Assert.Equal("AUTHENTICATED_OPT_IN_STORAGE_UNAVAILABLE",
            Denied(controller.OptIn(Intent())).Status);
        Assert.Equal("AUTHENTICATED_WITHDRAWAL_STORAGE_UNAVAILABLE",
            Denied(controller.Withdraw(Intent(selected:false))).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Default_disabled_configuration_denies_both_commands(bool withdrawal)
    {
        var controller=Controller(new BlissAuthenticationOptions(),FictionalPrincipal());
        var request=Intent(selected:!withdrawal);
        var result=withdrawal?controller.Withdraw(request):controller.OptIn(request);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",Denied(result).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_identity_denies_both_commands(bool withdrawal)
    {
        var controller=Controller(Declared(),null);
        var request=Intent(selected:!withdrawal);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            Denied(withdrawal?controller.Withdraw(request):controller.OptIn(request)).Status);
    }

    [Fact]
    public void Consent_inputs_cannot_override_the_backend_denial()
    {
        var controller=Controller(Declared(),FictionalPrincipal());
        Assert.Equal("CONSENT_COMMAND_INPUT_INVALID",
            Denied(controller.OptIn(Intent(selected:false))).Status);
        Assert.Equal("CONSENT_COMMAND_INPUT_INVALID",
            Denied(controller.Withdraw(Intent(selected:true))).Status);
        Assert.Equal("CONSENT_COMMAND_INPUT_INVALID",
            Denied(controller.OptIn(new AskAlphaVisitorConsentCommand(
                "TRANSCRIPT_EXPORT","AGM-CONSENT-PREVIEW-V1",true,"fictional_test_request_123"))).Status);
        Assert.Equal("CONSENT_COMMAND_INPUT_INVALID",
            Denied(controller.OptIn(new AskAlphaVisitorConsentCommand(
                "CREATIVE_PREVIEW_ONLY","UNAPPROVED_VERSION",true,"fictional_test_request_123"))).Status);
        Assert.Equal("CONSENT_COMMAND_INPUT_INVALID",
            Denied(controller.OptIn(null!)).Status);
        Assert.Equal("CONSENT_COMMAND_INPUT_INVALID",
            Denied(controller.Withdraw(null!)).Status);
    }

    [Fact]
    public void Identity_stitching_does_not_allow_consent_at_controller_boundary()
    {
        var principal=FictionalPrincipal();
        principal.AddIdentity(new ClaimsIdentity(new []
        {
            new Claim("sub","other-person-subject-123",ClaimValueTypes.String,Issuer)
        }));
        var controller=Controller(Declared(),principal);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            Denied(controller.OptIn(Intent())).Status);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            Denied(controller.Withdraw(Intent(selected:false))).Status);
    }

    [Fact]
    public void Routes_remain_authorized_rate_limited_and_uncached()
    {
        var type=typeof(AskAlphaVisitorConsentController);
        Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute),inherit:true));
        var cache=Assert.Single(type.GetCustomAttributes(
            typeof(ResponseCacheAttribute),inherit:true).Cast<ResponseCacheAttribute>());
        Assert.True(cache.NoStore);

        foreach(var action in new[]{nameof(AskAlphaVisitorConsentController.OptIn),
                                     nameof(AskAlphaVisitorConsentController.Withdraw)})
        {
            var method=Assert.NotNull(type.GetMethod(action));
            Assert.Single(method.GetCustomAttributes(
                typeof(EnableRateLimitingAttribute),inherit:true));
        }

        // Guard against accidentally injecting a storage service into what
        // is deliberately only a fail-closed controller.
        var ctor=Assert.Single(type.GetConstructors());
        var parameter=Assert.Single(ctor.GetParameters());
        Assert.Equal(typeof(BlissAuthenticationOptions),parameter.ParameterType);
    }
}
