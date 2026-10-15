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
    // Requires explicit issuer configuration; defaults cannot enable visitor
    // claims as trusted evidence of historic conversation ownership.
    public string AskAlphaVisitorRole { get; set; } = "bliss.ask_alpha_visitor";
    public string AskAlphaVisitorSessionClaimType { get; set; } = "ask_alpha_session_id";
    public string AskAlphaVisitorIssuer { get; set; } = string.Empty;
    // Off in every environment unless approved independently. This flag
    // enables only a *prerequisite check*, never a visitor registration route.
    public bool AskAlphaVisitorEnrollmentRequested { get; set; }


    public string AdvertiserIdClaimType { get; set; } = "advertiser_id";
    // A reviewer needs an issuer-assigned workspace scope. Each claim value
    // must be one workspace UUID; multiple assignments use repeated claims.
    public string ReviewerWorkspaceClaimType { get; set; } = "bliss_review_workspace_id";
}
