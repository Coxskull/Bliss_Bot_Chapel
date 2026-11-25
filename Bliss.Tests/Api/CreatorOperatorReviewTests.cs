using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bliss.Tests.Api;

public sealed class CreatorOperatorReviewTests
{
    private const string Issuer = "https://alpha-auth.example.invalid";
    private static BlissAuthenticationOptions Options(bool enabled) => new()
    {
        Enabled = true, SupabaseEnabled = true, Authority = Issuer,
        CreatorOperatorReviewRequested = enabled,
        ApprovedTestSubjects = ["operator"],
        ApprovedRegistrationOperatorSubjects = ["operator"]
    };
    private static CreatorOperatorReviewController Controller(BlissAuthenticationOptions options, ClaimsPrincipal user)
        => new(null!, options, NullLogger<CreatorOperatorReviewController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } } };
    private static ClaimsPrincipal User(BlissAuthenticationOptions options, string subject, string issuer, params Claim[] additional)
        => new(new ClaimsIdentity(new[] { new Claim("sub", subject),
            new Claim(options.RoleClaimType, options.OperatorRole, ClaimValueTypes.String, issuer) }.Concat(additional), "test"));

    [Fact]
    public async Task Disabled_unapproved_forged_and_pending_identities_cannot_read_or_approve()
    {
        var options = Options(true);
        var request = new CreatorOperatorApproval(Guid.NewGuid(), Guid.NewGuid(), true, true);
        foreach (var c in new[]
        {
            Controller(Options(false), User(options, "operator", Issuer)),
            Controller(options, User(options, "unapproved", Issuer)),
            Controller(options, User(options, "operator", "untrusted")),
            Controller(options, User(options, "operator", Issuer,
                new Claim(BlissAuthenticationOptions.CreatorRegistrationPendingMarkerClaimType, "true")))
        })
        {
            Assert.Equal(403, Assert.IsType<StatusCodeResult>(await c.Queue(CancellationToken.None)).StatusCode);
            Assert.Equal(403, Assert.IsType<StatusCodeResult>(await c.Approve(request, CancellationToken.None)).StatusCode);
        }
    }

    [Fact]
    public async Task Operator_must_review_profile_and_consent_before_database_access()
    {
        var options = Options(true);
        var c = Controller(options, User(options, "operator", Issuer));
        Assert.IsType<BadRequestObjectResult>(await c.Approve(
            new CreatorOperatorApproval(Guid.NewGuid(), Guid.NewGuid(), false, true), CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(await c.Approve(
            new CreatorOperatorApproval(Guid.NewGuid(), Guid.NewGuid(), true, false), CancellationToken.None));
    }

    [Fact]
    public void Exact_review_routes_do_not_open_creator_directory_or_dashboard()
    {
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", "/api/creators/operator-review", false));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("GET", "/api/creators/operator-review", true));
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed("POST", "/api/creators/operator-review", true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", "/api/creators", true));
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed("GET", "/api/creators/me", true));
    }
}
