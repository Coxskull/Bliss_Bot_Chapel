using System.Security.Claims;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace Bliss.Tests.Api;

// Synthetic tests only. No real authentication provider, user invitation
// or database writes.
public sealed class AdvertiserSelfServiceGateTests
{
    private const string Subject = "babb5c85-20db-44e1-b34f-90036aac74e9";
    private const string BusinessId = "89b5b0dc-0664-4cb4-a55d-61aa70d3470d";
    private const string Issuer = "https://synthetic-issuer.supabase.co/auth/v1";

    private static BlissAuthenticationOptions Options(bool requested = true) => new()
    {
        Enabled = true, SupabaseEnabled = true,
        AdvertiserSelfServiceRequested = requested,
        Authority = Issuer, ClientId = "fixture-client",
        ApprovedAdvertiserSubjects = [Subject]
    };

    private static ClaimsIdentity Identity(bool verifiedEmail = true)
    {
        var claims = new List<Claim> { new("sub", Subject) };
        if (verifiedEmail) claims.Add(new Claim("email_verified", "true"));
        return new ClaimsIdentity(claims, "oidc", "name", "roles");
    }

    private static ClaimsPrincipal Access(string? metadata = null)
    {
        metadata ??= $$"""{"bliss_roles":["bliss.advertiser"],"bliss_advertiser_id":"{{BusinessId}}","bliss_advertiser_verified":true}""";
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", Subject),
            new Claim("client_id", "fixture-client"),
            new Claim("app_metadata", metadata)
        ], "signed-access"));
    }

    [Fact]
    public void Default_off_does_not_create_advertiser_role()
    {
        var id = Identity();
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(id, Access(), Options(false)));
        Assert.False(BlissAuthorization.TryResolveVerifiedAdvertiserId(
            new ClaimsPrincipal(id), Options(false), out _));
    }

    [Fact]
    public void Verified_allowlisted_subject_is_scoped_to_its_business()
    {
        var options = Options();
        var id = Identity();
        SupabaseOidc.ApplyVerifiedAccessToken(id, Access(), options);
        Assert.True(BlissAuthorization.TryResolveVerifiedAdvertiserId(
            new ClaimsPrincipal(id), options, out var actual));
        Assert.Equal(Guid.Parse(BusinessId), actual);
        Assert.Equal(Issuer, Assert.Single(id.FindAll("roles")).Issuer);
        Assert.Equal(Issuer, Assert.Single(id.FindAll(BlissAuthenticationOptions.AdvertiserSelfServiceMarkerClaimType)).Issuer);
        Assert.False(BlissAuthorization.CanWrite(new ClaimsPrincipal(id), options));
        Assert.Empty(id.FindAll(options.CreatorIdClaimType));
    }

    [Theory]
    [InlineData("""{"bliss_roles":["bliss.advertiser"],"bliss_advertiser_id":"89b5b0dc-0664-4cb4-a55d-61aa70d3470d"}""")]
    [InlineData("""{"bliss_roles":["bliss.advertiser"],"bliss_advertiser_verified":false,"bliss_advertiser_id":"89b5b0dc-0664-4cb4-a55d-61aa70d3470d"}""")]
    [InlineData("""{"bliss_roles":["bliss.admin"],"bliss_advertiser_verified":true,"bliss_advertiser_id":"89b5b0dc-0664-4cb4-a55d-61aa70d3470d"}""")]
    [InlineData("""{"bliss_roles":["bliss.advertiser","bliss.admin"],"bliss_advertiser_verified":true,"bliss_advertiser_id":"89b5b0dc-0664-4cb4-a55d-61aa70d3470d"}""")]
    [InlineData("""{"bliss_roles":["bliss.advertiser"],"bliss_advertiser_verified":true,"bliss_advertiser_id":"not-a-guid"}""")]
    [InlineData("""{"bliss_roles":["bliss.advertiser"],"bliss_advertiser_verified":true,"bliss_advertiser_id":"89b5b0dc-0664-4cb4-a55d-61aa70d3470d","bliss_creator_id":"d65f4a92-18d0-4a3f-9f78-c72c6031ef4e"}""")]
    public void Unverified_or_cross_tenant_metadata_is_denied(string metadata)
    {
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(Identity(), Access(metadata), Options()));
    }

    [Fact]
    public void Unverified_email_and_overlap_with_test_subject_are_denied()
    {
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(Identity(false), Access(), Options()));
        var options = Options();
        options.ApprovedTestSubjects = [Subject];
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(Identity(), Access(), options));
    }

    [Fact]
    public void Duplicate_or_forged_role_scope_is_denied()
    {
        var options = Options();
        var id = Identity();
        SupabaseOidc.ApplyVerifiedAccessToken(id, Access(), options);
        id.AddClaim(new Claim("ROLES", "bliss.admin", ClaimValueTypes.String, Issuer));
        Assert.False(BlissAuthorization.TryResolveVerifiedAdvertiserId(
            new ClaimsPrincipal(id), options, out _));
        id.RemoveClaim(id.FindFirst("ROLES")!);
        id.AddClaim(new Claim("ADVERTISER_ID", BusinessId, ClaimValueTypes.String, Issuer));
        Assert.False(BlissAuthorization.TryResolveVerifiedAdvertiserId(
            new ClaimsPrincipal(id), options, out _));
    }

    [Fact]
    public void Legacy_advertiser_staff_role_does_not_inherit_customer_api_boundary()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/creators";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "bliss.advertiser", ClaimValueTypes.String, Issuer)],
            "signed-fixture"));
        Assert.False(AdvertiserApiBoundary.ShouldDeny(context, Options(false)));
    }

    [Theory]
    [InlineData("GET", "/api/auth/session", false)]
    [InlineData("GET", "/api/advertisers/me", false)]
    [InlineData("POST", "/api/auth/logout", false)]
    [InlineData("GET", "/api/creators", true)]
    [InlineData("GET", "/api/creators/me", true)]
    [InlineData("GET", "/api/advertisers", true)]
    [InlineData("GET", "/api/advertisers/89b5b0dc-0664-4cb4-a55d-61aa70d3470d", true)]
    [InlineData("GET", "/api/campaigns", true)]
    [InlineData("POST", "/api/auth/session", true)]
    [InlineData("POST", "/api/advertisers/me", true)]
    public void Advertiser_api_is_fail_closed(string method, string path, bool denied)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("roles", "bliss.advertiser", ClaimValueTypes.String, Issuer),
                new Claim(BlissAuthenticationOptions.AdvertiserSelfServiceMarkerClaimType,
                    "true", ClaimValueTypes.Boolean, Issuer)
            ],
            "signed-test"));
        Assert.Equal(denied, AdvertiserApiBoundary.ShouldDeny(context, Options(false)));
    }
}
