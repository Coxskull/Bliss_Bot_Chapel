namespace Bliss.Domain.AdvertisingRealEstate;

/// <summary>
/// Deterministic geometry evidence for catalog review. The denominator is the
/// declared frame, NOT sellable inventory, and this never authorizes pricing.
/// The region protecting creator content must be independently authorized.
/// </summary>
public static class CatalogGeometrySafety
{
    public static CatalogGeometryProof Inspect(
        IReadOnlyList<OwnerSlotGeometry> slots,
        OwnerSlotGeometry? declaredProtectedCreatorRegion = null)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (slots.Count == 0)
            return new CatalogGeometryProof("GEOMETRY_UNRECORDED", 0, 0, null, null, false,
                "No approved geometry coordinates were supplied. No occupancy was invented.");

        var first = slots[0];
        var canvasW = first.CanvasWidth;
        var canvasH = first.CanvasHeight;
        if (canvasW <= 0 || canvasH <= 0 || slots.Any(s =>
                s.CanvasWidth != canvasW || s.CanvasHeight != canvasH
                || s.Width <= 0 || s.Height <= 0 || s.OriginX < 0 || s.OriginY < 0
                || (long)s.Width * s.Height != s.Area
                || (long)s.OriginX + s.Width > canvasW
                || (long)s.OriginY + s.Height > canvasH))
            return new CatalogGeometryProof("GEOMETRY_INVALID", canvasW, canvasH, null, null, false,
                "Invalid or mixed-canvas slot geometry; no commercial offer can be formed.");

        var overlaps = false;
        for (var i = 0; i < slots.Count; i++)
            for (var j = i + 1; j < slots.Count; j++)
                if (Overlaps(slots[i], slots[j])) overlaps = true;

        // Sweep over all unique horizontal boundaries. For each x strip, merge
        // vertical intervals to count overlapping advertising rectangles once.
        var edges = slots.SelectMany(s => new[] { s.OriginX, s.OriginX + s.Width })
            .Distinct().Order().ToArray();
        long union = 0;
        for (var i = 0; i < edges.Length - 1; i++)
        {
            var x0 = edges[i];
            var x1 = edges[i + 1];
            var intervals = slots
                .Where(s => s.OriginX < x1 && s.OriginX + s.Width > x0)
                .Select(s => (Start: s.OriginY, End: s.OriginY + s.Height))
                .OrderBy(pair => pair.Start)
                .ToArray();
            if (intervals.Length == 0) continue;
            var start = intervals[0].Start;
            var end = intervals[0].End;
            long yUnion = 0;
            foreach (var pair in intervals.Skip(1))
            {
                if (pair.Start > end)
                {
                    yUnion += end - start;
                    start = pair.Start;
                    end = pair.End;
                }
                else end = Math.Max(end, pair.End);
            }
            yUnion += end - start;
            union += (long)(x1 - x0) * yUnion;
        }

        var frameBasisPoints = (int)decimal.Round(
            union * 10000m / ((long)canvasW * canvasH), 0, MidpointRounding.AwayFromZero);
        var declared = declaredProtectedCreatorRegion is not null;
        var protectedValid = declared
            && declaredProtectedCreatorRegion!.CanvasWidth == canvasW
            && declaredProtectedCreatorRegion.CanvasHeight == canvasH
            && declaredProtectedCreatorRegion.Width > 0
            && declaredProtectedCreatorRegion.Height > 0
            && declaredProtectedCreatorRegion.OriginX >= 0
            && declaredProtectedCreatorRegion.OriginY >= 0
            && (long)declaredProtectedCreatorRegion.OriginX + declaredProtectedCreatorRegion.Width <= canvasW
            && (long)declaredProtectedCreatorRegion.OriginY + declaredProtectedCreatorRegion.Height <= canvasH;

        var intrudes = protectedValid
            && slots.Any(s => Overlaps(s, declaredProtectedCreatorRegion!));
        var status = overlaps ? "OVERLAP_REVIEW_REQUIRED"
            : !declared ? "PROTECTED_CREATOR_REGION_UNVERIFIED"
            : !protectedValid || intrudes ? "PROTECTED_CREATOR_REGION_VIOLATION"
            : "VALID_AGAINST_DECLARED_PROTECTED_REGION";

        return new CatalogGeometryProof(
            status, canvasW, canvasH, union, frameBasisPoints,
            protectedValid && !intrudes && !overlaps,
            "Frame occupancy is a union of slot rectangles. It is NOT percent of sellable advertising inventory, not a price, not creator approval and not mobile validation.");
    }

    private static bool Overlaps(OwnerSlotGeometry a, OwnerSlotGeometry b) =>
        a.OriginX < (long)b.OriginX + b.Width
        && b.OriginX < (long)a.OriginX + a.Width
        && a.OriginY < (long)b.OriginY + b.Height
        && b.OriginY < (long)a.OriginY + a.Height;
}

public sealed record CatalogGeometryProof(
    string Status,
    int CanvasWidth,
    int CanvasHeight,
    long? UnionAllocatedPixels,
    int? FrameOccupancyBasisPoints,
    bool DeclaredCreatorRegionProtected,
    string Notice);
