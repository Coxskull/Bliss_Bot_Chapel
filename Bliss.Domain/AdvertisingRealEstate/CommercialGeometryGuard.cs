using System.Globalization;

namespace Bliss.Domain.AdvertisingRealEstate;

/// <summary>Explicitly versioned protected creator region supplied by an owner.
/// No screen geometry is inferred from a picture or a showcase.</summary>
public sealed record ProtectedCreatorRegion(
    string Version, int X, int Y, int Width, int Height);

public sealed record CommercialGeometryEvidence(
    string Status,
    string GeometryVersion,
    long? UnionAdvertiserArea,
    long? ReferenceFrameArea,
    int? FrameOccupancyBasisPoints,
    int? SellableOccupancyBasisPoints,
    IReadOnlyList<string> Findings)
{
    public bool Validated => Status == "GEOMETRY_VALIDATED";
}

/// <summary>
/// Deterministic, read-only geometry guard for the EXISTING Advertising Real
/// Estate catalog. This never creates inventory, changes a lifecycle, prices a
/// slot, or authorizes a campaign. Missing proofs fail closed.
/// </summary>
public static class CommercialGeometryGuard
{
    private readonly record struct Rectangle(long X, long Y, long Width, long Height)
    {
        public long Right => checked(X + Width);
        public long Bottom => checked(Y + Height);
    }

    public static CommercialGeometryEvidence Review(
        CatalogProduct product,
        IReadOnlyDictionary<string, OwnerSlotGeometry>? source,
        IReadOnlyList<ProtectedCreatorRegion>? protectedRegions,
        long? sellableAdvertiserArea = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        CommercialGeometryEvidence Reject(string status, string finding) =>
            new(status, string.Empty, null, null, null, null, [finding]);

        if (source is null || protectedRegions is null || protectedRegions.Count == 0)
            return Reject("GEOMETRY_UNVERIFIED", "Missing owner geometry or versioned creator-protection boundary.");
        if (product.SlotIds.Count == 0 || product.SlotIds.Count != product.SlotIds.Distinct(StringComparer.Ordinal).Count())
            return Reject("GEOMETRY_INVALID", "Inventory configuration has no slots or duplicate slot assignments.");

        var rows = new List<OwnerSlotGeometry>();
        foreach (var id in product.SlotIds)
        {
            if (!RealEstateCatalog.SlotIds.Contains(id, StringComparer.Ordinal)
                || !source.TryGetValue(id, out var row) || row.SlotId != id)
                return Reject("GEOMETRY_UNVERIFIED", "A selected inventory slot has no authoritative geometry row.");
            rows.Add(row);
        }

        var first = rows[0];
        if (first.CanvasWidth <= 0 || first.CanvasHeight <= 0
            || string.IsNullOrWhiteSpace(first.Version)
            || rows.Any(row => row.Version != first.Version
                || row.CanvasWidth != first.CanvasWidth || row.CanvasHeight != first.CanvasHeight
                || string.IsNullOrWhiteSpace(row.Basis)
                || string.IsNullOrWhiteSpace(row.ApprovingAuthority)
                || !DateOnly.TryParseExact(row.ApprovedOn, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _)))
            return Reject("GEOMETRY_UNVERIFIED", "Mixed frame/geometry versions or missing owner approval provenance.");

        var frameArea = checked((long)first.CanvasWidth * first.CanvasHeight);
        var rectangles = new List<Rectangle>();
        foreach (var row in rows)
        {
            if (row.Width <= 0 || row.Height <= 0 || row.OriginX < 0 || row.OriginY < 0
                || row.Area <= 0
                || (long)row.Width * row.Height != row.Area
                || (long)row.OriginX + row.Width > first.CanvasWidth
                || (long)row.OriginY + row.Height > first.CanvasHeight)
                return Reject("GEOMETRY_INVALID", "Advertiser rectangle has invalid bounds, dimensions or area.");
            rectangles.Add(new Rectangle(row.OriginX, row.OriginY, row.Width, row.Height));
        }

        var regions = new List<Rectangle>();
        foreach (var region in protectedRegions)
        {
            if (region.Version != first.Version
                || region.Width <= 0 || region.Height <= 0 || region.X < 0 || region.Y < 0
                || (long)region.X + region.Width > first.CanvasWidth
                || (long)region.Y + region.Height > first.CanvasHeight)
                return Reject("PROTECTED_REGION_UNVERIFIED", "Creator region is missing, incompatible or out of bounds.");
            regions.Add(new Rectangle(region.X, region.Y, region.Width, region.Height));
        }

        if (rectangles.Any(a => regions.Any(b => Intersects(a, b))))
            return Reject("CREATOR_CONTENT_OVERLAP", "Placement intersects a protected creator area.");
        for (var i = 0; i < rectangles.Count; i++)
        for (var j = i + 1; j < rectangles.Count; j++)
            if (Intersects(rectangles[i], rectangles[j]))
                return Reject("UNAUTHORIZED_SLOT_OVERLAP", "A product assigns overlapping advertiser rectangles.");

        // Validate even when only a subset of the frame is sellable; the basis
        // is explicitly supplied, not guessed from advertising artwork.
        if (sellableAdvertiserArea is <= 0 || sellableAdvertiserArea > frameArea)
            return Reject("SELLABLE_BASIS_INVALID", "Sellable advertiser reference area must be explicit and inside the frame.");

        var union = UnionArea(rectangles);
        if (union > sellableAdvertiserArea.Value)
            return Reject("SELLABLE_BASIS_INVALID", "Allocated union exceeds the declared sellable area.");
        var frameBasisPoints = (int)decimal.Round(union * 10000m / frameArea, 0, MidpointRounding.AwayFromZero);
        var sellableBasisPoints = (int)decimal.Round(union * 10000m / sellableAdvertiserArea.Value, 0, MidpointRounding.AwayFromZero);
        return new("GEOMETRY_VALIDATED", first.Version, union, frameArea, frameBasisPoints,
            sellableBasisPoints, ["Owner-provenance rows and protected creator region checked.",
                "Frame occupancy differs from sellable inventory occupancy. Neither is a price.",
                "Rendering and availability remain subject to separate authorization."]);
    }

    public static long UnionArea(IReadOnlyList<(int X, int Y, int Width, int Height)> rectangles)
    {
        ArgumentNullException.ThrowIfNull(rectangles);
        if (rectangles.Any(r => r.Width <= 0 || r.Height <= 0 || r.X < 0 || r.Y < 0))
            throw new ArgumentOutOfRangeException(nameof(rectangles), "Positive bounded rectangles required.");
        return UnionArea(rectangles.Select(r => new Rectangle(r.X, r.Y, r.Width, r.Height)).ToArray());
    }

    private static long UnionArea(IReadOnlyList<Rectangle> rectangles)
    {
        if (rectangles.Count == 0) return 0;
        var boundaries = rectangles.SelectMany(r => new[] { r.X, r.Right }).Distinct().Order().ToArray();
        long result = 0;
        for (var i = 1; i < boundaries.Length; i++)
        {
            var startX = boundaries[i - 1];
            var endX = boundaries[i];
            var ranges = rectangles.Where(r => r.X < endX && r.Right > startX)
                .Select(r => (r.Y, End: r.Bottom)).OrderBy(r => r.Y).ToArray();
            if (ranges.Length == 0) continue;
            long currentStart = ranges[0].Y, currentEnd = ranges[0].End, yUnion = 0;
            foreach (var interval in ranges.Skip(1))
            {
                if (interval.Y > currentEnd)
                {
                    yUnion = checked(yUnion + currentEnd - currentStart);
                    currentStart = interval.Y;
                    currentEnd = interval.End;
                }
                else
                {
                    currentEnd = Math.Max(currentEnd, interval.End);
                }
            }
            yUnion = checked(yUnion + currentEnd - currentStart);
            result = checked(result + checked((endX - startX) * yUnion));
        }
        return result;
    }

    private static bool Intersects(Rectangle a, Rectangle b) =>
        a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
}
