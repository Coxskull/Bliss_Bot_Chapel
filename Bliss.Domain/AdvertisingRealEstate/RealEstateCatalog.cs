using System.Globalization;

namespace Bliss.Domain.AdvertisingRealEstate;

public sealed record CatalogSlot(
    string SlotId,
    string Version,
    string Lifecycle,
    int? Width,
    int? Height,
    int? OriginX,
    int? OriginY,
    int? Area,
    string Notice);

public sealed record CatalogProduct(
    string ProductId,
    string Version,
    string Tier,
    string Exclusivity,
    int MaximumAdvertisers,
    IReadOnlyList<string> SlotIds,
    string ShowcaseId,
    string Lifecycle,
    IReadOnlyList<int> DurationSeconds,
    string DeviceStatus,
    string PlatformStatus,
    string OccupancyStatus,
    int? OccupancyBasisPoints,
    string Notice);

public sealed record ShowcaseRecord(
    string ShowcaseId,
    string Role,
    string Lifecycle,
    bool AssetPresent,
    bool Authoritative,
    string Notice);

public sealed record DeliveryFact(
    string ProductId,
    string Status,
    int? ScheduledOccurrences,
    int? DeliveredOccurrences);

public sealed record CatalogBoard(
    string AmendmentStatus,
    string Notice,
    IReadOnlyList<CatalogSlot> Slots,
    IReadOnlyList<CatalogProduct> Products,
    IReadOnlyList<ShowcaseRecord> Showcases,
    bool CreatorAuthorized,
    string Economics,
    IReadOnlyList<DeliveryFact> Deliveries,
    int ModelCalls,
    bool CampaignReady,
    bool GreenMeansSend,
    string Delivery);

public sealed record OccupancyResult(
    bool Recorded,
    int? BasisPoints,
    string Status,
    string Notice);

public sealed record AdaptedSlot(
    string SlotId,
    int? Width,
    int? Height);

public sealed record InventoryAdaptation(
    string ProductId,
    string Status,
    IReadOnlyList<AdaptedSlot> Slots,
    bool ScaledFromPrototype,
    int ModelCalls,
    bool CampaignReady,
    string Delivery,
    string Notice);

public sealed record CatalogTurn(
    string Intent,
    string Reply,
    IReadOnlyList<string> ProductIds,
    IReadOnlyList<string> ShowcaseIds,
    bool ShowcaseDisplayed,
    string GeometryStatus,
    string DeviceStatus,
    string Exclusivity,
    int? RequestedOccurrences,
    string Economics,
    bool HumanEscalation,
    bool InventedProduct,
    bool InventedPrice,
    int ModelCalls,
    bool CampaignReady,
    bool GreenMeansSend,
    string Delivery,
    string AmendmentStatus);

/// <summary>
/// Draft Advertising Real Estate catalog. Geometry stays unrecorded until a
/// human supplies dimensions. Illustrations do not become occupancy or price.
/// Ask Alpha may offer only ACTIVE products.
/// </summary>
public static class RealEstateCatalog
{
    public const string Draft = "DRAFT";
    public const string Active = "ACTIVE";
    public const string Recorded = "RECORDED";
    public const string Unrecorded = "UNRECORDED";
    public const string NeedsReview = "NEEDS_REVIEW";
    public const string AmendmentOpen = "OPEN / PENDING IMPLEMENTATION + END-TO-END EVIDENCE";
    public const string NoPrice =
        "Economics has no accepted result for this inventory request. Ask Alpha will not state a number.";
    public const string GuideId = "ARE-GUIDE-001-V1";

    public static IReadOnlyList<string> SlotIds { get; } =
    [
        "LEFT_VERTICAL",
        "RIGHT_VERTICAL",
        "TOP_LEFT",
        "TOP_RIGHT",
        "BOTTOM_LEFT",
        "BOTTOM_RIGHT",
        "BOTTOM_FULL"
    ];

    public static IReadOnlyList<int> UnpricedDurations { get; } = [15];

    public static OccupancyResult Measure(int? allocatedArea, int? sellableArea)
    {
        if (allocatedArea is null || sellableArea is null)
        {
            return new OccupancyResult(
                false,
                null,
                Unrecorded,
                "Official occupancy is unrecorded. A percentage drawn on a showcase is not a geometry specification.");
        }

        if (sellableArea <= 0 || allocatedArea < 0 || allocatedArea > sellableArea)
        {
            throw new InvalidOperationException(
                "Occupancy requires a positive sellable area and an allocated area inside that area. None is invented.");
        }

        var basisPoints = (int)decimal.Round(
            allocatedArea.Value * 10000m / sellableArea.Value,
            0,
            MidpointRounding.AwayFromZero);
        return new OccupancyResult(
            true,
            basisPoints,
            "RECORDED",
            "Occupancy is " + basisPoints.ToString(CultureInfo.InvariantCulture)
            + " basis points of the defined sellable area. This is an Economics input. It is not a price.");
    }

    public static CatalogBoard Board(
        IReadOnlyDictionary<string, bool>? assets = null,
        bool creatorAuthorized = false,
        string? economics = null,
        IReadOnlyList<DeliveryFact>? deliveries = null,
        IReadOnlyDictionary<string, string>? lifecycleOverrides = null,
        string? deviceStatus = null,
        string? platformStatus = null,
        IReadOnlyDictionary<string, OwnerSlotGeometry>? geometry = null)
    {
        var files = assets ?? new Dictionary<string, bool>(StringComparer.Ordinal);
        var overrides = lifecycleOverrides ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var slots = SlotIds.Select(id =>
        {
            if (geometry is not null && geometry.TryGetValue(id, out var stored))
            {
                return new CatalogSlot(
                    id,
                    stored.Version,
                    Active,
                    stored.Width,
                    stored.Height,
                    stored.OriginX,
                    stored.OriginY,
                    stored.Area,
                    stored.Basis + ". Owner-approved recommendation on " + stored.ApprovedOn
                    + " by " + stored.ApprovingAuthority + "; it is not measured from the guide.");
            }

            return new CatalogSlot(
                id,
                "V1",
                Draft,
                null,
                null,
                null,
                null,
                null,
                id + " is named. Width, height, coordinates, and area are unrecorded until a human approves a geometry version.");
        }).ToList();
        var geometryRecorded = slots.All(slot =>
            slot.Width > 0 && slot.Height > 0 && slot.OriginX >= 0 && slot.OriginY >= 0 && slot.Area > 0);

        var products = new List<CatalogProduct>
        {
            Product("ARE-P01", "PREMIUM", "EXCLUSIVE_WHEN_AUTHORIZED", 1, ["LEFT_VERTICAL", "BOTTOM_FULL"], "ARE-001-V1",
                "Premium single advertiser. Left vertical plus the bottom remainder. The creator center stays protected.", overrides, deviceStatus, platformStatus),
            Product("ARE-P02", "PREMIUM", "SHARED", 2, ["LEFT_VERTICAL", "BOTTOM_LEFT", "RIGHT_VERTICAL", "BOTTOM_RIGHT"], "ARE-002-V1",
                "Premium two-advertiser split. Shared inventory. Each advertiser keeps a separate brand identity.", overrides, deviceStatus, platformStatus),
            Product("ARE-S01", "ENTRY", "SHARED", 4, ["TOP_LEFT", "TOP_RIGHT", "BOTTOM_LEFT", "BOTTOM_RIGHT"], "ARE-003-V1",
                "Entry and standard smaller rectangles for a lower screen presence.", overrides, deviceStatus, platformStatus),
            Product("ARE-E01", "EXCLUSIVE", "EXCLUSIVE", 1, ["LEFT_VERTICAL", "BOTTOM_FULL"], "ARE-001-V1",
                "Exclusive product. Sale still requires creator authorization, availability, and an Economics result.", overrides, deviceStatus, platformStatus)
        };

        var showcases = new List<ShowcaseRecord>
        {
            Showcase(GuideId, "EDUCATIONAL", false, "Educational catalog overview. Printed percentages and prices are not authoritative.", files, overrides),
            Showcase("ARE-001-V1", "PREMIUM", true, "Premium single-advertiser illustration. The pictured brand is not the template.", files, overrides),
            Showcase("ARE-002-V1", "PREMIUM", true, "Premium two-advertiser illustration. The pictured brands are not the template.", files, overrides),
            Showcase("ARE-003-V1", "ENTRY", true, "Standard and entry multi-advertiser illustration.", files, overrides)
        };

        return new CatalogBoard(
            AmendmentOpen,
            "Ask Alpha may sell only an ACTIVE product. Geometry is "
            + (geometryRecorded ? "stored as an owner-approved recommendation" : "unrecorded")
            + ". Economics has not priced these products. Delivery remains NOT_SENT.",
            slots,
            products,
            showcases,
            creatorAuthorized,
            string.IsNullOrWhiteSpace(economics) ? NoPrice : economics.Trim(),
            deliveries ?? [],
            0,
            false,
            false,
            "NOT_SENT");
    }

    public static InventoryAdaptation Adapt(
        CatalogProduct product,
        IReadOnlyList<CatalogSlot> slots,
        bool scalePrototype)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(slots);
        if (scalePrototype)
        {
            throw new InvalidOperationException(
                "The Academy prototype was not scaled into the placement. Prototype dimensions are not podcast-advertising dimensions.");
        }

        if (product.SlotIds.Count == 0)
        {
            throw new InvalidOperationException("That inventory product is not stored. None was invented.");
        }

        var placed = new List<AdaptedSlot>();
        var geometryRecorded = true;
        foreach (var slotId in product.SlotIds)
        {
            var slot = slots.FirstOrDefault(item => item.SlotId.Equals(slotId, StringComparison.Ordinal));
            if (slot is null)
            {
                throw new InvalidOperationException("That inventory product is not stored. None was invented.");
            }

            if (slot.Width is null || slot.Height is null || slot.Width <= 0 || slot.Height <= 0)
            {
                geometryRecorded = false;
                placed.Add(new AdaptedSlot(slot.SlotId, null, null));
                continue;
            }

            placed.Add(new AdaptedSlot(slot.SlotId, slot.Width, slot.Height));
        }

        if (!geometryRecorded)
        {
            var missing = placed.Where(item => item.Width is null || item.Height is null).Select(item => item.SlotId).ToList();
            var geometry = missing.Count == placed.Count
                ? "Width and height are unrecorded."
                : "Width and height are unrecorded on " + string.Join(", ", missing) + ", so the product was not recomposed.";
            return new InventoryAdaptation(
                product.ProductId,
                "GEOMETRY_UNRECORDED",
                placed,
                false,
                0,
                false,
                "NOT_SENT",
                "The Academy prototype was not scaled. " + geometry + " Named slots for "
                + product.ProductId + " are " + string.Join(", ", placed.Select(item => item.SlotId))
                + ". Missing dimensions were not invented. Area was not calculated. Campaign ready is false. Delivery remains NOT_SENT.");
        }

        return new InventoryAdaptation(
            product.ProductId,
            "RECOMPOSED",
            placed,
            false,
            0,
            false,
            "NOT_SENT",
            "Recomposed into the stored slot geometry for " + product.ProductId
            + ". The Academy prototype was not scaled. Width and height are the stored slot values. Area was not calculated. Campaign ready is false. Delivery remains NOT_SENT.");
    }

    public static CatalogTurn Reply(string? message, CatalogBoard board)
    {
        ArgumentNullException.ThrowIfNull(board);
        var text = (message ?? string.Empty).Trim();
        var intent = Classify(text);
        var occurrences = intent == "FREQUENCY" ? ReadOccurrences(text) : null;
        var matches = Match(intent, board).ToList();
        var active = matches.Where(item => item.Lifecycle == Active).ToList();
        var offered = active.Count > 0 ? active : matches;
        var showcaseIds = offered.Select(item => item.ShowcaseId).Distinct(StringComparer.Ordinal).ToList();
        var displayed = intent is not ("UNSUPPORTED" or "DEVICE" or "FREQUENCY" or "DELIVERY")
            && active.Count > 0
            && board.CreatorAuthorized
            && offered.All(product => board.Showcases.Any(item =>
                item.ShowcaseId == product.ShowcaseId
                && item.Role != "EDUCATIONAL"
                && item.AssetPresent
                && item.Lifecycle == Active));

        var reply = Compose(intent, text, board, offered, active, displayed, occurrences);
        return new CatalogTurn(
            intent,
            reply,
            offered.Select(item => item.ProductId).ToList(),
            showcaseIds,
            displayed,
            board.Slots.All(slot => slot.Width > 0 && slot.Height > 0 && slot.Area > 0) ? Recorded : Unrecorded,
            board.Products[0].DeviceStatus,
            offered.FirstOrDefault()?.Exclusivity ?? "NONE",
            occurrences,
            board.Economics,
            intent is "UNSUPPORTED" || !board.CreatorAuthorized || active.Count == 0,
            false,
            false,
            0,
            false,
            false,
            "NOT_SENT",
            board.AmendmentStatus);
    }

    private static CatalogProduct Product(
        string id,
        string tier,
        string exclusivity,
        int capacity,
        IReadOnlyList<string> slots,
        string showcaseId,
        string notice,
        IReadOnlyDictionary<string, string> lifecycleOverrides,
        string? deviceStatus = null,
        string? platformStatus = null)
    {
        var lifecycle = lifecycleOverrides.TryGetValue(id, out var overridden) ? overridden : Draft;
        return new CatalogProduct(
            id,
            "V1",
            tier,
            exclusivity,
            capacity,
            slots,
            showcaseId,
            lifecycle,
            UnpricedDurations,
            string.IsNullOrWhiteSpace(deviceStatus) ? NeedsReview : deviceStatus.Trim(),
            string.IsNullOrWhiteSpace(platformStatus) ? NeedsReview : platformStatus.Trim(),
            Unrecorded,
            null,
            notice + " Lifecycle " + lifecycle + ". Occupancy UNRECORDED. Duration of 15 seconds is stored as an unpriced option.");
    }

    private static ShowcaseRecord Showcase(
        string id,
        string role,
        bool productArt,
        string notice,
        IReadOnlyDictionary<string, bool> files,
        IReadOnlyDictionary<string, string> lifecycleOverrides)
    {
        var present = files.TryGetValue(id, out var found) && found;
        var lifecycle = lifecycleOverrides.TryGetValue(id, out var overridden)
            ? overridden
            : Draft;
        return new ShowcaseRecord(
            id,
            role,
            lifecycle,
            present,
            !productArt,
            notice + (present ? " Asset is on file." : " Asset is awaiting upload.")
            + (productArt ? " This picture is not the contract." : " This guide is not geometry and not a price."));
    }

    private static string DistinctStatus(CatalogBoard board, Func<CatalogProduct, string> select)
    {
        var statuses = board.Products.Select(select).Distinct(StringComparer.Ordinal).ToList();
        return statuses.Count == 0 ? Unrecorded : string.Join(", ", statuses);
    }

    private static string Classify(string text)
    {
        var value = text.ToLowerInvariant();
        if (value.Contains("70%", StringComparison.Ordinal) || value.Contains("70 percent", StringComparison.Ordinal)
            || value.Contains('$') || value.Contains("ten dollars", StringComparison.Ordinal))
        {
            return "UNSUPPORTED";
        }

        if (value.Contains("actually ran", StringComparison.Ordinal)
            || value.Contains("proof", StringComparison.Ordinal)
            || value.Contains("know my ad", StringComparison.Ordinal)
            || value.Contains("impressions", StringComparison.Ordinal))
        {
            return "DELIVERY";
        }

        if (value.Contains("mobile", StringComparison.Ordinal))
        {
            return "DEVICE";
        }

        if (value.Contains("times", StringComparison.Ordinal) || value.Contains("occurrence", StringComparison.Ordinal)
            || value.Contains("frequency", StringComparison.Ordinal))
        {
            return "FREQUENCY";
        }

        if (value.Contains("exclusive", StringComparison.Ordinal)
            || value.Contains("beside", StringComparison.Ordinal)
            || value.Contains("another advertiser", StringComparison.Ordinal)
            || value.Contains("another company", StringComparison.Ordinal))
        {
            return "EXCLUSIVITY";
        }

        if (value.Contains("premium", StringComparison.Ordinal)
            || value.Contains("more visibility", StringComparison.Ordinal)
            || value.Contains("larger visual", StringComparison.Ordinal))
        {
            return "PREMIUM";
        }

        if (value.Contains("first campaign", StringComparison.Ordinal)
            || value.Contains("don't want to spend", StringComparison.Ordinal)
            || value.Contains("do not want to spend", StringComparison.Ordinal)
            || value.Contains("not very much", StringComparison.Ordinal)
            || value.Contains("try alpha", StringComparison.Ordinal)
            || value.Contains("small budget", StringComparison.Ordinal)
            || value.Contains("entry", StringComparison.Ordinal))
        {
            return "ENTRY";
        }

        return "CATALOG";
    }

    private static int? ReadOccurrences(string text)
    {
        var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var count)
                && parts[i + 1].StartsWith("time", StringComparison.OrdinalIgnoreCase)
                && count > 0)
            {
                return count;
            }
        }

        return null;
    }

    private static IEnumerable<CatalogProduct> Match(string intent, CatalogBoard board)
    {
        return intent switch
        {
            "ENTRY" => board.Products.Where(item => item.Tier is "ENTRY" or "STANDARD"),
            "PREMIUM" => board.Products.Where(item => item.Tier == "PREMIUM"),
            "EXCLUSIVITY" => board.Products.Where(item => item.Exclusivity.StartsWith("EXCLUSIVE", StringComparison.Ordinal)),
            "UNSUPPORTED" => board.Products,
            "CATALOG" => board.Products,
            _ => []
        };
    }

    private static string Compose(
        string intent,
        string text,
        CatalogBoard board,
        IReadOnlyList<CatalogProduct> offered,
        IReadOnlyList<CatalogProduct> active,
        bool displayed,
        int? occurrences)
    {
        var ids = offered.Count == 0
            ? "none"
            : string.Join(", ", offered.Select(item => item.ProductId + " (" + item.Tier + ", " + item.Exclusivity + ", " + item.Lifecycle + ")"));
        var lead = intent switch
        {
            "ENTRY" => "Ask Alpha recognizes a smaller first commitment. Eligible draft or active entry products: " + ids + ".",
            "PREMIUM" => "Ask Alpha recognizes a request for greater visual presence. Eligible premium products: " + ids + ".",
            "EXCLUSIVITY" => "Ask Alpha recognizes an exclusivity requirement. Eligible exclusive products: " + ids + ". Exclusivity is not promised unless the product is ACTIVE, the creator authorized it, availability allows it, and Economics authorizes the terms.",
            "DEVICE" => "Device status is " + DistinctStatus(board, item => item.DeviceStatus)
                + ". Platform status is " + DistinctStatus(board, item => item.PlatformStatus)
                + ". Ask Alpha will not guess, and it will not stretch a desktop advertisement until it is unreadable.",
            "FREQUENCY" => "Ask Alpha recognizes an occurrence change"
                + (occurrences is null ? "." : " to " + occurrences.Value.ToString(CultureInfo.InvariantCulture) + " displays.")
                + " Five displays and twenty displays are different requests. " + board.Economics,
            "DELIVERY" => board.Deliveries.Count == 0
                ? "No delivery record is stored. Ask Alpha will not invent impressions, scans, clicks, or performance."
                : "Stored delivery records: " + board.Deliveries.Count.ToString(CultureInfo.InvariantCulture) + ". Only stored facts are repeated.",
            "UNSUPPORTED" => "That configuration is not an Inventory Product. A stated price is not an Economics result. Ask Alpha will not create a product, a geometry, or a rate. Draft alternatives remain " + ids + ". This request is escalated for human review.",
            _ => "Ask Alpha can explain approved advertising time slots. Sellable products must already exist. Current products: " + ids + "."
        };

        if (intent is "DEVICE" or "FREQUENCY" or "DELIVERY")
        {
            return lead + " Showcase pictures are not displayed for this question. Campaign ready is false. Delivery remains NOT_SENT.";
        }

        var sale = active.Count == 0
            ? " No Inventory Product in this answer is ACTIVE, so nothing is offered for sale."
            : " ACTIVE products may be discussed.";
        var creator = board.CreatorAuthorized
            ? " Creator authorization is on file for this reading."
            : " Creator authorization is not on file, so Ask Alpha cannot sell the inventory.";
        var picture = displayed
            ? " The approved showcase illustration for the ACTIVE product is available to display."
            : " No approved product illustration is displayed. " + GuideId + " may be shown only as an educational guide, and its printed percentages are not occupancy.";
        var geometry = board.Slots.All(slot => slot.Width > 0 && slot.Height > 0 && slot.Area > 0)
            ? " Recommended slot geometry is RECORDED. Occupancy remains UNRECORDED."
            : " Geometry occupancy is UNRECORDED.";
        return lead + sale + creator + picture + geometry + " " + board.Economics
            + " Campaign ready is false. Delivery remains NOT_SENT.";
    }
}
