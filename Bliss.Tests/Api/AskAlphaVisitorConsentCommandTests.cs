using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Tests.Api;

public sealed class AskAlphaVisitorConsentCommandTests
{
    private const string Issuer = "https://idp.example.invalid/realms/agm";
    private static BlissAuthenticationOptions Options(bool enabled=true) => new()
    {
        Enabled = enabled,
        Authority = Issuer,
        AskAlphaVisitorIssuer = Issuer,
        AskAlphaVisitorEnrollmentRequested = enabled,
        ClientId = "fictional-visitor-client"
    };
    private static ClaimsPrincipal Principal(string role = "bliss.ask_alpha_visitor",
        string issuer = Issuer)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role,role,ClaimValueTypes.String,issuer),
            new Claim("sub","visitor-subject-12345",ClaimValueTypes.String,issuer)
        }, "fictional-cookie-auth");
        return new ClaimsPrincipal(identity);
    }
    private static AskAlphaVisitorConsentCommand Intent(
        bool selected = true, string purpose = "CREATIVE_PREVIEW_ONLY",
        string version = "AGM-CONSENT-PREVIEW-V1",
        string key = "fixture_key_12345") =>
        new(purpose,version,selected,key);

    [Fact]
    public void Even_a_matching_synthetic_visitor_opt_in_cannot_issue_real_consent()
    {
        var row = AskAlphaVisitorConsentCommandGate.Inspect(
            Principal(),Options(),Intent(),withdrawal:false);
        Assert.Equal("AUTHENTICATED_OPT_IN_STORAGE_UNAVAILABLE",row.Status);
        Assert.Equal("NOT_RECORDED",row.VisitorConsentStatus);
        Assert.False(row.AuthoritativeOwnershipVerified);
        Assert.False(row.AuthenticatedVisitorConsentRecorded);
        Assert.False(row.TranscriptExportAuthorized);
        Assert.False(row.PaymentAuthorized);
        Assert.False(row.CreatorPayoutAuthorized);
        Assert.False(row.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",row.Delivery);
    }

    [Fact]
    public void A_synthetic_withdrawal_does_not_misrepresent_storage_or_authorization()
    {
        var row = AskAlphaVisitorConsentCommandGate.Inspect(
            Principal(),Options(),Intent(selected:false),withdrawal:true);
        Assert.Equal("AUTHENTICATED_WITHDRAWAL_STORAGE_UNAVAILABLE",row.Status);
        Assert.False(row.AuthenticatedVisitorConsentRecorded);
        Assert.False(row.TranscriptExportAuthorized);
        Assert.Equal("NOT_SENT",row.Delivery);
    }

    [Theory]
    [InlineData("bliss.advertiser")]
    [InlineData("bliss.admin")]
    [InlineData("bliss.operator")]
    [InlineData("bliss.reviewer")]
    [InlineData("bliss.viewer")]
    public void Privileged_or_advertiser_roles_are_not_visitor_consent_signers(string role)
    {
        var row = AskAlphaVisitorConsentCommandGate.Inspect(
            Principal(role),Options(),Intent(),withdrawal:false);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",row.Status);
        Assert.False(row.AuthenticatedVisitorConsentRecorded);
    }

    [Fact]
    public void Bad_issuer_disabled_login_and_unauthenticated_user_are_denied()
    {
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            AskAlphaVisitorConsentCommandGate.Inspect(
                Principal(),Options(false),Intent(),false).Status);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            AskAlphaVisitorConsentCommandGate.Inspect(
                Principal(issuer:"LOCAL AUTHORITY"),Options(),Intent(),false).Status);
        Assert.Equal("VISITOR_IDENTITY_PREREQUISITES_MISSING",
            AskAlphaVisitorConsentCommandGate.Inspect(
                null,Options(),Intent(),false).Status);
    }

    [Theory]
    [InlineData(false,"CREATIVE_PREVIEW_ONLY","AGM-CONSENT-PREVIEW-V1","fixture_key_12345",false)]
    [InlineData(true,"TRANSCRIPT_EXPORT","AGM-CONSENT-PREVIEW-V1","fixture_key_12345",false)]
    [InlineData(true,"CREATIVE_PREVIEW_ONLY","OUTDATED","fixture_key_12345",false)]
    [InlineData(true,"CREATIVE_PREVIEW_ONLY","AGM-CONSENT-PREVIEW-V1","../unsafe",false)]
    [InlineData(true,"CREATIVE_PREVIEW_ONLY","AGM-CONSENT-PREVIEW-V1","short",false)]
    [InlineData(true,"CREATIVE_PREVIEW_ONLY","AGM-CONSENT-PREVIEW-V1","fixture_key_12345",true)]
    public void Invalid_scope_version_checkbox_key_and_withdrawal_intent_all_fail_closed(
        bool selected,string purpose,string version,string key,bool withdrawal)
    {
        var row=AskAlphaVisitorConsentCommandGate.Inspect(
            Principal(),Options(),Intent(selected,purpose,version,key),withdrawal);
        Assert.Equal("CONSENT_COMMAND_INPUT_INVALID",row.Status);
        Assert.Equal("NOT_RECORDED",row.VisitorConsentStatus);
        Assert.False(row.AuthenticatedVisitorConsentRecorded);
        Assert.False(row.TranscriptExportAuthorized);
    }

    [Fact]
    public void Direct_controller_actions_never_write_return_conflict_and_hide_visitor_data()
    {
        var controller=new AskAlphaVisitorConsentController(Options())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal() }
            }
        };
        var ready=Assert.IsType<OkObjectResult>(controller.Readiness().Result);
        var baseline=Assert.IsType<AskAlphaVisitorConsentDecision>(ready.Value);
        Assert.Equal("NOT_RECORDED",baseline.VisitorConsentStatus);
        var optIn=Assert.IsType<ObjectResult>(controller.OptIn(Intent()));
        Assert.Equal(StatusCodes.Status409Conflict,optIn.StatusCode);
        var intent=Assert.IsType<AskAlphaVisitorConsentDecision>(optIn.Value);
        Assert.Equal("AUTHENTICATED_OPT_IN_STORAGE_UNAVAILABLE",intent.Status);
        Assert.False(intent.AuthenticatedVisitorConsentRecorded);
        var revocation=Assert.IsType<ObjectResult>(controller.Withdraw(Intent(selected:false)));
        Assert.Equal(StatusCodes.Status409Conflict,revocation.StatusCode);
        var withdrawal=Assert.IsType<AskAlphaVisitorConsentDecision>(revocation.Value);
        Assert.Equal("AUTHENTICATED_WITHDRAWAL_STORAGE_UNAVAILABLE",withdrawal.Status);
        Assert.False(withdrawal.TranscriptExportAuthorized);
    }

    [Fact]
    public void Consent_controller_requires_authentication_no_store_and_write_rate_limits()
    {
        var type=typeof(AskAlphaVisitorConsentController);
        Assert.NotNull(Attribute.GetCustomAttribute(type,typeof(AuthorizeAttribute)));
        var cache=Assert.IsType<ResponseCacheAttribute>(
            Attribute.GetCustomAttribute(type,typeof(ResponseCacheAttribute)));
        Assert.True(cache.NoStore);
        foreach(var method in new[]{nameof(AskAlphaVisitorConsentController.OptIn),
            nameof(AskAlphaVisitorConsentController.Withdraw)})
        {
            var action=type.GetMethod(method)!;
            Assert.NotNull(Attribute.GetCustomAttribute(action,typeof(HttpPostAttribute)));
            Assert.NotNull(Attribute.GetCustomAttribute(action,typeof(EnableRateLimitingAttribute)));
        }
    }
}
