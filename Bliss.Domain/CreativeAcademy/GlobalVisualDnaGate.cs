namespace Bliss.Domain.CreativeAcademy;

public sealed record GlobalVisualDnaDecision(
    string Status,
    int ReportedScore,
    IReadOnlyList<string> BlockingDefects,
    bool VisualEvidenceRecorded,
    bool BrandDnaCompliant,
    bool OriginalityConfirmed,
    bool InventoryGeometryVerified,
    bool QrVerified,
    bool CampaignReady,
    string Delivery,
    string Notice);

/// <summary>
/// Deterministic release gate for visual QA evidence. A numeric score is
/// informational only; it cannot override a hard failure or missing evidence.
/// </summary>
public static class GlobalVisualDnaGate
{
    public const string Pass = "PASS";
    public const string Revise = "REVISE";
    public const string Fail = "FAIL";
    public const string Withheld = "WITHHELD";

    private static readonly HashSet<string> HardFailures = new(StringComparer.Ordinal)
    {
        "ANATOMY_FAILURE",
        "PRODUCT_REALISM_FAILURE",
        "MATERIAL_REALISM_FAILURE",
        "UNREADABLE_TYPOGRAPHY",
        "BRAND_DNA_VIOLATION",
        "REFERENCE_TOO_SIMILAR",
        "INVENTORY_GEOMETRY_FAILURE",
        "QR_FAILURE"
    };

    public static GlobalVisualDnaDecision Evaluate(
        int reportedScore,
        IEnumerable<string>? defectCodes,
        bool visualEvidenceRecorded,
        bool brandDnaCompliant,
        bool originalityConfirmed,
        bool inventoryGeometryVerified,
        bool qrVerified)
    {
        if (reportedScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(reportedScore), "Reported score must be between 0 and 100.");
        }

        var defects = (defectCodes ?? [])
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        var unknown = defects.Where(code => !ReferenceLibrary.DoNotProduce.Contains(code, StringComparer.Ordinal)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException(
                "Unknown visual defect code(s): " + string.Join(", ", unknown) + ". None were invented.");
        }

        var blocking = defects
            .Where(code => HardFailures.Contains(code)
                || code == "FLAT_LIGHTING"
                || code == "MUDDY_COLORS"
                || code == "WEAK_CONTRAST"
                || code == "GENERIC_AI_LOOK"
                || code == "POOR_HIERARCHY"
                || code == "OVERLOADED_COMPOSITION"
                || code == "WEAK_SCREEN_IMPACT")
            .ToArray();

        var status = !visualEvidenceRecorded
            ? Withheld
            : !brandDnaCompliant || !originalityConfirmed || !inventoryGeometryVerified || !qrVerified
                || defects.Any(HardFailures.Contains)
                ? Fail
                : defects.Length > 0
                    ? Revise
                    : Pass;

        var notice = status switch
        {
            Pass => "All required evidence and deterministic checks passed. The score is informational and does not authorize delivery.",
            Revise => "Visual defects remain. Repair and reassess before approval. The score cannot waive defects.",
            Fail => "A hard release condition failed. Automatic approval is refused regardless of score.",
            _ => "Visual evidence is missing. Approval is withheld regardless of score."
        };

        return new GlobalVisualDnaDecision(
            status,
            reportedScore,
            blocking,
            visualEvidenceRecorded,
            brandDnaCompliant,
            originalityConfirmed,
            inventoryGeometryVerified,
            qrVerified,
            false,
            "NOT_SENT",
            notice);
    }
}
