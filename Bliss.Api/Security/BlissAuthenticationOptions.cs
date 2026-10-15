namespace Bliss.Api.Security;

public sealed class BlissAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public bool Enabled { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string NameClaimType { get; set; } = "name";
    public string RoleClaimType { get; set; } = "roles";
    public string ViewerRole { get; set; } = "bliss.viewer";
    public string OperatorRole { get; set; } = "bliss.operator";
    public string ReviewerRole { get; set; } = "bliss.reviewer";
    public string AdminRole { get; set; } = "bliss.admin";
    public string AdvertiserRole { get; set; } = "bliss.advertiser";
    public string AdvertiserIdClaimType { get; set; } = "advertiser_id";
    // A reviewer needs an issuer-assigned workspace scope. Each claim value
    // must be one workspace UUID; multiple assignments use repeated claims.
    public string ReviewerWorkspaceClaimType { get; set; } = "bliss_review_workspace_id";
}
