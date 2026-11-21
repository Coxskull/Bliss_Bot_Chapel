using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;

public sealed class PublicAdvertiserRegistrationGateTests
{
    [Theory]
    [InlineData("GET", "/advertiser-registration.html")]
    [InlineData("GET", "/advertiser-registration.js")]
    [InlineData("GET", "/advertiser-account-privacy.html")]
    [InlineData("GET", "/advertiser-account-terms.html")]
    [InlineData("GET", "/alpha-intake.css")]
    [InlineData("GET", "/advertiser-login.html")]
    [InlineData("GET", "/api/auth/session")]
    [InlineData("GET", "/api/auth/login")]
    [InlineData("GET", "/api/auth/supabase-config")]
    [InlineData("GET", "/oauth-consent.html")]
    [InlineData("GET", "/oauth-consent.css")]
    [InlineData("GET", "/oauth-consent.bundle.js")]
    [InlineData("GET", "/signin-oidc")]
    [InlineData("POST", "/signin-oidc")]
    [InlineData("POST", "/api/advertisers/register")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("GET", "/")]
    [InlineData("GET", "/health/live")]
    public void Only_registration_consent_and_OIDC_routes_are_public(string method, string path) =>
        Assert.True(PublicAdvertiserRegistrationGate.IsAllowed(method, new PathString(path)));

    [Theory]
    [InlineData("POST", "/api/creators/register")]
    [InlineData("GET", "/api/advertisers/me")]
    [InlineData("GET", "/api/advertisers")]
    [InlineData("GET", "/api/advertisers/privacy-requests")]
    [InlineData("POST", "/api/advertisers/privacy-requests")]
    [InlineData("GET", "/api/advertisers/privacy-requests/operator-queue")]
    [InlineData("GET", "/api/campaigns")]
    [InlineData("POST", "/api/operations/trigger")]
    [InlineData("GET", "/swagger/index.html")]
    [InlineData("GET", "/operations/index.html")]
    [InlineData("GET", "/oauth-consent.js")]
    [InlineData("POST", "/oauth-consent.html")]
    [InlineData("GET", "/oauth-consent.html/other")]
    [InlineData("GET", "/api/auth/logout")]
    [InlineData("GET", "/api/advertisers/register")]
    [InlineData("POST", "/api/auth/session")]
    [InlineData("DELETE", "/api/advertisers/register")]
    [InlineData("POST", "/join-advertiser.html")]
    [InlineData("GET", "/api/advertisers/register/foo")]
    [InlineData("POST", "/signin-oidc/bypass")]
    public void Other_business_and_wrong_method_routes_remain_denied(string method, string path) =>
        Assert.False(PublicAdvertiserRegistrationGate.IsAllowed(method, new PathString(path)));

    [Fact]
    public async Task Denied_request_returns_503_without_dispatching_pipeline()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/campaigns";
        context.Response.Body = new MemoryStream();
        var continued = false;
        await PublicAdvertiserRegistrationGate.InvokeAsync(context, () =>
        {
            continued = true;
            return Task.CompletedTask;
        });
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.False(continued);
        Assert.Equal("advertiser-registration-only", context.Response.Headers["X-Bliss-Mode"]);
    }
}
