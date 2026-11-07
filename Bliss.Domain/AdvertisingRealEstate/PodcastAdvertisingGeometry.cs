namespace Bliss.Domain.AdvertisingRealEstate;

/// <summary>
/// Exact pixel coordinates on a 1920x1080 output frame. The screenshot is a
/// concept illustration, not a measurement source or a rendered asset.
/// </summary>
public sealed record PodcastAdvertisingRect(string SlotId, int X, int Y, int Width, int Height)
{
    public int Right => checked(X + Width);
    public int Bottom => checked(Y + Height);
    public int Area => checked(Width * Height);
}

public sealed record PodcastAdvertisingLayout(
    string Version,
    int RectangleCount,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<PodcastAdvertisingRect> AdSlots,
    PodcastAdvertisingRect ProtectedCreatorRegion,
    int AdCoverageBasisPoints,
    int ProtectedCreatorBasisPoints,
    bool RendererIntegrated,
    string Status);

/// <summary>
/// Reference specification for the owner's two-, four-, and six-rectangle
/// 16:9 podcast-advertising diagram. Not a sales catalog activation, not an
/// authorization to render copyrighted source media, and not a guarantee that
/// the existing 1280x720 demo compositor has adopted these placements.
/// </summary>
public static class PodcastAdvertisingGeometry
{
    public const string Version = "ARE-PODCAST-16X9-V2";
    public const int CanvasWidth = 1920;
    public const int CanvasHeight = 1080;
    public const int CanvasArea = CanvasWidth * CanvasHeight;
    public const string Status = "REFERENCE_STANDARD_ONLY_RENDERER_NOT_INTEGRATED";

    public static PodcastAdvertisingLayout Get(int rectangleCount)
    {
        PodcastAdvertisingRect[] slots;
        PodcastAdvertisingRect protectedCreator;
        switch (rectangleCount)
        {
            case 2:
                slots =
                [
                    new("BOTTOM_LEFT", 0, 810, 960, 270),
                    new("BOTTOM_RIGHT", 960, 810, 960, 270)
                ];
                protectedCreator = new("CREATOR_SAFE", 0, 0, 1920, 810);
                break;
            case 4:
                slots =
                [
                    new("TOP_LEFT", 0, 0, 960, 180),
                    new("TOP_RIGHT", 960, 0, 960, 180),
                    new("BOTTOM_LEFT", 0, 900, 960, 180),
                    new("BOTTOM_RIGHT", 960, 900, 960, 180)
                ];
                protectedCreator = new("CREATOR_SAFE", 0, 180, 1920, 720);
                break;
            case 6:
                slots =
                [
                    new("TOP_LEFT", 0, 0, 960, 180),
                    new("TOP_RIGHT", 960, 0, 960, 180),
                    new("LEFT_SIDE", 0, 270, 270, 540),
                    new("RIGHT_SIDE", 1650, 270, 270, 540),
                    new("BOTTOM_LEFT", 0, 900, 960, 180),
                    new("BOTTOM_RIGHT", 960, 900, 960, 180)
                ];
                // Side slots are vertically centered in the 720-pixel band;
                // their 90-pixel upper/lower margins are not ad inventory.
                protectedCreator = new("CREATOR_SAFE", 270, 180, 1380, 720);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(rectangleCount), "Only 2, 4 or 6 advertiser rectangles are defined.");
        }

        ValidateGeometry(slots, protectedCreator, rectangleCount);
        var area = slots.Sum(slot => slot.Area);
        return new PodcastAdvertisingLayout(
            Version,
            rectangleCount,
            CanvasWidth,
            CanvasHeight,
            Array.AsReadOnly(slots),
            protectedCreator,
            BasisPoints(area),
            BasisPoints(protectedCreator.Area),
            RendererIntegrated: false,
            Status);
    }

    /// <summary>
    /// Fail closed: exact pixel match required, no implicit scaling or
    /// cropping of advertiser artwork. Used by a future compositor gate.
    /// </summary>
    public static PodcastAdvertisingRect ValidateAdvertiserTemplate(
        int rectangleCount, string slotId, int assetWidth, int assetHeight)
    {
        var layout = Get(rectangleCount);
        var slot = layout.AdSlots.SingleOrDefault(
            item => string.Equals(item.SlotId, slotId, StringComparison.Ordinal))
            ?? throw new ArgumentException("Unknown slot in the selected layout.", nameof(slotId));

        if (assetWidth != slot.Width || assetHeight != slot.Height)
        {
            throw new InvalidOperationException(
                $"Advertiser template for {rectangleCount} rectangles / {slotId} must be exactly " +
                $"{slot.Width}x{slot.Height}px; got {assetWidth}x{assetHeight}px. " +
                "Automatic stretch, crop or upscale is forbidden.");
        }
        return slot;
    }

    public static void ValidateRenderedCanvas(int width, int height)
    {
        if (width != CanvasWidth || height != CanvasHeight)
        {
            throw new InvalidOperationException(
                $"Rendered podcast frame must be exactly {CanvasWidth}x{CanvasHeight}px; got {width}x{height}px.");
        }
    }

    private static int BasisPoints(int area) =>
        (int)decimal.Round(area * 10000m / CanvasArea, 0, MidpointRounding.AwayFromZero);

    private static void ValidateGeometry(
        IReadOnlyList<PodcastAdvertisingRect> slots,
        PodcastAdvertisingRect protectedCreator,
        int expectedCount)
    {
        if (slots.Count != expectedCount || !Inside(protectedCreator))
        {
            throw new InvalidOperationException("Invalid slot count or protected creator region.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            if (!ids.Add(slot.SlotId) || !Inside(slot) || Intersects(slot, protectedCreator))
            {
                throw new InvalidOperationException("Advertising rectangle is invalid or invades protected creator content.");
            }

            for (var next = index + 1; next < slots.Count; next++)
            {
                if (Intersects(slot, slots[next]))
                {
                    throw new InvalidOperationException("Advertising rectangles overlap each other.");
                }
            }
        }

        if (slots.Sum(slot => (long)slot.Area) + protectedCreator.Area > CanvasArea)
        {
            throw new InvalidOperationException("Creator and ad coverage exceeds canvas area.");
        }
    }

    private static bool Inside(PodcastAdvertisingRect rect) =>
        rect.Width > 0 && rect.Height > 0 && rect.X >= 0 && rect.Y >= 0
        && rect.Right <= CanvasWidth && rect.Bottom <= CanvasHeight;

    private static bool Intersects(PodcastAdvertisingRect first, PodcastAdvertisingRect second) =>
        first.X < second.Right && second.X < first.Right
        && first.Y < second.Bottom && second.Y < first.Bottom;
}
