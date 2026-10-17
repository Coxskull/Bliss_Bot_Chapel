using System.Security.Claims;
using Bliss.Api.Security;
using Microsoft.IdentityModel.Tokens;

namespace Bliss.Tests.Api;

public sealed class SupabaseOidcTests
{
    private const string Subject = "d9729419-50a9-4c55-a2aa-3b1ae5386e23";
    private static BlissAuthenticationOptions Options(params string[] subjects) => new()
    {
        Enabled = true, SupabaseEnabled = true,
        Authority = "https://example.supabase.co/auth/v1", ClientId = "bliss-test-client",
        ApprovedTestSubjects = subjects
    };
    private static ClaimsIdentity Identity() => new([new Claim("sub", Subject)], "oidc", "name", "roles");
    private static ClaimsPrincipal Access(string subject = Subject, string clientId = "bliss-test-client", string? app = null, string? user = null)
    {
        var claims = new List<Claim> { new("sub", subject), new("client_id", clientId) };
        if (app is not null) claims.Add(new("app_metadata", app));
        if (user is not null) claims.Add(new("user_metadata", user));
        return new(new ClaimsIdentity(claims, "verified-access-token"));
    }
    [Fact]
    public void Empty_approval_list_denies_every_login() => Assert.Throws<SecurityTokenValidationException>(
        () => SupabaseOidc.ApplyVerifiedAccessToken(Identity(), Access(), Options()));

    [Theory]
    [InlineData("different-subject", "bliss-test-client")]
    [InlineData(Subject, "different-client")]
    public void Tokens_must_bind_to_same_subject_and_registered_client(string subject, string client) =>
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(Identity(), Access(subject, client), Options(Subject)));

    [Fact]
    public void User_metadata_and_existing_profile_roles_cannot_grant_access()
    {
        var identity = Identity();
        identity.AddClaim(new("roles", "bliss.admin"));
        SupabaseOidc.ApplyVerifiedAccessToken(identity, Access(user: "{\"bliss_roles\":[\"bliss.admin\"]}"), Options(Subject));
        Assert.Empty(identity.FindAll("roles"));
        Assert.False(BlissAuthorization.CanWrite(new(identity), Options(Subject)));
    }
    [Fact]
    public void Server_metadata_preserves_roles_and_exact_workspace_scope()
    {
        const string workspace = "a28c3643-8b35-4772-8bb4-382fdfab3af2";
        var identity = Identity();
        SupabaseOidc.ApplyVerifiedAccessToken(identity, Access(app:
            "{\"bliss_roles\":[\"bliss.reviewer\",\"unknown.admin\"],\"bliss_review_workspace_ids\":[\"" + workspace + "\",\"invalid\"]}"), Options(Subject));
        var principal = new ClaimsPrincipal(identity);
        Assert.Equal("bliss.reviewer", Assert.Single(identity.FindAll("roles")).Value);
        Assert.True(BlissAuthorization.CanReviewWorkspace(principal, Options(Subject), Guid.Parse(workspace)));
        Assert.False(BlissAuthorization.CanReviewWorkspace(principal, Options(Subject), Guid.NewGuid()));
        Assert.False(BlissAuthorization.CanWrite(principal, Options(Subject)));
    }
}
