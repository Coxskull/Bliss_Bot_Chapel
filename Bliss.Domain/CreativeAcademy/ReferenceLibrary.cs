using System.Globalization;

namespace Bliss.Domain.CreativeAcademy;

public sealed record AcademyReferenceRecord(
    string ReferenceId,
    int NicheNumber,
    string NicheKey,
    string NicheName,
    string ExpectedFile,
    string Lifecycle,
    string UploadStatus,
    bool AssetPresent,
    string Learn,
    string DoNotCopy,
    string MappingNote = "",
    string QualityStatus = "UNCLASSIFIED");

public sealed record RetrievedReference(
    string ReferenceId,
    string NicheKey,
    string Reason,
    string Learn = "",
    string DoNotCopy = "");

public sealed record ReferenceRetrieval(
    string Status,
    string Notice,
    IReadOnlyList<RetrievedReference> Selected,
    int ModelCalls,
    bool CampaignReady,
    string Delivery);

public sealed record QualityAttribute(string Name, string Grade);

public sealed record OriginalityResult(
    string Status,
    string Notice,
    bool CampaignReady,
    string Delivery);

public sealed record StoredRejection(
    string Code,
    string SubjectId,
    string Notice,
    bool PositiveReference,
    int ModelCalls,
    bool CampaignReady,
    string Delivery);

public sealed record RegressionCase(
    string BriefId,
    string Brief,
    string BaselineAsset,
    string LatestRun,
    bool Passed);

public sealed record RegressionSuiteResult(
    string Status,
    bool Passed,
    IReadOnlyList<RegressionCase> Cases,
    string Notice);

public sealed record AssetProvenance(
    string ReferenceId,
    string Source,
    string GenerationProvider,
    string Ownership,
    string ApprovalHistory,
    string PermittedInternalUse,
    string PermittedProviderUse,
    string Restrictions,
    string Lifecycle,
    string Dates,
    string ApprovingAuthority);

public sealed record StoredQualityReading(
    string ReferenceId,
    string Status,
    IReadOnlyList<QualityAttribute> Attributes,
    int ModelCalls,
    string Notice);

public sealed record SimilarityResult(
    string Status,
    string Notice,
    string MatchedField,
    string MatchedReferenceId,
    int ModelCalls,
    bool CampaignReady,
    string Delivery);

/// <summary>
/// The 50 prototypes are quality references. Retrieval uses ACTIVE records only.
/// A missing file stays awaiting upload. The library does not invent an image.
/// </summary>
public static class ReferenceLibrary
{
    public const string AwaitingUpload = "AWAITING_UPLOAD";
    public const string Uploaded = "UPLOADED";
    public const string Candidate = "CANDIDATE";
    public const string HumanReview = "HUMAN_REVIEW";
    public const string AlphaApproved = "ALPHA_APPROVED";
    public const string Active = "ACTIVE";
    public const string Superseded = "SUPERSEDED";
    public const string Retired = "RETIRED";
    public const string Unclassified = "UNCLASSIFIED";
    public const string QualityDnaVersion = "GQD-1";

    public static IReadOnlyList<string> Lifecycles { get; } =
    [
        Candidate,
        HumanReview,
        AlphaApproved,
        Active,
        Superseded,
        Retired
    ];

    public static IReadOnlyList<string> QualityGrades { get; } =
    [
        Unclassified,
        "REFERENCE_STRENGTH",
        "STRONG",
        "SUPPORTING",
        "NOT_APPLICABLE"
    ];
    public const string UnassignedReason = "Stored in the inbox and not forced into a niche. Not an ACTIVE reference.";

    public static IReadOnlyList<string> QualityDna { get; } = CreativeAcademy.ReferenceParityGate;

    public static IReadOnlyList<QualityAttribute> UnclassifiedAttributes() =>
        QualityDna.Select(name => new QualityAttribute(name, Unclassified)).ToArray();

    public static IReadOnlyList<string> DoNotProduce { get; } =
    [
        "FLAT_LIGHTING",
        "MUDDY_COLORS",
        "WEAK_CONTRAST",
        "GENERIC_AI_LOOK",
        "UNREALISTIC_SKIN",
        "ANATOMY_FAILURE",
        "PRODUCT_REALISM_FAILURE",
        "MATERIAL_REALISM_FAILURE",
        "POOR_HIERARCHY",
        "UNREADABLE_TYPOGRAPHY",
        "OVERLOADED_COMPOSITION",
        "WEAK_SCREEN_IMPACT",
        "BRAND_DNA_VIOLATION",
        "REFERENCE_TOO_SIMILAR",
        "INVENTORY_GEOMETRY_FAILURE",
        "QR_FAILURE"
    ];

    public static IReadOnlyList<string> RegressionBriefs { get; } =
    [
        "RQ-01 pharmacy Panama burgundy",
        "RQ-02 motorcycle Spanish showroom",
        "RQ-03 coffee Medellin morning",
        "RQ-04 law Santo Domingo consultation",
        "RQ-05 fitness Manila dawn",
        "RQ-06 bakery Kuala Lumpur weekend",
        "RQ-07 dental Jakarta family",
        "RQ-08 auto parts Panama counter",
        "RQ-09 bottled water Colombia",
        "RQ-10 beauty salon Dominican Republic",
        "RQ-11 hardware store Indonesia",
        "RQ-12 household cleaner Malaysia"
    ];

    public static IReadOnlyList<AcademyReferenceRecord> ParseManifest(string? tsv, Func<string, bool>? assetExists = null)
    {
        var exists = assetExists ?? (_ => false);
        var rows = new List<AcademyReferenceRecord>();
        if (string.IsNullOrWhiteSpace(tsv))
        {
            return rows;
        }

        foreach (var line in tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("referenceId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length < 7 || !int.TryParse(cells[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                continue;
            }

            var referenceId = cells[0].Trim();
            var expected = cells.Length > 4 ? cells[4].Trim() : referenceId + ".png";
            var present = exists(expected) || exists(referenceId);
            var upload = present ? Uploaded : (cells.Length > 6 ? cells[6].Trim() : AwaitingUpload);
            if (!present && string.IsNullOrWhiteSpace(upload))
            {
                upload = AwaitingUpload;
            }

            if (!present)
            {
                upload = AwaitingUpload;
            }

            rows.Add(new AcademyReferenceRecord(
                referenceId,
                number,
                cells[2].Trim(),
                cells[3].Trim(),
                expected,
                cells.Length > 5 && !string.IsNullOrWhiteSpace(cells[5]) ? cells[5].Trim() : Candidate,
                upload,
                present,
                cells.Length > 7 ? cells[7].Trim() : string.Empty,
                cells.Length > 8 ? cells[8].Trim() : string.Empty,
                cells.Length > 9 ? cells[9].Trim() : string.Empty));
        }

        return rows;
    }

    public static ReferenceRetrieval Select(
        string? nicheKey,
        IReadOnlyList<AcademyReferenceRecord> library,
        IReadOnlyList<string>? extraReasons = null)
    {
        var niche = (nicheKey ?? string.Empty).Trim().ToLowerInvariant();
        var active = library.Where(item => item.Lifecycle == Active && item.AssetPresent).ToList();
        var selected = new List<RetrievedReference>();
        var nicheHit = active.FirstOrDefault(item => item.NicheKey.Equals(niche, StringComparison.OrdinalIgnoreCase));
        if (nicheHit is not null)
        {
            selected.Add(new RetrievedReference(nicheHit.ReferenceId, nicheHit.NicheKey, "niche match", nicheHit.Learn, nicheHit.DoNotCopy));
        }

        foreach (var reason in extraReasons ?? [])
        {
            if (selected.Count >= 5)
            {
                break;
            }

            var match = active.FirstOrDefault(item =>
                selected.All(chosen => chosen.ReferenceId != item.ReferenceId)
                && (item.Learn.Contains(reason, StringComparison.OrdinalIgnoreCase)
                    || item.NicheName.Contains(reason, StringComparison.OrdinalIgnoreCase)));
            if (match is not null)
            {
                selected.Add(new RetrievedReference(match.ReferenceId, match.NicheKey, reason, match.Learn, match.DoNotCopy));
            }
        }

        if (selected.Count == 0)
        {
            return new ReferenceRetrieval(
                "NICHE_REFERENCE_NOT_ACTIVE",
                "No ACTIVE Academy reference with a stored file matches this brief. Awaiting upload is not retrieval. No image was invented. Delivery remains NOT_SENT.",
                selected,
                0,
                false,
                "NOT_SENT");
        }

        return new ReferenceRetrieval(
            "RETRIEVED",
            "Retrieved " + selected.Count.ToString(CultureInfo.InvariantCulture)
            + " ACTIVE references. The full library was not attached. Each selection has a stored reason. Delivery remains NOT_SENT.",
            selected,
            0,
            false,
            "NOT_SENT");
    }

    public static OriginalityResult Judge(string? brandName, string? headline, IReadOnlyList<AcademyReferenceRecord> library)
    {
        var brand = (brandName ?? string.Empty).Trim();
        var line = (headline ?? string.Empty).Trim();
        foreach (var reference in library)
        {
            var tokens = reference.DoNotCopy
                .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                if (token.Length < 3)
                {
                    continue;
                }

                if (brand.Length > 0 && brand.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(reference.ReferenceId, "brand");
                }

                if (line.Length > 0 && line.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return Fail(reference.ReferenceId, "headline");
                }
            }
        }

        return new OriginalityResult(
            "PASS",
            "No stored do-not-copy identity matched this brand or headline. This is not a visual score and not campaign ready.",
            false,
            "NOT_SENT");
    }

    public static SimilarityResult Compare(
        string? brandName,
        string? headline,
        string? face,
        string? product,
        IReadOnlyList<AcademyReferenceRecord> library)
    {
        ArgumentNullException.ThrowIfNull(library);
        var fields = new[]
        {
            ("brand", (brandName ?? string.Empty).Trim()),
            ("headline", (headline ?? string.Empty).Trim()),
            ("face", (face ?? string.Empty).Trim()),
            ("product", (product ?? string.Empty).Trim())
        };

        if (fields.All(field => field.Item2.Length == 0))
        {
            return new SimilarityResult(
                "HUMAN REVIEW",
                "No brand, headline, face, or product was supplied. A human reviews the creative. This is not a visual score and not a pass. Delivery remains NOT_SENT.",
                "",
                "",
                0,
                false,
                "NOT_SENT");
        }

        foreach (var field in fields)
        {
            if (field.Item2.Length > 0 && CreativeAcademy.NamesReferenceIdentity(field.Item2))
            {
                return Regenerate(field.Item1, "");
            }
        }

        foreach (var reference in library)
        {
            foreach (var token in Tokens(reference.DoNotCopy))
            {
                foreach (var field in fields)
                {
                    if (field.Item2.Length > 0 && field.Item2.Contains(token, StringComparison.OrdinalIgnoreCase))
                    {
                        return Regenerate(field.Item1, reference.ReferenceId);
                    }
                }
            }
        }

        if (fields.Any(field => CreativeAcademy.RequestsPrototypePlacement(field.Item2)))
        {
            return new SimilarityResult(
                "RECOMPOSE",
                "The creative asks to scale the Academy prototype into the placement. Recompose it into the purchased product's slot geometry. Prototype dimensions are not podcast-advertising dimensions. Delivery remains NOT_SENT.",
                "",
                "",
                0,
                false,
                "NOT_SENT");
        }

        if (!library.Any(reference => Tokens(reference.DoNotCopy).Any()))
        {
            return new SimilarityResult(
                "REVIEW REQUIRED",
                "No stored do-not-copy note is on file. This is not a visual score. Prior Alpha demos and other advertiser creatives are not stored, so they were not compared. Campaign ready is false. Delivery remains NOT_SENT.",
                "",
                "",
                0,
                false,
                "NOT_SENT");
        }

        return new SimilarityResult(
            "PASS",
            "Stored do-not-copy notes did not match this brand, headline, face, or product. This is not a visual score and not campaign ready. Delivery remains NOT_SENT.",
            "",
            "",
            0,
            false,
            "NOT_SENT");
    }

    private static SimilarityResult Regenerate(string field, string referenceId) =>
        new(
            "REGENERATE",
            "The " + field + " reproduces a reference identity. The Academy reference is not a template."
            + (referenceId.Length == 0 ? "" : " Matched " + referenceId + ".")
            + " Delivery remains NOT_SENT.",
            field,
            referenceId,
            0,
            false,
            "NOT_SENT");

    private static IEnumerable<string> Tokens(string notes) =>
        notes.Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 3);

    public static StoredRejection Reject(string? code, string? subjectId)
    {
        var rejection = (code ?? string.Empty).Trim();
        var subject = (subjectId ?? string.Empty).Trim();
        if (!DoNotProduce.Contains(rejection, StringComparer.Ordinal) || subject.Length == 0)
        {
            throw new InvalidOperationException("A stored rejection code and subject are required. None was invented.");
        }

        return new StoredRejection(
            rejection,
            subject,
            "Rejected work is stored for QA. It is not a positive Academy reference. Delivery remains NOT_SENT.",
            false,
            0,
            false,
            "NOT_SENT");
    }

    public static RegressionSuiteResult ReadRegression(IReadOnlyDictionary<string, string>? baselines)
    {
        var stored = baselines ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var cases = RegressionBriefs.Select(brief =>
        {
            var id = brief.Split(' ', 2)[0];
            var baseline = stored.TryGetValue(id, out var asset) ? asset.Trim() : string.Empty;
            var recorded = baseline.Length > 0;
            return new RegressionCase(
                id,
                brief,
                recorded ? baseline : "BASELINE_NOT_RECORDED",
                recorded ? "NOT_COMPARED" : "BASELINE_NOT_RECORDED",
                false);
        }).ToList();
        return new RegressionSuiteResult(
            "BASELINE_NOT_RECORDED",
            false,
            cases,
            "The suite names its briefs. No accepted baseline asset is stored, so the latest run is not passed. An API response is not a quality comparison. Delivery remains NOT_SENT.");
    }

    public static AssetProvenance ReadProvenance(AcademyReferenceRecord reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new AssetProvenance(
            reference.ReferenceId,
            "UNRECORDED",
            "UNRECORDED",
            "UNRECORDED",
            "UNRECORDED",
            "UNRECORDED",
            "NOT_AUTHORIZED",
            "UNRECORDED",
            reference.Lifecycle,
            "UNRECORDED",
            "UNRECORDED");
    }

    public static bool MaySendToProvider(AssetProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        return provenance.PermittedProviderUse == "ALLOWED" && provenance.Restrictions == "NONE";
    }

    public static StoredQualityReading ReadStoredQuality(AcademyReferenceRecord reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var stored = !string.IsNullOrWhiteSpace(reference.QualityStatus)
            && reference.QualityStatus != Unclassified;
        return new StoredQualityReading(
            reference.ReferenceId,
            stored ? "STORED" : Unclassified,
            UnclassifiedAttributes(),
            0,
            stored
                ? "Stored quality status " + reference.QualityStatus + " was read. A model was not called to re-analyze it. Per-attribute grades were not invented."
                : "Stored quality metadata is UNCLASSIFIED. A model was not called to re-analyze it. Grades were not invented.");
    }

    private static OriginalityResult Fail(string referenceId, string field) =>
        new(
            "REFERENCE_TOO_SIMILAR",
            "The " + field + " repeats a do-not-copy element on " + referenceId + ". The Academy reference is not a template. Delivery remains NOT_SENT.",
            false,
            "NOT_SENT");
}
