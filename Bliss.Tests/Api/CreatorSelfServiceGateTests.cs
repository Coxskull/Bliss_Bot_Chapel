using System.Net;
using System.Security.Claims;
using Bliss.Api.Security;
using Microsoft.IdentityModel.Tokens;

namespace Bliss.Tests.Api;

// No real credentials, CRM leads or paid provider calls. These prove only
// the opt-in signed creator-role and tenant identity boundary, not live signup.
public sealed class CreatorSelfServiceGateTests
{
    private const string CreatorSubject = "f54bbb41-0e6f-4c0c-90cc-620677950bbe";
    private const string OtherSubject = "c27fdc92-a845-4ae9-bfc6-b880713e39f6";
    private const string CreatorId = "5e85f8fa-1fe8-43b5-94e1-93a91ad57b6c";
    private const string Issuer = "https://synthetic-issuer.supabase.co/auth/v1";

    private static BlissAuthenticationOptions Options(bool enabled = true) => new()
    {
        Enabled = true, SupabaseEnabled = true,
        CreatorSelfServiceRequested = enabled,
        Authority = Issuer,
        ClientId = "fixture-client",
        ApprovedCreatorSubjects = [CreatorSubject]
    };

    private static ClaimsIdentity Identity(
        string subject = CreatorSubject, bool verifiedEmail = true)
    {
        var claims = new List<Claim> { new("sub", subject) };
        if (verifiedEmail) claims.Add(new Claim("email_verified", "true"));
        return new ClaimsIdentity(claims, "oidc", "name", "roles");
    }

    private static ClaimsPrincipal Access(
        string subject = CreatorSubject, string? metadata = null, string? userMetadata = null)
    {
        metadata ??= $$"""{"bliss_roles":["bliss.creator"],"bliss_creator_id":"{{CreatorId}}","bliss_creator_verified":true}""";
        var claims = new List<Claim> {
            new("sub", subject),
            new("client_id", "fixture-client"),
            new("app_metadata", metadata)
        };
        if (userMetadata is not null) claims.Add(new Claim("user_metadata", userMetadata));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "verified-access-token"));
    }

    [Fact]
    public void Disabled_by_default_creator_enrollment_does_not_issue_account_role()
    {
        var id = Identity();
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(id, Access(), Options(false)));
        Assert.False(BlissAuthorization.TryResolveVerifiedCreatorId(
            new ClaimsPrincipal(id), Options(false), out _));
    }

    [Fact]
    public void Approved_signed_and_verified_creator_gets_only_exact_self_scope()
    {
        var id = Identity();
        SupabaseOidc.ApplyVerifiedAccessToken(id, Access(userMetadata:
            """{"bliss_roles":["bliss.admin"],"bliss_creator_id":"00000000-0000-0000-0000-000000000001"}"""),
            Options());
        var principal = new ClaimsPrincipal(id);
        Assert.True(BlissAuthorization.TryResolveVerifiedCreatorId(
            principal, Options(), out var actual));
        Assert.Equal(Guid.Parse(CreatorId), actual);
        Assert.Equal("bliss.creator", Assert.Single(id.FindAll("roles")).Value);
        Assert.Equal(Issuer, Assert.Single(id.FindAll("roles")).Issuer);
        Assert.False(BlissAuthorization.CanWrite(principal, Options()));
        Assert.False(BlissAuthorization.CanReview(principal, Options()));
        Assert.Empty(id.FindAll("advertiser_id"));
    }

    [Fact]
    public void Unapproved_or_overlap_subject_is_denied_even_if_issuer_says_creator()
    {
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(
                Identity(OtherSubject), Access(OtherSubject), Options()));
        var overlap = Options();
        overlap.ApprovedTestSubjects = [CreatorSubject];
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(Identity(), Access(), overlap));
    }

    [Theory]
    [InlineData("""{"bliss_roles":["bliss.creator"],"bliss_creator_id":"5e85f8fa-1fe8-43b5-94e1-93a91ad57b6c"}""")]
    [InlineData("""{"bliss_roles":["bliss.creator"],"bliss_creator_verified":true,"bliss_creator_id":"not-a-guid"}""")]
    [InlineData("""{"bliss_roles":["bliss.creator","bliss.admin"],"bliss_creator_verified":true,"bliss_creator_id":"5e85f8fa-1fe8-43b5-94e1-93a91ad57b6c"}""")]
    [InlineData("""{"bliss_roles":["bliss.viewer"],"bliss_creator_verified":true,"bliss_creator_id":"5e85f8fa-1fe8-43b5-94e1-93a91ad57b6c"}""")]
    [InlineData("""{"bliss_roles":["bliss.creator"],"bliss_creator_verified":false,"bliss_creator_id":"5e85f8fa-1fe8-43b5-94e1-93a91ad57b6c"}""")]
    [InlineData("""{"bliss_roles":["bliss.creator"],"bliss_creator_verified":true,"bliss_creator_id":"5e85f8fa-1fe8-43b5-94e1-93a91ad57b6c","bliss_advertiser_id":"11111111-1111-1111-1111-111111111111"}""")]
    public void Missing_verification_bad_scope_or_mixed_roles_fails_closed(string metadata)
    {
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(
                Identity(), Access(metadata: metadata), Options()));
    }

    [Fact]
    public void Missing_verified_email_does_not_produce_authenticated_creator()
    {
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(
                Identity(verifiedEmail: false), Access(), Options()));
    }

    [Fact]
    public void Forged_role_issuer_duplicate_scope_and_different_subject_are_denied()
    {
        var id = Identity();
        SupabaseOidc.ApplyVerifiedAccessToken(id, Access(), Options());

        id.AddClaim(new Claim("roles", "bliss.admin",
            ClaimValueTypes.String, "https://foreign-issuer.example"));
        Assert.False(BlissAuthorization.TryResolveVerifiedCreatorId(new(id), Options(), out _));
        id.RemoveClaim(id.FindAll("roles").Last());

        id.AddClaim(new Claim("bliss_creator_id", Guid.NewGuid().ToString("D"),
            ClaimValueTypes.String, Issuer));
        Assert.False(BlissAuthorization.TryResolveVerifiedCreatorId(new(id), Options(), out _));
        id.RemoveClaim(id.FindAll("bliss_creator_id").Last());

        id.RemoveClaim(id.FindFirst("sub")!);
        id.AddClaim(new Claim("sub", OtherSubject));
        Assert.False(BlissAuthorization.TryResolveVerifiedCreatorId(new(id), Options(), out _));
    }

    [Fact]
    public async Task Public_or_test_header_claim_cannot_open_creator_me_endpoint()
    {
        await using var factory = new OidcSecurityApiFactory();
        using var anonymous = factory.CreateSecureClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/creators/me")).StatusCode);

        using var spoofed = factory.CreateSecureClient();
        spoofed.DefaultRequestHeaders.Add("X-Test-Name", "Fixture Creator");
        spoofed.DefaultRequestHeaders.Add("X-Test-Roles", "bliss.creator,bliss.admin");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await spoofed.GetAsync("/api/creators/me")).StatusCode);
    }
}
