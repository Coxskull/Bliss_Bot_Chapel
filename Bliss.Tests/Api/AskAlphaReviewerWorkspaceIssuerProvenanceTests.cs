using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// AGM-COMM-015: fiction-only issuer provenance regressions for reviewer
// workspace claims. This does not certify an IdP, select a trusted staff
// issuer, activate live visitor consent, or change authorization policy.
public sealed class AskAlphaReviewerWorkspaceIssuerProvenanceTests
{
    private const string Scope = "bliss_review_workspace_id";
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Foreign = "https://foreign.example.invalid/agm";
    private const string Original = "https://origin.example.invalid/agm";
    private const string First = "11111111-1111-1111-1111-111111111111";
    private const string Second = "22222222-2222-2222-2222-222222222222";

    private static ClaimsIdentity Identity(params Claim[] claims) =>
        new(claims, "fictional-validated-token", "name", "roles");

    private static Claim Workspace(string value, string issuer = Trusted,
        string? originalIssuer = null) =>
        new(Scope, value, ClaimValueTypes.String, issuer, originalIssuer ?? issuer);

    [Fact]
    public void Single_signed_workspace_scope_retains_issuer_and_original_issuer()
    {
        var identity = Identity(Workspace(First, Trusted, Original));
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity, Scope);
        var scope = Assert.Single(identity.FindAll(Scope));
        Assert.Equal(First, scope.Value);
        Assert.Equal(Trusted, scope.Issuer);
        Assert.Equal(Original, scope.OriginalIssuer);
        Assert.NotEqual("LOCAL AUTHORITY", scope.Issuer);
    }

    [Fact]
    public void Identical_workspace_from_distinct_issuers_is_not_silently_collapsed()
    {
        var identity = Identity(
            Workspace(First, Trusted),
            Workspace(First, Foreign));
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity, Scope);
        var scopes = identity.FindAll(Scope).ToArray();
        Assert.Equal(2, scopes.Length);
        Assert.Contains(scopes, c => c.Value == First && c.Issuer == Trusted);
        Assert.Contains(scopes, c => c.Value == First && c.Issuer == Foreign);
        // BlissAuthorization remains issuer-agnostic; human provider and
        // security signoff is required before hosted staff authorization.
    }

    [Fact]
    public void Json_array_expands_uuid_scopes_without_losing_original_issuer()
    {
        var identity = Identity(
            Workspace($"""["{First}","{Second}"]""", Trusted, Original));
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity, Scope);
        var scopes = identity.FindAll(Scope).ToArray();
        Assert.Equal(2, scopes.Length);
        Assert.All(scopes, c => Assert.Equal(Trusted, c.Issuer));
        Assert.All(scopes, c => Assert.Equal(Original, c.OriginalIssuer));
        Assert.Contains(scopes, c => c.Value == First);
        Assert.Contains(scopes, c => c.Value == Second);
    }

    [Fact]
    public void Duplicate_valid_scope_same_issuer_normalizes_to_one_claim()
    {
        var identity = Identity(
            Workspace(First.ToUpperInvariant()),
            Workspace(First));
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity, Scope);
        var scope = Assert.Single(identity.FindAll(Scope));
        Assert.Equal(First, scope.Value);
        Assert.Equal(Trusted, scope.Issuer);
        Assert.Equal(Trusted, scope.OriginalIssuer);
    }

    [Fact]
    public void Invalid_guid_scopes_are_discarded_without_changing_valid_claim_origin()
    {
        var identity = Identity(
            Workspace("not-a-guid", Foreign),
            Workspace(Guid.Empty.ToString("D"), Trusted),
            Workspace(Second, Trusted, Original));
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity, Scope);
        var scope = Assert.Single(identity.FindAll(Scope));
        Assert.Equal(Second, scope.Value);
        Assert.Equal(Trusted, scope.Issuer);
        Assert.Equal(Original, scope.OriginalIssuer);
    }

    [Fact]
    public void Cookie_alignment_does_not_rewrite_workspace_scope_origin()
    {
        var identity = Identity(
            Workspace(First, Foreign, Original),
            new Claim("roles", "bliss.reviewer", ClaimValueTypes.String, Trusted, Trusted));
        OidcRoleClaimMapper.NormalizeReviewerWorkspaceAssignments(identity, Scope);
        var cookie = OidcRoleClaimMapper.Align(identity, "name", "roles");
        var scope = Assert.Single(cookie.FindAll(Scope));
        Assert.Equal(Foreign, scope.Issuer);
        Assert.Equal(Original, scope.OriginalIssuer);
        Assert.Equal(First, scope.Value);
        Assert.Equal(Trusted, Assert.Single(cookie.FindAll("roles")).Issuer);
    }
}
