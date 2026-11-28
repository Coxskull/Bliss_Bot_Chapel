using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;

public sealed class StaffCreativeReadinessTests
{
    private const string Authority = "https://issuer.example";
    private static BlissAuthenticationOptions Options() => new()
    {
        Enabled = true, SupabaseEnabled = true, Authority = Authority,
        StaffCreativeReadinessRequested = true,
        ApprovedTestSubjects = ["staff"], ApprovedRegistrationOperatorSubjects = ["staff"]
    };

    private static ClaimsPrincipal Principal(string subject = "staff", string role = "bliss.operator",
        string issuer = Authority, params Claim[] extra) =>
        new(new ClaimsIdentity(new[] { new Claim("sub", subject),
            new Claim("roles", role, ClaimValueTypes.String, issuer, issuer) }.Concat(extra),
            "test", "name", "roles"));

    [Fact]
    public void Exact_get_only_and_default_off()
    {
        var path = new PathString("/api/internal/creative-readiness");
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", path));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("GET", path, staffCreativeReadinessEnabled: true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST", path, staffCreativeReadinessEnabled: true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", new PathString("/ai-workspace.html"), staffCreativeReadinessEnabled: true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", new PathString("/api/internal/creative-readiness/other"), staffCreativeReadinessEnabled: true));
    }

    [Fact]
    public void Preparation_is_separate_exact_post_only()
    {
        var path = new PathString("/api/internal/creative-preparation");
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST", path, staffCreativeReadinessEnabled: true));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST", path, staffCreativeReadinessEnabled: true, staffCreativePreparationEnabled: true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", path, staffCreativePreparationEnabled: true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST", new PathString("/api/internal/creative-preparation/other"), staffCreativePreparationEnabled: true));
        var id = Guid.Parse("f63c329c-d45d-421b-a3f5-a02b0f842ccb");
        var digest = StaffCreativePreparationController.CampaignDigest(id, "Campaign", "Brief");
        Assert.Equal(Bliss.Api.Operations.ControlledAiService.Hash(id.ToString("D") + "\nCampaign\nBrief"), digest);
        Assert.NotEqual(digest, StaffCreativePreparationController.CampaignDigest(id, "Campaign", "Changed"));
    }

    [Theory]
    [InlineData("/staff-creative-preparation.html")]
    [InlineData("/staff-creative-preparation.js")]
    public void Staff_preparation_console_has_exact_default_off_static_routes(string path)
    {
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", new PathString(path), staffCreativeReadinessEnabled: true));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("GET", new PathString(path), staffCreativePreparationEnabled: true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("POST", new PathString(path), staffCreativePreparationEnabled: true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", new PathString(path + "/other"), staffCreativePreparationEnabled: true));
    }

    [Fact]
    public void Brand_confirmation_requires_explicit_approval_and_exact_revision()
    {
        var request = new StaffCampaignPreparationRequest(Guid.NewGuid(), new string('a', 64), true, null, null, null, null,
            "CONFIRM_DRAFT", null, 1, Guid.NewGuid().ToString("D"));
        Assert.False(StaffCreativePreparationController.ValidBrandAction(request));
        Assert.True(StaffCreativePreparationController.ValidBrandAction(request with { CustomerBrandApproved = true }));
        Assert.False(StaffCreativePreparationController.ValidBrandAction(request with { CustomerBrandApproved = true, BrandExpectedVersion = 0 }));
        Assert.False(StaffCreativePreparationController.ValidBrandAction(request with { CustomerBrandApproved = true, Family = "other" }));
        Assert.False(StaffCreativePreparationController.ValidBrandAction(request with { CustomerBrandApproved = true, BrandIdempotencyKey = "invalid" }));
    }

    [Fact]
    public void Only_approved_issuer_staff_can_read()
    {
        var auth = Options();
        Assert.True(StaffCreativeReadinessController.IsStaff(Principal(), auth));
        Assert.True(StaffCreativeReadinessController.IsStaff(Principal(role: "bliss.admin"), auth));
        Assert.False(StaffCreativeReadinessController.IsStaff(Principal(subject: "customer"), auth));
        Assert.False(StaffCreativeReadinessController.IsStaff(Principal(role: "bliss.advertiser"), auth));
        Assert.False(StaffCreativeReadinessController.IsStaff(Principal(issuer: "untrusted"), auth));
        Assert.False(StaffCreativeReadinessController.IsStaff(Principal(extra: [new Claim("bliss_advertiser_self_service", "true")]), auth));
        Assert.False(StaffCreativeReadinessController.IsStaff(Principal(extra: [new Claim("roles", "bliss.operator")]), auth));
        Assert.False(StaffCreativeReadinessController.IsStaff(new ClaimsPrincipal(), auth));
        auth.StaffCreativeReadinessRequested = false;
        Assert.False(StaffCreativeReadinessController.IsStaff(Principal(), auth));
    }
}
