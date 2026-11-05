using System.Security.Claims;
using Bliss.Api.Security;
using Microsoft.IdentityModel.Tokens;

namespace Bliss.Tests.Api;

// Task 4: the public creator INTEREST/CRM record is not a signed identity,
// and neither public form/user profile claims nor an unapproved role label
// may enroll a creator or grant operator, reviewer or admin permissions.
// These tests use synthetic claims and never contact a provider.
public sealed class CreatorInterestNoRoleEscalationTests
{
    private const string Approved = "0fd0e451-47ed-49ae-9484-5610aa85a4f7";
    private const string Unapproved = "9b14e98f-2191-4825-b3fa-e85572bc2a0b";

    private static BlissAuthenticationOptions Options() => new()
    {
        Enabled = true,
        SupabaseEnabled = true,
        Authority = "https://fixture.supabase.co/auth/v1",
        ClientId = "synthetic-bliss-client",
        ApprovedTestSubjects = [Approved]
    };

    private static ClaimsIdentity Identity(string sub = Approved)
        => new([new Claim("sub", sub)], "validated-id-token", "name", "roles");

    private static ClaimsPrincipal Access(
        string sub = Approved, string? appMetadata = null, string? userMetadata = null)
    {
        var claims = new List<Claim>
        {
            new("sub", sub),
            new("client_id", "synthetic-bliss-client")
        };
        if (appMetadata is not null) claims.Add(new("app_metadata", appMetadata));
        if (userMetadata is not null) claims.Add(new("user_metadata", userMetadata));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "synthetic-access"));
    }

    [Fact]
    public void Creator_interest_role_labels_in_profile_or_server_metadata_do_not_mint_account_privileges()
    {
        var identity = Identity();
        // Even preexisting untrusted role text is discarded on verified access.
        identity.AddClaim(new Claim("roles", "bliss.admin"));
        SupabaseOidc.ApplyVerifiedAccessToken(identity,
            Access(
                appMetadata: """{"bliss_roles":["bliss.creator","creator","bliss.creator_verified"]}""",
                userMetadata: """{"bliss_roles":["bliss.admin"],"kind":"creator","intake_status":"NOT_YET_VERIFIED"}"""),
            Options());
        var principal = new ClaimsPrincipal(identity);

        Assert.Empty(identity.FindAll("roles"));
        Assert.False(BlissAuthorization.CanWrite(principal, Options()));
        Assert.False(BlissAuthorization.CanReview(principal, Options()));
    }

    [Fact]
    public void Creator_interest_record_cannot_bypass_approved_identity_subject_gate()
    {
        var identity = Identity(Unapproved);
        Assert.Throws<SecurityTokenValidationException>(() =>
            SupabaseOidc.ApplyVerifiedAccessToken(identity,
                Access(sub: Unapproved,
                    appMetadata: """{"bliss_roles":["bliss.creator","bliss.operator","bliss.admin"]}"""),
                Options()));
    }

    [Fact]
    public void Recognized_read_only_role_does_not_become_creator_admin_or_reviewer()
    {
        var identity = Identity();
        SupabaseOidc.ApplyVerifiedAccessToken(identity,
            Access(appMetadata: """{"bliss_roles":["bliss.viewer","creator"]}"""),
            Options());
        var principal = new ClaimsPrincipal(identity);

        Assert.Equal("bliss.viewer", Assert.Single(identity.FindAll("roles")).Value);
        Assert.False(BlissAuthorization.CanWrite(principal, Options()));
        Assert.False(BlissAuthorization.CanReview(principal, Options()));
    }
}
