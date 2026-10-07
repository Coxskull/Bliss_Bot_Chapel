using System.Text.Json;

namespace Bliss.Domain.CreativeAcademy;

/// <summary>
/// The stored attempt's failure record. An absent failure file is not a provider invoice and not a visual grade.
/// </summary>
public sealed record HarborlightFailureRecord(
    string Status,
    int StoredAttempts,
    int StoredFailureFiles,
    string Retries,
    string Notice);

public static class HarborlightFailures
{
    public const string NoneStored = "NONE_STORED";
    public const string RetriesNotRun = "NOT_RUN";

    public static HarborlightFailureRecord ParseEvidence(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("failuresAndRetries", out var record) || record.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Failures and retries are not stored. None were invented.");
        }

        var attempts = root.GetProperty("provider").TryGetProperty("attempts", out var providerAttempts)
            && providerAttempts.TryGetInt32(out var count)
            ? count
            : throw new InvalidOperationException("The stored attempt count is missing. None was invented.");
        var parsed = new HarborlightFailureRecord(
            ReadString(record, "status"),
            record.TryGetProperty("storedAttempts", out var storedAttempts) && storedAttempts.TryGetInt32(out var stored)
                ? stored
                : throw new InvalidOperationException("The stored attempt count is missing. None was invented."),
            record.TryGetProperty("storedFailureFiles", out var files) && files.TryGetInt32(out var fileCount)
                ? fileCount
                : throw new InvalidOperationException("The stored failure-file count is missing. None was invented."),
            ReadString(record, "retries"),
            ReadString(record, "notice"));
        if (!string.Equals(parsed.Status, NoneStored, StringComparison.Ordinal)
            || !string.Equals(parsed.Retries, RetriesNotRun, StringComparison.Ordinal)
            || parsed.StoredFailureFiles != 0
            || parsed.StoredAttempts != 1
            || parsed.StoredAttempts != attempts
            || !parsed.Notice.Contains("none was invented", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A failure or retry was not stored. None was invented.");
        }

        if (!string.Equals(ReadString(root.GetProperty("checks"), "humanReview"), "NOT_REQUESTED", StringComparison.Ordinal)
            || root.GetProperty("checks").GetProperty("campaignReady").ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException("The failure record claimed acceptance. None was recorded.");
        }

        return parsed;
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The failure field " + property + " is not stored. None was invented.");
        }

        return value.GetString() ?? string.Empty;
    }
}
