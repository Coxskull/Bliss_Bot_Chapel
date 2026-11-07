using System.Text.Json;
using Bliss.Domain.AdvertisingRealEstate;

// Directive 345: deterministic fictional customer conversation traces only.
// This runner must never connect to production, read private customer data,
// invoke paid providers, send messages, or reserve real inventory.
if (args.Length != 2 || args[0] != "--output")
{
    Console.Error.WriteLine("Usage: AGM345.OfflineProbe --output <new-local-json-path>");
    return 2;
}
var destination = Path.GetFullPath(args[1]);
if (File.Exists(destination))
{
    Console.Error.WriteLine("Refusing to overwrite existing offline evidence.");
    return 2;
}

var root = Directory.GetCurrentDirectory();
var geometryPath = Path.Combine(root, "assets/alpha-prototypes/OWNER-GEOMETRY.tsv");
var authorizationPath = Path.Combine(root, "assets/alpha-prototypes/OWNER-AUTHORIZATION.tsv");
var geometry = OwnerGeometryFile.Read(File.ReadAllText(geometryPath));
var ownerPrototype = OwnerAuthorizationFile.Read(File.ReadAllText(authorizationPath));
if (geometry.Count != RealEstateCatalog.SlotIds.Count)
{
    throw new InvalidOperationException("Offline owner geometry file is missing a known catalog slot.");
}
var showcasePaths = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["ARE-001-V1"] = "assets/alpha-prototypes/advertising-real-estate/showcase/ARE-001-V1/ARE-001-V1.jpeg",
    ["ARE-002-V1"] = "assets/alpha-prototypes/advertising-real-estate/showcase/ARE-002-V1/ARE-002-V1.jpeg",
    ["ARE-003-V1"] = "assets/alpha-prototypes/advertising-real-estate/showcase/ARE-003-V1/ARE-003-V1.jpeg",
};
if (showcasePaths.Any(item => !File.Exists(Path.Combine(root, item.Value))))
{
    throw new InvalidOperationException("A referenced draft showcase is absent. Cannot substitute other artwork.");
}

// ACTIVE flags in OWNER-AUTHORIZATION.tsv are source facts about a *prototype*
// only. The separate product-specific authorization below is explicitly fictional.
var board = RealEstateCatalog.Board(
    assets: showcasePaths.ToDictionary(x => x.Key, x => true, StringComparer.Ordinal),
    creatorAuthorized: ownerPrototype.CreatorAuthorized,
    lifecycleOverrides: ownerPrototype.LifecycleOverrides,
    deviceStatus: ownerPrototype.DeviceStatus,
    platformStatus: ownerPrototype.PlatformStatus,
    geometry: geometry);

var frameVersion = geometry["LEFT_VERTICAL"].Version;
// This center protection and sellable area are *fictional test assumptions*,
// not a claim of an owner-approved production commercial perimeter.
var protectedCenter = new[] { new ProtectedCreatorRegion(frameVersion, 320, 180, 1280, 720) };
const long syntheticSellableArea = 1152000;

var scenarios = new (string Id, string Prompt, string ProductId, int Occurrences, bool Exclusive)[]
{
    ("T01", "This is my first campaign. I don't want to spend very much.", "ARE-S01", 5, false),
    ("T02", "We want a premium campaign with a much larger visual presence.", "ARE-P01", 5, false),
    ("T03", "We don't want another advertiser displayed beside us.", "ARE-E01", 5, true),
    ("T04", "Will this same advertising configuration work on mobile?", "ARE-P01", 5, false),
    ("T05-5", "I want the advertisement shown 5 times.", "ARE-P01", 5, false),
    ("T05-20", "I want the advertisement shown 20 times.", "ARE-P01", 20, false),
    ("T06", "How will I know my advertisement actually ran?", "ARE-P01", 5, false),
    ("T07", "I want 70% of the podcast screen for $10.", "ARE-P01", 5, false)
};
var conversations = scenarios.Select(scenario =>
{
    var product = board.Products.Single(item => item.ProductId == scenario.ProductId);
    var authorized = new CreatorProductProof(product.ProductId, product.Version,
        "FICTIONAL_CREATOR", "AUTH-FICTIONAL-V1", "synthetic://not-real-creator-rights", true);
    var available = new InventoryAvailabilityProof(product.ProductId, product.Version,
        "AVAILABLE", 1, "synthetic://not-real-availability", "AVAIL-FICTIONAL-V1");
    var compatible = new DevicePlatformProof("podcast", "desktop",
        "DEVICE-FICTIONAL-V1", "SUPPORTED");
    var asset = new ShowcaseAssetProof(product.ShowcaseId, "V1",
        new string('a', 64), "synthetic://not-a-real-content-digest", true);
    var review = CommercialInventoryReviewGate.SimulateConversation(
        board, scenario.Prompt, scenario.ProductId, geometry,
        protectedCenter, syntheticSellableArea, authorized, available, compatible,
        asset, scenario.Occurrences, scenario.Exclusive);
    if (review.Mode != "OFFLINE_SYNTHETIC_ONLY" || review.CampaignReady
        || review.InventedPrice || review.InventedProduct || review.DeliveryStatus != "NOT_SENT"
        || review.ProductReview?.CommitmentAuthorized == true
        || review.ProductReview?.ReservationAuthorized == true)
    {
        throw new InvalidOperationException("Fictional customer scenario attempted to assert commercial authorization.");
    }
    return new
    {
        caseId = scenario.Id,
        persona = "FICTIONAL_ADVERTISER",
        prompt = scenario.Prompt,
        requestedProductId = scenario.ProductId,
        requestedOccurrences = scenario.Occurrences,
        review
    };
}).ToArray();

var evidence = new
{
    schema = "ALPHA-345-OFFLINE-COMMERCIAL-TRANSCRIPT/V1",
    mode = "OFFLINE_SYNTHETIC_ONLY",
    source = "EXISTING_BLISS_CATALOG_AND_OWNER_PROTOTYPE_FILES",
    creatorProofAuthority = "SYNTHETIC_NOT_REAL",
    availabilityAuthority = "SYNTHETIC_NOT_REAL",
    showcaseApprovalAuthority = "SYNTHETIC_NOT_REAL",
    assetDigests = "SYNTHETIC_PLACEHOLDERS_NOT_VERIFIED_FILE_SHA256",
    geometryProtectedRegion = "SYNTHETIC_NOT_OWNER_SIGNED",
    geometrySellableBasis = "SYNTHETIC_DECLARED",
    economicsQuoteStatus = "PRICING_UNAVAILABLE",
    creativeQaStatus = "NOT_LINKED",
    deliveryStatus = "NOT_SENT",
    outreachAuthorized = false,
    reservationAuthorized = false,
    paymentAuthorized = false,
    commitmentAuthorized = false,
    realCustomerContacted = false,
    scenarioCount = conversations.Length,
    conversations
};
Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
File.WriteAllText(destination, JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("AGM345_OFFLINE_TRANSCRIPT_RECORDED cases=" + conversations.Length
    + " commercial_authority=false delivery=NOT_SENT pricing=PRICING_UNAVAILABLE");
return 0;
