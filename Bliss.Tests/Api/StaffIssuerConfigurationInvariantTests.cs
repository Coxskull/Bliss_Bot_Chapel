using System.Security.Claims;
using Bliss.Api.Security;

namespace Bliss.Tests.Api;

// Fictional issuer fixtures only. The preview is not wired into production.
public sealed class StaffIssuerConfigurationInvariantTests
{
    private const string Issuer = "https://idp.example.invalid/agm";
    private static readonly Guid Workspace = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static BlissAuthenticationOptions Config() => new()
    {
        Enabled = true,
        Authority = Issuer,
        ClientId = "fictional-client",
        RoleClaimType = "roles",
        ReviewerWorkspaceClaimType = "bliss_review_workspace_id"
    };

    private static ClaimsPrincipal User(string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim("roles", role, ClaimValueTypes.String, Issuer, Issuer),
            new Claim("bliss_review_workspace_id", Workspace.ToString("D"),
                ClaimValueTypes.String, Issuer, Issuer)
        }, "fictional-token", "name", "roles"));

    private static void MustDeny(BlissAuthenticationOptions config, ClaimsPrincipal principal)
    {
        Assert.False(StaffIssuerAuthorizationPreview.CanWrite(principal, config, Issuer));
        Assert.False(StaffIssuerAuthorizationPreview.CanReview(principal, config, Issuer));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, config, Issuer, Workspace));
    }

    [Fact]
    public void Empty_oidc_client_id_is_rejected()
    {
        var config = Config();
        config.ClientId = " ";
        MustDeny(config, User("bliss.admin"));
    }

    [Fact]
    public void Admin_and_reviewer_role_alias_collision_is_rejected()
    {
        var config = Config();
        config.AdminRole = config.ReviewerRole;
        MustDeny(config, User("bliss.reviewer"));
    }

    [Fact]
    public void Visitor_role_must_not_alias_admin_role()
    {
        var config = Config();
        config.AdminRole = config.AskAlphaVisitorRole;
        MustDeny(config, User("bliss.ask_alpha_visitor"));
    }

    [Fact]
    public void Role_and_workspace_claim_type_alias_is_rejected()
    {
        var config = Config();
        config.ReviewerWorkspaceClaimType = config.RoleClaimType;
        MustDeny(config, User("bliss.admin"));
    }

    [Fact]
    public void Missing_configured_role_label_is_rejected()
    {
        var config = Config();
        config.ViewerRole = string.Empty;
        MustDeny(config, User("bliss.admin"));
    }

    [Fact]
    public void Approved_distinct_operator_still_has_only_write_permission()
    {
        var config = Config();
        var principal = User("bliss.operator");
        Assert.True(StaffIssuerAuthorizationPreview.CanWrite(principal, config, Issuer));
        Assert.False(StaffIssuerAuthorizationPreview.CanReview(principal, config, Issuer));
        Assert.False(StaffIssuerAuthorizationPreview.CanReviewWorkspace(
            principal, config, Issuer, Workspace));
    }
}
