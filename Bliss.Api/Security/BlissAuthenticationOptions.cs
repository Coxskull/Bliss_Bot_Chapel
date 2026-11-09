namespace Bliss.Api.Security;

public sealed class BlissAuthenticationOptions
{
    public const string SectionName = "Authentication";
    public const string AdvertiserSelfServiceMarkerClaimType = "bliss_advertiser_self_service";

    public bool Enabled { get; set; }
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public bool SupabaseEnabled { get; set; }
    public string SupabasePublishableKey { get; set; } = string.Empty;
    // Explicit issuer subjects, never emails or user-editable metadata.
    // Empty means no one can complete initial test login.
    public string[] ApprovedTestSubjects { get; set; } = [];
    public string NameClaimType { get; set; } = "name";
    public string RoleClaimType { get; set; } = "roles";
    public string ViewerRole { get; set; } = "bliss.viewer";
    public string OperatorRole { get; set; } = "bliss.operator";
    public string ReviewerRole { get; set; } = "bliss.reviewer";
    public string AdminRole { get; set; } = "bliss.admin";
    public string AdvertiserRole { get; set; } = "bliss.advertiser";
    // Task #4: creator accounts are disabled until approved identity and
    // recovery/ingress release gates are separately accepted. Never infer
    // these values from a public interest record or CRM contact.
    public bool CreatorSelfServiceRequested { get; set; }
    public string CreatorRole { get; set; } = "bliss.creator";
    public string CreatorIdClaimType { get; set; } = "bliss_creator_id";
    // Explicit approved OIDC subject IDs, not editable email or form IDs.
    // Keep disjoint from ApprovedTestSubjects; empty means no creator login.
    public string[] ApprovedCreatorSubjects { get; set; } = [];

    // Advertiser-only self-service stays off until identity-to-business
    // verification and hosted recovery/ingress checks are accepted.
    public bool AdvertiserSelfServiceRequested { get; set; }
    public string[] ApprovedAdvertiserSubjects { get; set; } = [];
    // Verified Supabase identities with no trusted roles may enter only the
    // registration endpoint while this is explicitly enabled. It is OFF by default.
    public bool AdvertiserRegistrationRequested { get; set; }
    public const string AdvertiserRegistrationPendingMarkerClaimType = "bliss_advertiser_registration_pending";
    // Creator registration is a separate, no-role Supabase identity lane.
    // Like advertiser registration, it creates no verified creator authority.
    public bool CreatorRegistrationRequested { get; set; }
    public const string CreatorRegistrationPendingMarkerClaimType = "bliss_creator_registration_pending";

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
