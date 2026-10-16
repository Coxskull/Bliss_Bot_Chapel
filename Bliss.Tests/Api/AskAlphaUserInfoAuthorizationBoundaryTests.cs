using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;

// AGM-COMM-015: test the actual non-authorizing UserInfo callback helper.
// Only claims present before this callback may have been signed by the IdP.
// These tests do not substitute for hosted signed-token acceptance.
public sealed class AskAlphaUserInfoAuthorizationBoundaryTests
{
    private const string Issuer="https://idp.example.invalid/realms/agm";
    private const string WorkspaceClaim="bliss_review_workspace_id";
    private static readonly Guid First=Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second=Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static BlissAuthenticationOptions Options() => new()
    {
        Enabled=true,Authority=Issuer,ClientId="fictional-idp-client",
        AskAlphaVisitorIssuer=Issuer,RoleClaimType="roles",
        ReviewerWorkspaceClaimType=WorkspaceClaim,
        AskAlphaVisitorEnrollmentRequested=true
    };

    private static ClaimsIdentity IdToken(params Claim[] claims) =>
        new(claims,"fictional-validated-token","name","roles");

    private static Claim Signed(string type,string value) =>
        new(type,value,ClaimValueTypes.String,Issuer,Issuer);

    private static ClaimsPrincipal Principal(ClaimsIdentity identity) =>
        new(OidcRoleClaimMapper.Align(identity,"name","roles"));

    private static void ApplyUserInfo(ClaimsIdentity identity,string json)
    {
        using var doc=JsonDocument.Parse(json);
        OidcRoleClaimMapper.IgnoreUntrustedUserInfoAuthorizationClaims(
            identity,doc.RootElement,Options());
    }

    // Unlike the direct helper tests below, run ASP.NET's configured
    // OnUserInformationReceived event to detect future callback rewiring.
    private static async Task InvokeUserInfoCallback(ClaimsIdentity identity,string json)
    {
        var oidc=new OpenIdConnectOptions();
        OidcRoleClaimMapper.Configure(oidc,Options());
        using var document=JsonDocument.Parse(json);
        var context=new UserInformationReceivedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme(
                OpenIdConnectDefaults.AuthenticationScheme,
                "OIDC",
                typeof(OpenIdConnectHandler)),
            oidc,new ClaimsPrincipal(identity),new AuthenticationProperties())
        {
            User=document
        };
        await oidc.Events.OnUserInformationReceived(context);
    }

    [Fact]
    public async Task Configured_userinfo_callback_does_not_grant_staff_or_foreign_tenant()
    {
        var identity=IdToken(
            Signed("roles","bliss.reviewer"),
            Signed(WorkspaceClaim,First.ToString("D")));
        OidcRoleClaimMapper.Normalize(identity,"roles");
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity,WorkspaceClaim);

        await InvokeUserInfoCallback(identity,"""
            {"roles":["bliss.admin","bliss.operator","bliss.reviewer"],
             "bliss_review_workspace_id":["22222222-2222-2222-2222-222222222222"]}
            """);

        var principal=Principal(identity);
        Assert.False(BlissAuthorization.CanWrite(principal,Options()));
        Assert.True(BlissAuthorization.CanReviewWorkspace(principal,Options(),First));
        Assert.False(BlissAuthorization.CanReviewWorkspace(principal,Options(),Second));
        Assert.False(principal.IsInRole("bliss.admin"));
        Assert.Equal(First.ToString("D"),
            Assert.Single(identity.FindAll(WorkspaceClaim)).Value);
    }

    [Fact]
    public async Task Configured_userinfo_callback_ignores_administrator_and_workspace_injection()
    {
        var identity=IdToken(Signed("sub","fictional-viewer"));
        await InvokeUserInfoCallback(identity,"""
            {"roles":["bliss.admin"],"bliss_review_workspace_id":
             ["11111111-1111-1111-1111-111111111111"]}
            """);
        var principal=Principal(identity);
        Assert.False(BlissAuthorization.CanWrite(principal,Options()));
        Assert.False(BlissAuthorization.CanReview(principal,Options()));
        Assert.False(BlissAuthorization.CanReviewWorkspace(principal,Options(),First));
        Assert.Empty(identity.FindAll("roles"));
        Assert.Empty(identity.FindAll(WorkspaceClaim));
    }

    [Fact]
    public async Task Configured_userinfo_callback_preserves_signed_visitor_revocation()
    {
        var identity=IdToken(
            Signed("sub","fictional-visitor"),
            Signed("roles","bliss.ask_alpha_visitor"));
        OidcRoleClaimMapper.Normalize(identity,"roles");
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity,Options());
        Assert.Single(identity.FindAll(ClaimTypes.Role));

        await InvokeUserInfoCallback(identity,"""{"roles":null}""");
        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        var readiness=AskAlphaVisitorEnrollmentGate.Inspect(Principal(identity),Options());
        Assert.False(readiness.RealVisitorIdentityAccepted);
        Assert.False(readiness.VisitorConsentRecorded);
        Assert.False(readiness.TranscriptExportAuthorized);
        Assert.False(readiness.PaymentAuthorized);
        Assert.False(readiness.CreatorPayoutAuthorized);
        Assert.False(readiness.CampaignDeliveryAuthorized);
    }

    [Fact]
    public void Userinfo_cannot_create_operator_admin_or_reviewer_access()
    {
        var identity=IdToken(Signed("sub","fictional-user"));
        ApplyUserInfo(identity,"""
            {"roles":["bliss.admin","bliss.operator","bliss.reviewer"],"role":"bliss.admin",
             "bliss_review_workspace_id":["11111111-1111-1111-1111-111111111111"]}
            """);

        var principal=Principal(identity);
        Assert.False(BlissAuthorization.CanWrite(principal,Options()));
        Assert.False(BlissAuthorization.CanReview(principal,Options()));
        Assert.False(BlissAuthorization.CanReviewWorkspace(principal,Options(),First));
        Assert.Empty(identity.FindAll("roles"));
        Assert.Empty(identity.FindAll(WorkspaceClaim));
    }

    [Fact]
    public void Validated_viewer_cannot_escalate_to_staff_through_userinfo()
    {
        var identity=IdToken(Signed("roles","bliss.viewer"));
        OidcRoleClaimMapper.Normalize(identity,"roles");
        ApplyUserInfo(identity,"""
            {"roles":["bliss.admin","bliss.operator"],"bliss_review_workspace_id":
            ["11111111-1111-1111-1111-111111111111"]}
            """);
        var principal=Principal(identity);
        Assert.True(principal.IsInRole("bliss.viewer"));
        Assert.False(BlissAuthorization.CanWrite(principal,Options()));
        Assert.False(BlissAuthorization.CanReview(principal,Options()));
        Assert.False(BlissAuthorization.CanReviewWorkspace(principal,Options(),First));
    }

    [Fact]
    public void Signed_reviewer_scope_survives_but_userinfo_cannot_extend_tenant_access()
    {
        var identity=IdToken(
            Signed("roles","bliss.reviewer"),
            Signed(WorkspaceClaim,First.ToString("D")));
        OidcRoleClaimMapper.Normalize(identity,"roles");
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity,WorkspaceClaim);
        ApplyUserInfo(identity,$$"""
            {"roles":["bliss.reviewer","bliss.admin"],
             "bliss_review_workspace_id":["{{Second:D}}"]}
            """);

        var principal=Principal(identity);
        Assert.True(BlissAuthorization.CanReviewWorkspace(principal,Options(),First));
        Assert.False(BlissAuthorization.CanReviewWorkspace(principal,Options(),Second));
        Assert.False(BlissAuthorization.CanWrite(principal,Options()));
        Assert.Equal(First.ToString("D"),Assert.Single(identity.FindAll(WorkspaceClaim)).Value);
    }

    [Fact]
    public void Signed_visitor_projection_is_revoked_by_later_userinfo_roles()
    {
        var identity=IdToken(
            Signed("sub","fictional-visitor"),
            Signed("roles","bliss.ask_alpha_visitor"));
        OidcRoleClaimMapper.Normalize(identity,"roles");
        OidcRoleClaimMapper.ProjectValidatedVisitorRole(identity,Options());
        Assert.Single(identity.FindAll(ClaimTypes.Role));

        ApplyUserInfo(identity,"""
            {"roles":["bliss.ask_alpha_visitor"],"bliss_review_workspace_id":
            ["22222222-2222-2222-2222-222222222222"]}
            """);
        Assert.Empty(identity.FindAll(ClaimTypes.Role));
        Assert.Empty(identity.FindAll(WorkspaceClaim));
        var decision=AskAlphaVisitorEnrollmentGate.Inspect(Principal(identity),Options());
        Assert.False(decision.RealVisitorIdentityAccepted);
        Assert.False(decision.VisitorConsentRecorded);
        Assert.False(decision.TranscriptExportAuthorized);
        Assert.False(decision.PaymentAuthorized);
        Assert.False(decision.CreatorPayoutAuthorized);
        Assert.False(decision.CampaignDeliveryAuthorized);
        Assert.Equal("NOT_SENT",decision.Delivery);
    }

    [Fact]
    public void Profile_only_userinfo_does_not_remove_signed_staff_authority()
    {
        var identity=IdToken(Signed("roles","bliss.operator"));
        OidcRoleClaimMapper.Normalize(identity,"roles");
        ApplyUserInfo(identity,"""{"nickname":"Fictional Operator"}""");
        Assert.True(BlissAuthorization.CanWrite(Principal(identity),Options()));
    }
}
