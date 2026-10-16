using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: fictional UserInfo inputs must never mint tenant permission.
// These fixtures do not represent trusted signed tokens or hosted acceptance.
public sealed class AskAlphaUntrustedWorkspaceGrantBoundaryTests
{
    private const string Scope = "bliss_review_workspace_id";
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Foreign = "https://foreign.example.invalid/agm";
    private static readonly Guid First = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Second = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ClaimsIdentity Principal(params Claim[] claims) =>
        new(claims, "fictional-validated-oidc", "name", "roles");

    private static Claim ScopeClaim(Guid id, string issuer = Trusted) =>
        new(Scope, id.ToString("D"), ClaimValueTypes.String, issuer, issuer);

#pragma warning disable CS0618 // Validate compatibility entry point is deliberately inert.
    [Fact]
    public void UserInfo_array_cannot_create_a_workspace_grant()
    {
        var identity = Principal();
        using var user = JsonDocument.Parse(
            """{"bliss_review_workspace_id":["11111111-1111-1111-1111-111111111111"]}""");

        OidcRoleClaimMapper.ApplyReviewerWorkspaceAssignments(identity, user.RootElement, Scope);

        Assert.Empty(identity.FindAll(Scope));
    }

    [Fact]
    public void UserInfo_cannot_append_foreign_workspace_to_verified_scope()
    {
        var identity = Principal(ScopeClaim(First));
        using var user = JsonDocument.Parse(
            """{"bliss_review_workspace_id":"22222222-2222-2222-2222-222222222222"}""");

        OidcRoleClaimMapper.ApplyReviewerWorkspaceAssignments(identity, user.RootElement, Scope);

        var scope = Assert.Single(identity.FindAll(Scope));
        Assert.Equal(First.ToString("D"), scope.Value);
        Assert.Equal(Trusted, scope.Issuer);
        Assert.Equal(Trusted, scope.OriginalIssuer);
    }

    [Fact]
    public void UserInfo_cannot_replace_existing_foreign_issuer_origin()
    {
        var identity = Principal(ScopeClaim(Second, Foreign));
        using var user = JsonDocument.Parse(
            """{"bliss_review_workspace_id":["22222222-2222-2222-2222-222222222222"]}""");

        OidcRoleClaimMapper.ApplyReviewerWorkspaceAssignments(identity, user.RootElement, Scope);

        var scope = Assert.Single(identity.FindAll(Scope));
        Assert.Equal(Foreign, scope.Issuer);
        Assert.Equal(Foreign, scope.OriginalIssuer);
    }

    [Fact]
    public void UserInfo_cannot_create_local_authority_claims_even_on_duplicate_id()
    {
        var identity = Principal(ScopeClaim(First));
        using var user = JsonDocument.Parse(
            """{"bliss_review_workspace_id":["11111111-1111-1111-1111-111111111111","22222222-2222-2222-2222-222222222222"]}""");

        OidcRoleClaimMapper.ApplyReviewerWorkspaceAssignments(identity, user.RootElement, Scope);

        Assert.Single(identity.FindAll(Scope));
        Assert.DoesNotContain(identity.Claims, claim => claim.Issuer == "LOCAL AUTHORITY");
    }

    [Fact]
    public void Repeated_UserInfo_calls_are_inert()
    {
        var identity = Principal();
        using var user = JsonDocument.Parse(
            """{"bliss_review_workspace_id":"11111111-1111-1111-1111-111111111111"}""");

        OidcRoleClaimMapper.ApplyReviewerWorkspaceAssignments(identity, user.RootElement, Scope);
        OidcRoleClaimMapper.ApplyReviewerWorkspaceAssignments(identity, user.RootElement, Scope);

        Assert.Empty(identity.FindAll(Scope));
    }
#pragma warning restore CS0618

    [Fact]
    public void Signed_token_workspace_normalization_remains_supported()
    {
        var identity = Principal(ScopeClaim(First), ScopeClaim(Second));
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity, Scope);
        var scopes = identity.FindAll(Scope).ToArray();

        Assert.Equal(2, scopes.Length);
        Assert.All(scopes, claim => Assert.Equal(Trusted, claim.Issuer));
        Assert.Contains(scopes, claim => claim.Value == First.ToString("D"));
        Assert.Contains(scopes, claim => claim.Value == Second.ToString("D"));
    }
}
