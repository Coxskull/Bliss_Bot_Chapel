using System.Text.Json;

namespace Bliss.Domain.CreativeAcademy;

public sealed record HarborlightTraceStep(int Sequence, string Step, string Status, string Evidence);

/// <summary>
/// The stored HV-001 workflow. Each status is copied from evidence that already exists.
/// </summary>
public static class HarborlightWorkflow
{
    public static IReadOnlyList<HarborlightTraceStep> Expected { get; } =
    [
        new(1, "BRIEF", "RECORDED",
            "Harborlight Pharmacy, Panama City, Panama. Niche pharmacy. Inventory ARE-P01. Objective: introduce convenient prescription pickup."),
        new(2, "AUTOMATIC_RETRIEVAL", "RETRIEVED",
            "ACA-001-V1, ACA-002-V1, ACA-006-V1, and ACA-008-V1 were selected by niche and quality need. References were not manually selected. Reference image files were not sent."),
        new(3, "CREATIVE_DNA", "RECORDED",
            "Headline: Prescription pickup, ready when you are. Palette: deep ocean navy, luminous coral, warm ivory, sea-glass teal. Copied reference identity: false."),
        new(4, "ORIGINAL_GENERATION", "GENERATED",
            "One 1280x720 image. Provider: Cursor GenerateImage capability. Model, job ID, and cost: UNREPORTED_BY_PROVIDER. Attempts: 1. New subscription: no."),
        new(5, "INVENTORY_ADAPTATION", "PURPOSE_BUILT_TEST_PREVIEW",
            "1920x1080 preview. LEFT_VERTICAL 320x1080 contains the brand panel. BOTTOM_FULL 1280x180 is purpose-built. Prototype scaled: no. Delivered: no."),
        new(6, "TEXT_ORIGINALITY", "PASS",
            "The advertiser name and headline did not reproduce a stored reference identity. Campaign ready remains false."),
        new(7, "PIXEL_SIMILARITY", "MEASURED",
            "Distances to the four retrieved references are stored. Judgment NOT_JUDGED. Visual grade NOT_ASSIGNED."),
        new(8, "DETERMINISTIC_QA", "PASS",
            "The original is 1280x720 and the preview is 1920x1080. Both hashes match the stored files. This check does not grade visual quality."),
        new(9, "VISUAL_QA", "NOT_RUN",
            "No visual score was recorded."),
        new(10, "HUMAN_REVIEW", "NOT_REQUESTED",
            "The review sheet is prepared. Every GQD-1 grade is UNCLASSIFIED. No acceptance was recorded."),
        new(11, "DELIVERY", "NOT_SENT",
            "Campaign ready is false. Green does not send."),
        new(12, "REGRESSION", "BASELINE_NOT_RECORDED",
            "No accepted creative is stored as a baseline."),
        new(13, "AMENDMENTS", "OPEN",
            "Creative Academy OPEN. Advertising Real Estate OPEN. Hosted acceptance UNCLAIMED.")
    ];

    public static IReadOnlyList<HarborlightTraceStep> ParseEvidence(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("workflowTrace", out var trace) || trace.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The Harborlight workflow trace is not stored. None was invented.");
        }

        var steps = trace.EnumerateArray().Select(item => new HarborlightTraceStep(
            item.TryGetProperty("sequence", out var sequence) && sequence.TryGetInt32(out var number)
                ? number
                : throw new InvalidOperationException("A workflow sequence is not stored."),
            ReadString(item, "step"),
            ReadString(item, "status"),
            ReadString(item, "evidence"))).ToArray();

        if (steps.Length != Expected.Count
            || steps.Where((step, index) => step != Expected[index]).Any())
        {
            throw new InvalidOperationException("The workflow trace does not match the stored Harborlight facts. A status was not invented.");
        }

        RequireSource(root);
        return steps;
    }

    private static void RequireSource(JsonElement root)
    {
        var checks = root.GetProperty("checks");
        if (!string.Equals(ReadString(checks, "visualQa"), "NOT_RUN", StringComparison.Ordinal)
            || !string.Equals(ReadString(checks, "humanReview"), "NOT_REQUESTED", StringComparison.Ordinal)
            || !string.Equals(ReadString(checks, "delivery"), "NOT_SENT", StringComparison.Ordinal)
            || !string.Equals(ReadString(checks, "regression"), "BASELINE_NOT_RECORDED", StringComparison.Ordinal)
            || !string.Equals(ReadString(checks, "pixelSimilarity"), "MEASURED", StringComparison.Ordinal)
            || !string.Equals(ReadString(checks, "deterministicImageValidation"), "PASS", StringComparison.Ordinal)
            || checks.GetProperty("campaignReady").ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException("The workflow source claimed acceptance. None was recorded.");
        }

        if (root.GetProperty("original").GetProperty("width").GetInt32() != 1280
            || root.GetProperty("original").GetProperty("height").GetInt32() != 720
            || root.GetProperty("adaptation").GetProperty("width").GetInt32() != 1920
            || root.GetProperty("adaptation").GetProperty("height").GetInt32() != 1080
            || root.GetProperty("retrieval").GetProperty("manuallySelected").GetBoolean()
            || root.GetProperty("retrieval").GetProperty("referenceAssetsSentToProvider").GetBoolean()
            || !string.Equals(ReadString(root.GetProperty("amendments"), "creativeAcademy"), "OPEN", StringComparison.Ordinal)
            || !string.Equals(ReadString(root.GetProperty("amendments"), "hostedAcceptance"), "UNCLAIMED", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The workflow source does not match the stored attempt.");
        }
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The workflow field " + property + " is not stored. None was invented.");
        }

        return value.GetString() ?? string.Empty;
    }
}
