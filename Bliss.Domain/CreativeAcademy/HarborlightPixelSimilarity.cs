using System.Text.Json;

namespace Bliss.Domain.CreativeAcademy;

public sealed record HarborlightPixelDistance(
    string ReferenceId,
    string File,
    string Sha256,
    double MeanAbsoluteError,
    int AverageHashDistance);

/// <summary>
/// Stored distances from one original to the retrieved references. A distance is not a grade.
/// </summary>
public sealed record HarborlightPixelSimilarityReport(
    string Status,
    string Judgment,
    string VisualGrade,
    string Method,
    string OriginalSha256,
    IReadOnlyList<HarborlightPixelDistance> References,
    string Notice);

public static class HarborlightPixelSimilarity
{
    public const string Measured = "MEASURED";
    public const string NotJudged = "NOT_JUDGED";
    public const string NotAssigned = "NOT_ASSIGNED";

    public static HarborlightPixelSimilarityReport ParseEvidence(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("pixelSimilarity", out var report) || report.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The pixel similarity measurement is not stored. A judgment was not invented.");
        }

        if (!string.Equals(ReadString(root.GetProperty("checks"), "pixelSimilarity"), Measured, StringComparison.Ordinal)
            || !string.Equals(ReadString(root.GetProperty("checks"), "visualQa"), "NOT_RUN", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Pixel similarity was treated as visual QA. No visual grade was assigned.");
        }

        var references = ReadDistances(report);
        var parsed = new HarborlightPixelSimilarityReport(
            ReadString(report, "status"),
            ReadString(report, "judgment"),
            ReadString(report, "visualGrade"),
            ReadString(report, "method"),
            ReadString(report, "originalSha256"),
            references,
            ReadString(report, "notice"));
        if (!string.Equals(parsed.Status, Measured, StringComparison.Ordinal)
            || !string.Equals(parsed.Judgment, NotJudged, StringComparison.Ordinal)
            || !string.Equals(parsed.VisualGrade, NotAssigned, StringComparison.Ordinal)
            || parsed.Method.Length == 0
            || !parsed.Notice.Contains("not a quality grade", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The pixel measurement claimed a judgment. None was recorded.");
        }

        if (!string.Equals(parsed.OriginalSha256, ReadString(root.GetProperty("original"), "sha256"), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The pixel measurement does not name the stored original.");
        }

        var expected = root.GetProperty("retrieval").GetProperty("references").EnumerateArray()
            .Select(item => ReadString(item, "referenceId"))
            .ToArray();
        if (references.Count != expected.Length
            || references.Where((item, index) => !string.Equals(item.ReferenceId, expected[index], StringComparison.Ordinal)).Any())
        {
            throw new InvalidOperationException("The pixel measurement does not use the retrieved references.");
        }

        foreach (var distance in references)
        {
            if (distance.MeanAbsoluteError is < 0 or > 255
                || distance.AverageHashDistance is < 0 or > 64
                || distance.Sha256.Length != 64
                || distance.File.Length == 0)
            {
                throw new InvalidOperationException("A pixel distance is outside the measured range. None was invented.");
            }
        }

        return parsed;
    }

    private static IReadOnlyList<HarborlightPixelDistance> ReadDistances(JsonElement report)
    {
        if (!report.TryGetProperty("references", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The pixel distances are not stored. None were invented.");
        }

        return items.EnumerateArray().Select(item => new HarborlightPixelDistance(
            ReadString(item, "referenceId"),
            ReadString(item, "file"),
            ReadString(item, "sha256"),
            item.TryGetProperty("meanAbsoluteError", out var error) && error.TryGetDouble(out var value)
                ? value
                : throw new InvalidOperationException("A mean absolute error is not stored. None was invented."),
            item.TryGetProperty("averageHashDistance", out var hash) && hash.TryGetInt32(out var bits)
                ? bits
                : throw new InvalidOperationException("An average-hash distance is not stored. None was invented."))).ToArray();
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The pixel field " + property + " is not stored. None was invented.");
        }

        return value.GetString() ?? string.Empty;
    }
}
