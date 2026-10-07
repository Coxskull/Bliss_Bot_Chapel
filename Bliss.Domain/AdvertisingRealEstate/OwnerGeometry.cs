using System.Globalization;

namespace Bliss.Domain.AdvertisingRealEstate;

public sealed record OwnerSlotGeometry(
    string SlotId,
    string Version,
    int CanvasWidth,
    int CanvasHeight,
    int Width,
    int Height,
    int OriginX,
    int OriginY,
    int Area,
    string Basis,
    string ApprovedOn,
    string ApprovingAuthority);

/// <summary>
/// Reads an owner-approved geometry recommendation. Invalid or incomplete rows
/// are ignored so a malformed file cannot silently become catalog geometry.
/// </summary>
public static class OwnerGeometryFile
{
    public static IReadOnlyDictionary<string, OwnerSlotGeometry> Read(string? text)
    {
        var geometries = new Dictionary<string, OwnerSlotGeometry>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            return geometries;
        }

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("slotId", StringComparison.OrdinalIgnoreCase) || line.StartsWith('#'))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length < 12
                || !ReadPositive(cells[2], out var canvasWidth)
                || !ReadPositive(cells[3], out var canvasHeight)
                || !ReadPositive(cells[4], out var width)
                || !ReadPositive(cells[5], out var height)
                || !ReadNonNegative(cells[6], out var originX)
                || !ReadNonNegative(cells[7], out var originY)
                || !ReadPositive(cells[8], out var area)
                || area != width * height
                || originX + width > canvasWidth
                || originY + height > canvasHeight)
            {
                continue;
            }

            var slotId = cells[0].Trim();
            var version = cells[1].Trim();
            var basis = cells[9].Trim();
            var approvedOn = cells[10].Trim();
            var authority = cells[11].Trim();
            if (!RealEstateCatalog.SlotIds.Contains(slotId, StringComparer.Ordinal)
                || string.IsNullOrWhiteSpace(version)
                || string.IsNullOrWhiteSpace(basis)
                || string.IsNullOrWhiteSpace(approvedOn)
                || string.IsNullOrWhiteSpace(authority))
            {
                continue;
            }

            geometries[slotId] = new OwnerSlotGeometry(
                slotId,
                version,
                canvasWidth,
                canvasHeight,
                width,
                height,
                originX,
                originY,
                area,
                basis,
                approvedOn,
                authority);
        }

        return geometries;
    }

    private static bool ReadPositive(string value, out int result) =>
        int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out result) && result > 0;

    private static bool ReadNonNegative(string value, out int result) =>
        int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out result) && result >= 0;
}
