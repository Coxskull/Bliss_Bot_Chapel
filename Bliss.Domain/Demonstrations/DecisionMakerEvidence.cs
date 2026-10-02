namespace Bliss.Domain.Demonstrations;

public sealed record DecisionMakerInput(
    string? PersonName,
    string? Role,
    string? EvidenceKind,
    string? EvidenceUrl,
    string? CorroboratingKind,
    string? CorroboratingUrl,
    string? ContactKind,
    string? ContactValue,
    string? ContactSourceUrl,
    DateTime VerifiedAt,
    DateTime AsOf);

public sealed record DecisionMakerAssessment(
    bool Accepted,
    string Confidence,
    string Freshness,
    bool PersonalizationAllowed,
    string ContactTier,
    string ContactRoute,
    string PersonName,
    string Role,
    string EvidenceKind,
    string EvidenceSourceUrl,
    string CorroboratingKind,
    string CorroboratingSourceUrl,
    string ContactType,
    string ContactValue,
    string ContactSourceUrl,
    string ContactVerification,
    IReadOnlyList<string> Reasons);

public static class DecisionMakerEvidence
{
    public const int FreshnessDays = 90;

    private static readonly string[] Official =
        ["official_website", "team_page", "press_release", "company_announcement"];

    private static readonly string[] Professional = ["professional_profile"];

    private static readonly string[] Weak = ["directory", "business_news", "company_contact_page"];

    private static readonly string[] Contacts =
        ["named_business_email", "company_marketing", "general_company", "published_messaging"];

    public static DecisionMakerAssessment Evaluate(string niche, DecisionMakerInput input)
    {
        var reasons = new List<string>();
        var name = (input.PersonName ?? string.Empty).Trim();
        var role = (input.Role ?? string.Empty).Trim();
        var evidenceKind = Normalize(input.EvidenceKind);
        var evidenceUrl = (input.EvidenceUrl ?? string.Empty).Trim();
        var corroboratingKind = Normalize(input.CorroboratingKind);
        var corroboratingUrl = (input.CorroboratingUrl ?? string.Empty).Trim();
        var contactKind = Normalize(input.ContactKind);
        if (contactKind.Length == 0)
        {
            contactKind = "none";
        }

        var contactValue = (input.ContactValue ?? string.Empty).Trim();
        var contactSource = (input.ContactSourceUrl ?? string.Empty).Trim();
        var hasName = name.Length > 0;
        if (hasName && name.Length < 2)
        {
            reasons.Add("A decision-maker name from a public source must be at least two characters. Alpha will not invent one.");
        }

        var knownRole = !hasName || BuyingRoleCatalog.RolesFor(niche)
            .Contains(role, StringComparer.OrdinalIgnoreCase);
        if (hasName && !knownRole)
        {
            reasons.Add("The role must be one of the configured buying roles. Alpha will not invent a title.");
        }

        if (hasName && !OpportunityScreen.IsPublicSource(evidenceUrl))
        {
            reasons.Add("A person name without a public http or https evidence URL is not evidence. Alpha will not store it.");
        }
        else if (hasName && !IsKnownEvidence(evidenceKind))
        {
            reasons.Add("The evidence kind is not in the public-source catalog.");
        }

        var wantsContact = contactKind != "none" || contactValue.Length > 0 || contactSource.Length > 0;
        if (wantsContact)
        {
            if (!Contacts.Contains(contactKind, StringComparer.Ordinal))
            {
                reasons.Add("The contact type is not a legitimate business route.");
            }

            if (contactValue.Length < 3)
            {
                reasons.Add("A contact value copied from the public page is required. Alpha will not invent one.");
            }

            if (!OpportunityScreen.IsPublicSource(contactSource))
            {
                reasons.Add("A public contact needs its own http or https source URL.");
            }
        }

        if (!hasName && !wantsContact)
        {
            reasons.Add("Record a public person or a public contact. Alpha will not invent either.");
        }

        if (reasons.Count > 0)
        {
            return Rejected(reasons);
        }

        var strong = IsStrong(evidenceKind) && OpportunityScreen.IsPublicSource(evidenceUrl);
        var corroborating = IsStrong(corroboratingKind)
            && OpportunityScreen.IsPublicSource(corroboratingUrl)
            && !SameUrl(evidenceUrl, corroboratingUrl);
        var weak = !strong && IsWeak(evidenceKind) && OpportunityScreen.IsPublicSource(evidenceUrl);
        var direct = contactKind == "named_business_email";
        var usefulContact = wantsContact;

        string confidence;
        if (!hasName)
        {
            confidence = "UNVERIFIED";
        }
        else if (strong && corroborating && direct)
        {
            confidence = "HIGH";
        }
        else if (strong)
        {
            confidence = "MEDIUM";
        }
        else if (weak)
        {
            confidence = "LOW";
        }
        else
        {
            return Rejected(["The public source does not support a confidence classification."]);
        }

        var freshness = FreshnessOf(input.VerifiedAt, input.AsOf);
        var named = confidence is "HIGH" or "MEDIUM";
        var personalization = named && freshness == "CURRENT";
        var (tier, route, verification) = Route(confidence, contactKind, usefulContact);
        return new DecisionMakerAssessment(
            true,
            confidence,
            freshness,
            personalization,
            tier,
            route,
            hasName ? name : string.Empty,
            hasName ? role : string.Empty,
            hasName ? evidenceKind : string.Empty,
            hasName ? evidenceUrl : string.Empty,
            corroborating ? corroboratingKind : string.Empty,
            corroborating ? corroboratingUrl : string.Empty,
            usefulContact ? contactKind : string.Empty,
            usefulContact ? contactValue : string.Empty,
            usefulContact ? contactSource : string.Empty,
            verification,
            []);
    }

    public static (string Freshness, bool PersonalizationAllowed) Present(
        string confidence,
        string personName,
        DateTime? lastVerifiedAt,
        DateTime asOf)
    {
        if (lastVerifiedAt is not DateTime verifiedAt)
        {
            return ("UNRECORDED", false);
        }

        var freshness = FreshnessOf(verifiedAt, asOf);
        var allowed = confidence is "HIGH" or "MEDIUM"
            && freshness == "CURRENT"
            && !string.IsNullOrWhiteSpace(personName);
        return (freshness, allowed);
    }

    private static (string Tier, string Route, string Verification) Route(
        string confidence,
        string contactKind,
        bool usefulContact)
    {
        var named = confidence is "HIGH" or "MEDIUM";
        if (named && usefulContact && contactKind == "named_business_email")
        {
            return ("TIER_1",
                "Named decision-maker and a direct business contact are recorded from public sources. Outreach delivery is not authorized.",
                "PUBLIC_SOURCE_RECORDED");
        }

        if (named && usefulContact)
        {
            return ("TIER_2",
                "Named decision-maker and a general company contact are recorded from public sources. This is not a direct personal mailbox. Outreach delivery is not authorized.",
                "PUBLIC_SOURCE_RECORDED");
        }

        if (!named && usefulContact && contactKind is "company_marketing" or "published_messaging" or "named_business_email")
        {
            return ("TIER_3",
                "No verified named person. A marketing or business-development contact is recorded from a public source. Outreach delivery is not authorized.",
                "PUBLIC_SOURCE_RECORDED");
        }

        if (!named && usefulContact)
        {
            return ("TIER_4",
                "Verified general business contact only. No named decision-maker is verified. Outreach delivery is not authorized.",
                "PUBLIC_SOURCE_RECORDED");
        }

        return ("UNROUTED",
            "No verified business contact is on file. Outreach delivery is not authorized.",
            "UNVERIFIED");
    }

    private static string FreshnessOf(DateTime verifiedAt, DateTime asOf) =>
        asOf - verifiedAt > TimeSpan.FromDays(FreshnessDays) ? "STALE" : "CURRENT";

    private static bool IsStrong(string kind) =>
        Official.Contains(kind, StringComparer.Ordinal) || Professional.Contains(kind, StringComparer.Ordinal);

    private static bool IsWeak(string kind) => Weak.Contains(kind, StringComparer.Ordinal);

    private static bool IsKnownEvidence(string kind) => IsStrong(kind) || IsWeak(kind);

    private static bool SameUrl(string left, string right) =>
        string.Equals(left.TrimEnd('/'), right.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static DecisionMakerAssessment Rejected(IReadOnlyList<string> reasons) =>
        new(false, "UNVERIFIED", "UNRECORDED", false, "TIER_4",
            "No verified named person and no verified company mailbox are on file. Outreach delivery is not authorized.",
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, "UNVERIFIED", reasons);
}
