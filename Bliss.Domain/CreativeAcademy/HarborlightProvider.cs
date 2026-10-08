using System.Text.Json;

namespace Bliss.Domain.CreativeAcademy;

/// <summary>
/// The stored provider facts for HV-001. An unreported cost stays unreported.
/// </summary>
public sealed record HarborlightProviderRecord(
    string Name,
    string Model,
    string JobId,
    int Attempts,
    string Usage,
    string CostStatus,
    decimal? ActualCost,
    string ActualCostState,
    bool NewSubscriptionPurchased,
    string Notice);

public static class HarborlightProvider
{
    public const string Unreported = "UNREPORTED_BY_PROVIDER";
    public const string StoredName = "Cursor GenerateImage capability";
    public const string StoredUsage = "one 1280x720 image";
    public const string UnreportedCost = "UNREPORTED";
    public const string NoticeText =
        "Model, job identifier, and cost are UNREPORTED_BY_PROVIDER. An unreported cost stays unreported. No new subscription was purchased.";

    public static HarborlightProviderRecord ParseEvidence(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("provider", out var provider) || provider.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("The Harborlight provider record is not stored. None was invented.");
        }

        if (!provider.TryGetProperty("actualCost", out var cost) || cost.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException("The provider did not report a cost. Zero was not invented.");
        }

        var currency = ReadString(provider, "currency");
        if (currency.Length != 0)
        {
            throw new InvalidOperationException("No currency was reported. None was invented.");
        }

        var record = new HarborlightProviderRecord(
            ReadString(provider, "name"),
            ReadString(provider, "model"),
            ReadString(provider, "jobId"),
            provider.TryGetProperty("attempts", out var attempts) && attempts.TryGetInt32(out var count)
                ? count
                : throw new InvalidOperationException("The stored attempt count is missing. None was invented."),
            ReadString(provider, "usage"),
            ReadString(provider, "costStatus"),
            null,
            UnreportedCost,
            provider.TryGetProperty("newSubscriptionPurchased", out var purchased)
                && purchased.ValueKind == JsonValueKind.False
                ? false
                : throw new InvalidOperationException("No new subscription was purchased. None was recorded."),
            NoticeText);
        if (!string.Equals(record.Name, StoredName, StringComparison.Ordinal)
            || !string.Equals(record.Model, Unreported, StringComparison.Ordinal)
            || !string.Equals(record.JobId, Unreported, StringComparison.Ordinal)
            || !string.Equals(record.CostStatus, Unreported, StringComparison.Ordinal)
            || !string.Equals(record.Usage, StoredUsage, StringComparison.Ordinal)
            || record.Attempts != 1)
        {
            throw new InvalidOperationException("The provider did not report a model, job, or cost. None was substituted.");
        }

        if (!string.Equals(ReadString(root.GetProperty("checks"), "humanReview"), "NOT_REQUESTED", StringComparison.Ordinal)
            || root.GetProperty("checks").GetProperty("campaignReady").ValueKind != JsonValueKind.False)
        {
            throw new InvalidOperationException("The provider record claimed acceptance. None was recorded.");
        }

        return record;
    }

    private static string ReadString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("The provider field " + property + " is not stored. None was invented.");
        }

        return value.GetString() ?? string.Empty;
    }
}
