namespace Bliss.Domain.Demonstrations;

public sealed record ContactRoadAssessment(
    bool Accepted,
    string Kind,
    string Value,
    string SourceUrl,
    string State,
    bool OutreachEligible,
    IReadOnlyList<string> Reasons);

public static class ContactRoads
{
    public const string NotPermission = "A public road is not permission to send.";

    public static readonly string[] Kinds =
        ["named_business_email", "company_marketing", "general_company", "published_messaging"];

    public static ContactRoadAssessment Accept(string kind, string value, string sourceUrl, bool prospectSuppressed)
    {
        var reasons = new List<string>();
        var normalized = (kind ?? string.Empty).Trim().ToLowerInvariant();
        var copied = (value ?? string.Empty).Trim();
        var source = (sourceUrl ?? string.Empty).Trim();
        if (!Kinds.Contains(normalized, StringComparer.Ordinal))
        {
            reasons.Add("The contact type is not a legitimate business route.");
        }

        if (copied.Length < 3)
        {
            reasons.Add("A contact value copied from the public page is required. Alpha will not invent one.");
        }

        if (!OpportunityScreen.IsPublicSource(source))
        {
            reasons.Add("A public contact needs its own http or https source URL. Alpha will not store a road without one.");
        }

        if (reasons.Count > 0)
        {
            return new ContactRoadAssessment(false, string.Empty, string.Empty, string.Empty, string.Empty, false, reasons);
        }

        return new ContactRoadAssessment(
            true,
            normalized,
            copied,
            source,
            prospectSuppressed ? "SUPPRESSED" : "DISCOVERED",
            false,
            [NotPermission]);
    }

    public static string? SuppressionError(string reason)
    {
        if ((reason ?? string.Empty).Trim().Length < 3)
        {
            return "A suppression reason is required. Alpha will not invent one.";
        }

        return null;
    }
}
