using System.Security.Claims;
using Bliss.Api.Controllers;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bliss.Tests.Api;

public sealed class StarterReceiptOperatorAccessTests
{
    [Fact]
    public void Restricted_mode_allows_exact_receipt_read_only()
    {
        const string path = "/api/advertisers/operator-review/starter-receipts";
        Assert.True(Bliss.Api.Runtime.PublicAdvertiserRegistrationGate.IsAllowed("GET", new PathString(path)));
        Assert.False(Bliss.Api.Runtime.PublicAdvertiserRegistrationGate.IsAllowed("POST", new PathString(path)));
        Assert.False(Bliss.Api.Runtime.PublicAdvertiserRegistrationGate.IsAllowed("GET", new PathString(path + "/other")));
    }

    [Theory]
    [InlineData(false, "operator", "https://trusted.example")]
    [InlineData(true, "customer", "https://trusted.example")]
    [InlineData(true, "operator", "https://untrusted.example")]
    public async Task Unauthorized_receipt_read_is_refused_before_database_access(bool enabled, string subject, string issuer)
    {
        var auth = new BlissAuthenticationOptions
        {
            Enabled = enabled, SupabaseEnabled = true, AdvertiserOperatorReviewRequested = true,
            Authority = "https://trusted.example",
            ApprovedRegistrationOperatorSubjects = ["operator"],
            ApprovedTestSubjects = ["operator"]
        };
        var controller = new AdvertiserOperatorReviewController(null!, auth,
            NullLogger<AdvertiserOperatorReviewController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                    new Claim("sub", subject),
                    new Claim(auth.RoleClaimType, auth.OperatorRole, ClaimValueTypes.String, issuer)
                }, "fixture"))
            }}
        };
        var result = Assert.IsType<StatusCodeResult>(await controller.StarterReceipts(default));
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }
}
