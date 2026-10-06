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
    string DoNotCopy);

public sealed record RetrievedReference(
    string ReferenceId,
    string NicheKey,
    string Reason);

public sealed record ReferenceRetrieval(
    string Status,
    string Notice,
    IReadOnlyList<RetrievedReference> Selected,
    int ModelCalls,
    bool CampaignReady,
    string Delivery);

public sealed record OriginalityResult(
    string Status,
    string Notice,
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
    public const string Active = "ACTIVE";
    public const string QualityDnaVersion = "GQD-1";

    public static IReadOnlyList<string> QualityDna { get; } = CreativeAcademy.ReferenceParityGate;

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
                cells.Length > 8 ? cells[8].Trim() : string.Empty));
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
            selected.Add(new RetrievedReference(nicheHit.ReferenceId, nicheHit.NicheKey, "niche match"));
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
                selected.Add(new RetrievedReference(match.ReferenceId, match.NicheKey, reason));
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

    private static OriginalityResult Fail(string referenceId, string field) =>
        new(
            "REFERENCE_TOO_SIMILAR",
            "The " + field + " repeats a do-not-copy element on " + referenceId + ". The Academy reference is not a template. Delivery remains NOT_SENT.",
            false,
            "NOT_SENT");
}
