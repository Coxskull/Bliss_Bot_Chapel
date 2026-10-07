using System.Text.Json;

namespace Bliss.Domain.CreativeAcademy;

public sealed record HarborlightReviewCheck(string Name, string Status);

/// <summary>
/// The HV-4 sheet for one stored attempt. Preparing it does not classify an attribute or accept the creative.
/// </summary>
public sealed record HarborlightReviewSheet(
    string AttemptId,
    string Status,
    string QualityDnaVersion,
    string OriginalSha256,
    string AdaptedSha256,
    IReadOnlyList<QualityAttribute> Attributes,
    IReadOnlyList<HarborlightReviewCheck> CopyChecks,
    IReadOnlyList<HarborlightReviewCheck> Questions,
    string HumanReview,
    bool CampaignReady,
    string Delivery,
    string Regression,
    string Notice);

public static class HarborlightReview
{
    public const string Prepared = "PREPARED";
    public const string NotReviewed = "NOT_REVIEWED";
    public const string Unrecorded = "UNRECORDED";
    public const string NotRequested = "NOT_REQUESTED";

    public static IReadOnlyList<string> CopyCheckNames { get; } =
    [
        "company",
        "identity",
        "person",
        "product",
        "photograph",
        "wording",
        "background",
        "composition",
        "distinctive identity"
    ];

    public static IReadOnlyList<string> QuestionNames { get; } =
    [
        "Panama-market authenticity",
        "adapted layout"
    ];

    public static HarborlightReviewSheet Prepare(string attemptId, string originalSha256, string adaptedSha256)
    {
        var attempt = RequireToken(attemptId, "attempt");
        return RequirePrepared(new HarborlightReviewSheet(
            attempt,
            Prepared,
            ReferenceLibrary.QualityDnaVersion,
            RequireHash(originalSha256, "original"),
            RequireHash(adaptedSha256, "adapted preview"),
            ReferenceLibrary.UnclassifiedAttributes(),
            CopyCheckNames.Select(name => new HarborlightReviewCheck(name, NotReviewed)).ToArray(),
            QuestionNames.Select(name => new HarborlightReviewCheck(name, Unrecorded)).ToArray(),
            NotRequested,
            false,
            "NOT_SENT",
            "BASELINE_NOT_RECORDED",
            "The GQD-1 attribute list is prepared for owner review. Every grade is UNCLASSIFIED. No copy judgment, market judgment, layout acceptance, or regression baseline was written."));
    }

    public static HarborlightReviewSheet ParseEvidence(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("reviewSheet", out var sheet))
        {
            throw new InvalidOperationException("The Harborlight review sheet is not stored. None was invented.");
        }

        var parsed = new HarborlightReviewSheet(
            ReadString(sheet, "attemptId"),
            ReadString(sheet, "status"),
            ReadString(sheet, "qualityDnaVersion"),
            ReadString(sheet, "originalSha256"),
            ReadString(sheet, "adaptedSha256"),
            ReadAttributes(sheet),
            ReadChecks(sheet, "copyChecks"),
            ReadChecks(sheet, "questions"),
            ReadString(sheet, "humanReview"),
            sheet.TryGetProperty("campaignReady", out var ready) && ready.ValueKind == JsonValueKind.False
                ? false
                : throw new InvalidOperationException("Campaign ready was not left false. Acceptance was not invented."),
            ReadString(sheet, "delivery"),
            ReadString(sheet, "regression"),
            ReadString(sheet, "notice"));
        var prepared = RequirePrepared(parsed);
        if (!string.Equals(prepared.AttemptId, ReadString(root, "attemptId"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The review sheet does not name this attempt. None was substituted.");
        }

        if (!string.Equals(prepared.OriginalSha256, ReadString(root.GetProperty("original"), "sha256"), StringComparison.Ordinal)
            || !string.Equals(prepared.AdaptedSha256, ReadString(root.GetProperty("adaptation"), "sha256"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The review sheet does not name the stored images. None was substituted.");
        }

        if (!string.Equals(ReadString(root.GetProperty("checks"), "humanReview"), NotRequested, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Human review was not requested. A decision was not invented.");
        }

        return prepared;
    }

    public static HarborlightReviewSheet RequirePrepared(HarborlightReviewSheet sheet)
    {
        if (!string.Equals(sheet.Status, Prepared, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The review sheet is not prepared. A decision was not invented.");
        }

        if (!string.Equals(sheet.QualityDnaVersion, ReferenceLibrary.QualityDnaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The review sheet does not use " + ReferenceLibrary.QualityDnaVersion + ".");
        }

        if (!string.Equals(sheet.HumanReview, NotRequested, StringComparison.Ordinal)
            || sheet.CampaignReady
            || !string.Equals(sheet.Delivery, "NOT_SENT", StringComparison.Ordinal)
            || !string.Equals(sheet.Regression, "BASELINE_NOT_RECORDED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The review sheet claimed acceptance. None was recorded.");
        }

        RequireHash(sheet.OriginalSha256, "original");
        RequireHash(sheet.AdaptedSha256, "adapted preview");
        RequireList(
            sheet.Attributes.Select(item => (item.Name, item.Grade)).ToArray(),
            ReferenceLibrary.QualityDna,
            ReferenceLibrary.Unclassified,
            "grade");
        RequireList(
            sheet.CopyChecks.Select(item => (item.Name, item.Status)).ToArray(),
            CopyCheckNames,
            NotReviewed,
            "status");
        RequireList(
            sheet.Questions.Select(item => (item.Name, item.Status)).ToArray(),
            QuestionNames,
            Unrecorded,
            "status");
        return sheet;
    }

    private static void RequireList(
        IReadOnlyList<(string Name, string Value)> actual,
        IReadOnlyList<string> expectedNames,
        string expectedValue,
        string valueLabel)
    {
        if (actual.Count != expectedNames.Count
            || actual.Where((item, index) =>
                !string.Equals(item.Name, expectedNames[index], StringComparison.Ordinal)
                || !string.Equals(item.Value, expectedValue, StringComparison.Ordinal)).Any())
        {
            throw new InvalidOperationException(
                "A review " + valueLabel + " was changed. Unreviewed items stay " + expectedValue + ".");
        }
    }

    private static IReadOnlyList<QualityAttribute> ReadAttributes(JsonElement sheet)
    {
        if (!sheet.TryGetProperty("attributes", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The review attributes are not stored. None were invented.");
        }

        return items.EnumerateArray()
            .Select(item => new QualityAttribute(ReadString(item, "name"), ReadString(item, "grade")))
            .ToArray();
    }

    private static IReadOnlyList<HarborlightReviewCheck> ReadChecks(JsonElement sheet, string property)
    {
        if (!sheet.TryGetProperty(property, out var items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The review " + property + " are not stored. None were invented.");
        }

        return items.EnumerateArray()
            .Select(item => new HarborlightReviewCheck(ReadString(item, "name"), ReadString(item, "status")))
            .ToArray();
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The review field " + property + " is not stored. None was invented.");
        }

        return value.GetString() ?? string.Empty;
    }

    private static string RequireToken(string? value, string label)
    {
        var token = (value ?? string.Empty).Trim();
        if (token.Length is < 3 or > 40
            || token.Any(character => character is not ((>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-')))
        {
            throw new InvalidOperationException("A Harborlight " + label + " id is required. None was invented.");
        }

        return token;
    }

    private static string RequireHash(string? value, string label)
    {
        var hash = (value ?? string.Empty).Trim();
        if (hash.Length != 64 || hash.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new InvalidOperationException("The " + label + " hash is not stored. None was invented.");
        }

        return hash;
    }
}
