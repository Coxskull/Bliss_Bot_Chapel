using Bliss.Domain.Demonstrations;

namespace Bliss.Tests.Demonstrations;

public sealed class DecisionMakerEvidenceTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Dental_prospect_identifies_a_likely_buying_role()
    {
        var roles = BuyingRoleCatalog.RolesFor("dental");
        Assert.Contains("Owner/practitioner", roles);
        Assert.Contains("Practice manager", roles);
        Assert.Contains("Marketing manager", roles);
    }

    [Fact]
    public void Unsupported_name_is_rejected_and_stays_unverified()
    {
        var result = DecisionMakerEvidence.Evaluate("restaurant", Input("Ana Ruiz", "Owner", "team_page", ""));
        Assert.False(result.Accepted);
        Assert.Equal("UNVERIFIED", result.Confidence);
        Assert.Equal(string.Empty, result.PersonName);
        Assert.Contains("will not store", string.Join(" ", result.Reasons));
    }

    [Fact]
    public void Official_and_professional_evidence_with_company_contact_is_medium_tier_two()
    {
        var result = DecisionMakerEvidence.Evaluate("restaurant", Input(
            "Ana Ruiz",
            "Owner",
            "team_page",
            "https://example.com/casa-verde/team",
            "professional_profile",
            "https://example.com/casa-verde/profile",
            "company_marketing",
            "marketing@example.com",
            "https://example.com/casa-verde/contact"));

        Assert.True(result.Accepted);
        Assert.Equal("MEDIUM", result.Confidence);
        Assert.Equal("TIER_2", result.ContactTier);
        Assert.Equal("CURRENT", result.Freshness);
        Assert.True(result.PersonalizationAllowed);
        Assert.Contains("not a direct personal mailbox", result.ContactRoute);
        Assert.Contains("not authorized", result.ContactRoute);
    }

    [Fact]
    public void Official_and_professional_evidence_with_direct_contact_is_high()
    {
        var result = DecisionMakerEvidence.Evaluate("dental", Input(
            "Ana Ruiz",
            "Owner/practitioner",
            "official_website",
            "https://example.com/practice",
            "professional_profile",
            "https://example.com/practice/profile",
            "named_business_email",
            "ana@example.com",
            "https://example.com/practice/contact"));

        Assert.Equal("HIGH", result.Confidence);
        Assert.Equal("TIER_1", result.ContactTier);
        Assert.True(result.PersonalizationAllowed);
    }

    [Fact]
    public void Directory_only_stays_low_and_is_not_personalized()
    {
        var result = DecisionMakerEvidence.Evaluate("restaurant", Input(
            "Ana Ruiz", "Owner", "directory", "https://example.com/listing"));
        Assert.Equal("LOW", result.Confidence);
        Assert.False(result.PersonalizationAllowed);
    }

    [Fact]
    public void Marketing_contact_without_a_person_uses_tier_three()
    {
        var result = DecisionMakerEvidence.Evaluate("restaurant", Input(
            "", "", "", "", "", "",
            "company_marketing",
            "marketing@example.com",
            "https://example.com/casa-verde/contact"));
        Assert.Equal("UNVERIFIED", result.Confidence);
        Assert.Equal(string.Empty, result.PersonName);
        Assert.Equal("TIER_3", result.ContactTier);
        Assert.False(result.PersonalizationAllowed);
    }

    [Fact]
    public void Stale_high_confidence_requires_reverification()
    {
        var result = DecisionMakerEvidence.Evaluate("dental", Input(
            "Ana Ruiz",
            "Owner/practitioner",
            "official_website",
            "https://example.com/practice",
            "professional_profile",
            "https://example.com/practice/profile",
            "named_business_email",
            "ana@example.com",
            "https://example.com/practice/contact",
            Now.AddDays(-91)));

        Assert.Equal("HIGH", result.Confidence);
        Assert.Equal("STALE", result.Freshness);
        Assert.False(result.PersonalizationAllowed);
        var presentation = DecisionMakerEvidence.Present("HIGH", "Ana Ruiz", Now.AddDays(-91), Now);
        Assert.Equal("STALE", presentation.Freshness);
        Assert.False(presentation.PersonalizationAllowed);
    }

    [Fact]
    public void Personalized_reply_uses_the_recorded_name_and_stays_unsent()
    {
        var facts = new ProspectFacts(
            "Casa Verde",
            "TIER_2",
            "Named decision-maker and a general company contact are recorded from public sources. Outreach delivery is not authorized.",
            BuyingRoleCatalog.RolesFor("restaurant"),
            ["Table Concept"],
            "Ana Ruiz",
            "Owner",
            "MEDIUM",
            true,
            "CURRENT");
        var turn = DemonstrationConversation.Reply(facts, "Who is the marketing director?");
        Assert.Contains("Ana Ruiz", turn.Reply);
        Assert.Contains("MEDIUM", turn.Reply);
        Assert.Contains("not authorized", turn.Reply);
        Assert.False(turn.HumanEscalation);
    }

    [Fact]
    public void Stale_reply_does_not_use_the_personal_name()
    {
        var facts = new ProspectFacts(
            "Casa Verde", "TIER_2", "route", ["Owner"], ["Table Concept"],
            "Ana Ruiz", "Owner", "HIGH", false, "STALE");
        var turn = DemonstrationConversation.Reply(facts, "Who is the marketing director?");
        Assert.DoesNotContain("Ana Ruiz", turn.Reply);
        Assert.Contains("reverification", turn.Reply);
    }

    private static DecisionMakerInput Input(
        string name,
        string role,
        string evidenceKind,
        string evidenceUrl,
        string corroboratingKind = "",
        string corroboratingUrl = "",
        string contactKind = "none",
        string contactValue = "",
        string contactSource = "",
        DateTime? verifiedAt = null) =>
        new(name, role, evidenceKind, evidenceUrl, corroboratingKind, corroboratingUrl,
            contactKind, contactValue, contactSource, verifiedAt ?? Now, Now);
}
