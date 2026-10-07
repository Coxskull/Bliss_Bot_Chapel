namespace Bliss.Domain.AdvertisingRealEstate;

public sealed record OwnerAuthorization(
    bool CreatorAuthorized,
    string? DeviceStatus,
    string? PlatformStatus,
    IReadOnlyDictionary<string, string> LifecycleOverrides);

/// <summary>
/// Owner decisions that are approvals, not measurements. A missing file leaves
/// the draft defaults in place.
/// </summary>
public static class OwnerAuthorizationFile
{
    public static OwnerAuthorization Read(string? text)
    {
        var lifecycles = new Dictionary<string, string>(StringComparer.Ordinal);
        var creator = false;
        string? device = null;
        string? platform = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new OwnerAuthorization(false, null, null, lifecycles);
        }

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("key", StringComparison.OrdinalIgnoreCase) || line.StartsWith('#'))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length < 2)
            {
                continue;
            }

            var key = cells[0].Trim();
            var value = cells[1].Trim();
            if (key.Equals("creatorAuthorized", StringComparison.OrdinalIgnoreCase))
            {
                creator = value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            else if (key.Equals("deviceStatus", StringComparison.OrdinalIgnoreCase))
            {
                device = value;
            }
            else if (key.Equals("platformStatus", StringComparison.OrdinalIgnoreCase))
            {
                platform = value;
            }
            else if (key.Equals("lifecycle", StringComparison.OrdinalIgnoreCase) && cells.Length > 2)
            {
                lifecycles[value] = cells[2].Trim();
            }
        }

        return new OwnerAuthorization(creator, device, platform, lifecycles);
    }
}
