using System.Security.Claims;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Http;

namespace Bliss.Tests.Api;

// Synthetic-only route acceptance tests. No OIDC provider, CRM, user
// invitation, database, network or application deployment is touched.
public sealed class CreatorApiBoundaryTests
{
    private static BlissAuthenticationOptions Options(bool feature = true) => new()
    {
        Enabled = true,
        SupabaseEnabled = true,
        CreatorSelfServiceRequested = feature,
        CreatorRole = "bliss.creator"
    };

    private static DefaultHttpContext Request(
        string method, string path,
        string? role = "bliss.creator",
        string claimType = "roles")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.User = role is null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(claimType, role, ClaimValueTypes.String,
                    "https://fixture.supabase.co/auth/v1")],
                "signed-fixture", "name", "roles"));
        return context;
    }

    [Theory]
    [InlineData("GET", "/api/auth/session")]
    [InlineData("GET", "/api/auth/login")]
    [InlineData("GET", "/api/auth/supabase-config")]
    [InlineData("GET", "/api/creators/me")]
    [InlineData("POST", "/api/auth/logout")]
    public void Only_necessary_creator_account_routes_are_permitted(string method, string path)
    {
        Assert.False(CreatorApiBoundary.ShouldDeny(Request(method, path), Options()));
    }

    [Theory]
    [InlineData("GET", "/api/creators")]
    [InlineData("GET", "/api/creators/3f4a3178-d74a-4a37-935b-55202753a2bb")]
    [InlineData("GET", "/api/campaigns")]
    [InlineData("GET", "/api/advertisers")]
    [InlineData("GET", "/api/operations")]
    [InlineData("GET", "/api/research")]
    [InlineData("GET", "/api/creators/me/history")]
    [InlineData("POST", "/api/creators/me")]
    [InlineData("PATCH", "/api/creators/me")]
    [InlineData("POST", "/api/campaigns")]
    [InlineData("GET", "/api/auth/logout")]
    [InlineData("HEAD", "/api/creators/me")]
    [InlineData("POST", "/api/auth/session")]
    public void Creator_principal_cannot_cross_into_legacy_authenticated_apis(string method, string path)
    {
        Assert.True(CreatorApiBoundary.ShouldDeny(Request(method, path), Options()));
    }

    [Fact]
    public void Mixed_staff_and_case_variant_creator_role_is_still_restricted()
    {
        var context = Request("GET", "/api/advertisers", "bliss.viewer");
        ((ClaimsIdentity)context.User.Identity!).AddClaim(
            new Claim("ROLES", "bliss.creator",
                ClaimValueTypes.String, "https://foreign.example"));
        Assert.True(CreatorApiBoundary.ShouldDeny(context, Options()));
    }

    [Fact]
    public void Anonymous_and_staff_accounts_keep_existing_policies_but_old_creator_cookies_remain_restricted()
    {
        Assert.False(CreatorApiBoundary.ShouldDeny(
            Request("GET", "/api/creators", role: null), Options()));
        Assert.False(CreatorApiBoundary.ShouldDeny(
            Request("GET", "/api/creators", role: "bliss.viewer"), Options()));

        // An operator can turn enrollment off without widening a stale
        // creator cookie into general authenticated marketplace privileges.
        Assert.True(CreatorApiBoundary.ShouldDeny(
            Request("GET", "/api/creators", role: "bliss.creator"), Options(false)));
        Assert.False(CreatorApiBoundary.ShouldDeny(
            Request("GET", "/api/creators/me", role: "bliss.creator"), Options(false)));
        var options = Options();
        options.Enabled = false;
        Assert.False(CreatorApiBoundary.ShouldDeny(
            Request("GET", "/api/creators", role: "bliss.creator"), options));
    }

    [Fact]
    public void Non_api_static_creator_dashboard_is_outside_route_restrictions()
    {
        Assert.False(CreatorApiBoundary.ShouldDeny(
            Request("GET", "/creator-dashboard.html"), Options()));
        Assert.False(CreatorApiBoundary.ShouldDeny(
            Request("GET", "/creator-dashboard.js"), Options()));
    }
}
