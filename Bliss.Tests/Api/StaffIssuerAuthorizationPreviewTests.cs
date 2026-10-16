using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// Fictional fixture tests only. No real IdP signature, live cookie, hosted
// authority decision, production wiring, or consent activation is asserted.
public sealed class StaffIssuerAuthorizationPreviewTests
{
    private const string Trusted = "https://idp.example.invalid/agm";
    private const string Foreign = "https://foreign.example.invalid/agm";
    private const string Scope = "bliss_review_workspace_id";
    private static readonly Guid WorkspaceA =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WorkspaceB =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static BlissAuthenticationOptions Config(string authority = Trusted) => new()
    {
        Enabled = true,
        Authority = authority,
        ClientId = "fictional-staff-preview-client",
        RoleClaimType = "roles",
        ReviewerWorkspaceClaimType = Scope
    };

    private static Claim Role(string name, string issuer = Trusted,
        string? originalIssuer = null) =>
        new("roles", name, ClaimValueTypes.String, issuer, originalIssuer ?? issuer);

    private static Claim Workspace(Guid id, string issuer = Trusted,
        string? originalIssuer = null) =>
        new(Scope, id.ToString("D"), ClaimValueTypes.String,
            issuer, originalIssuer ?? issuer);

    private static ClaimsIdentity Identity(params Claim[] claims) =>
        new(claims, "fictional-oidc-cookie", "name", "roles");

    private static ClaimsPrincipal Principal(params ClaimsIdentity[] identities) =>
        new(identities);

    [Fact]
    public void Trusted_token_role_allows_only_its_preview_scope()
    {
        var principal = Principal(Identity(Role("bliss.reviewer"), Workspace(WorkspaceA)));
        Assert.True(StaffIssuerAuthorizationPreview.CanReview(principal, Config(), Trusted));
        Assert.True(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceA));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceB));
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(principal, Config(), Trusted));
    }

    [Fact]
    public void Foreign_role_with_same_value_cannot_grant_staff_access()
    {
        var principal = Principal(Identity(
            Role("bliss.admin", Foreign), Role("bliss.reviewer", Foreign),
            Workspace(WorkspaceA)));
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReview(principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceA));
    }

    [Fact]
    public void Trusted_reviewer_with_foreign_scope_cannot_cross_tenants()
    {
        var principal = Principal(Identity(
            Role("bliss.reviewer"), Workspace(WorkspaceA, Foreign)));
        Assert.True(StaffIssuerAuthorizationPreview.CanReview(principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceA));
    }

    [Fact]
    public void Trusted_admin_retains_explicit_preview_administrator_rights()
    {
        var principal = Principal(Identity(Role("bliss.admin")));
        Assert.True(StaffIssuerAuthorizationPreview.CanWrite(principal, Config(), Trusted));
        Assert.True(StaffIssuerAuthorizationPreview.CanReview(principal, Config(), Trusted));
        Assert.True(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceB));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, Guid.Empty));
    }

    [Fact]
    public void Cross_identity_claim_stitching_is_rejected_even_when_each_matches()
    {
        var principal = Principal(
            Identity(Role("bliss.reviewer")),
            Identity(Workspace(WorkspaceA)));
        Assert.False(StaffIssuerAuthorizationPreview.CanReview(principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceA));
    }

    [Fact]
    public void Unauthenticated_identity_and_extra_unauthenticated_identity_are_denied()
    {
        var unauthenticated = new ClaimsIdentity(new[] { Role("bliss.admin") });
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(
            Principal(unauthenticated), Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(
            Principal(Identity(Role("bliss.admin")), unauthenticated), Config(), Trusted));
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://idp.example.invalid/agm")]
    [InlineData("https://localhost/agm")]
    [InlineData("https://idp.example.invalid/agm?client=unapproved")]
    [InlineData("https://foreign.example.invalid/agm")]
    public void Missing_invalid_or_unapproved_issuer_denies_all(string approvedIssuer)
    {
        var principal = Principal(Identity(Role("bliss.admin"), Workspace(WorkspaceA)));
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(
            principal, Config(), approvedIssuer));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), approvedIssuer, WorkspaceA));
    }

    [Fact]
    public void Mismatched_provider_configuration_and_disabled_auth_deny()
    {
        var principal = Principal(Identity(Role("bliss.admin")));
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(
            principal, Config(Foreign), Trusted));
        var disabled = Config();
        disabled.Enabled = false;
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(
            principal, disabled, Trusted));
    }

    [Fact]
    public void Forged_original_issuer_metadata_is_rejected()
    {
        var principal = Principal(Identity(
            Role("bliss.reviewer", Trusted, Foreign),
            Workspace(WorkspaceA, Trusted, Foreign)));
        Assert.False(StaffIssuerAuthorizationPreview.CanReview(
            principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceA));
    }

    [Fact]
    public void Visitor_role_cannot_be_elevated_to_staff_by_preview_gate()
    {
        var principal = Principal(Identity(
            Role("bliss.ask_alpha_visitor"), Workspace(WorkspaceA)));
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(
            principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReview(
            principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceA));
    }

    [Fact]
    public void Foreign_role_and_trusted_role_same_value_are_not_interchangeable()
    {
        var principal = Principal(Identity(
            Role("bliss.reviewer", Foreign),
            Workspace(WorkspaceA, Trusted)));
        Assert.False(StaffIssuerAuthorizationPreview.CanReview(
            principal, Config(), Trusted));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, Config(), Trusted, WorkspaceA));
    }
}
