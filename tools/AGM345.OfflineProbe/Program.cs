using System.Text.Json;
using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Entities;
using Bliss.Domain.Economics;
using Bliss.Domain.Demonstrations;

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


/* T14–T17: human reviewer attestations are FICTIONAL TEST INPUTS.
   The candidate and reference "hashes" below are synthetic placeholders,
   never evidence of actual uploaded artwork or rights. */
var dna = new WeddingPlannerBrandDna
{
    Id = Guid.Parse("22222222-2222-4222-8222-222222222222"),
    WorkspaceId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
    Version = 3, Status = "CONFIRMED",
    BrandName = "Fictional Orchid Coffee", Market = "PH", Language = "fil",
    Colors = "#143A38 and #FFE4B5", Font = "Example Sans",
    Niche = "coffee", Goals = "Synthetic quality test",
    Restrictions = "No unsupported claims"
};
var candidateHash = new string('a', 64);
var brandReview = new CommercialBrandReviewProof(dna.Id, dna.Version, candidateHash,
    dna.BrandName, dna.Colors, dna.Font, dna.Language,
    true, true, true, "FICTIONAL_BRAND_REVIEWER", "synthetic://brandqa", "V1");
var qualityTeacher = new AcademyReferenceRecord(
    "FICTIONAL-QUALITY-REF-01", 1, "coffee", "Coffee", "ref.png",
    ReferenceLibrary.Active, ReferenceLibrary.Uploaded, true,
    "Study lighting, texture and hierarchy craftsmanship",
    "Never copy branding, people, product composition or props",
    ProvenanceSource: "synthetic://reference-not-real", Ownership: "FICTIONAL",
    ApprovingAuthority: "FICTIONAL_REFERENCE_REVIEWER");
var qualities = CreativeAcademy.ReferenceParityGate.ToDictionary(
    name => name, _ => true, StringComparer.Ordinal);
var qualityReview = new CommercialQualityReviewProof(candidateHash,
    qualityTeacher.ReferenceId, "FICTIONAL-REF-V1",
    new string('b', 64), qualities, 100, [], true, true, true, true,
    "FICTIONAL_QUALITY_REVIEWER", "synthetic://quality-qa");
var localization = new CommercialLocalizationReviewProof(
    "PH", "fil", "synthetic://locale", "FICTIONAL_LOCALE_REVIEWER",
    true, true, true);
var policy = new CommercialPolicyReviewProof(
    "synthetic://policy", "FICTIONAL_POLICY_REVIEWER",
    true, true, true, true, true, true, true);
var geometryQa = CommercialGeometryGuard.Review(
    board.Products.Single(product => product.ProductId == "ARE-P01"),
    geometry, protectedCenter, syntheticSellableArea);
if (!geometryQa.Validated)
    throw new InvalidOperationException("Fictional creative sample has no valid local geometry.");

CommercialCreativeQaDecision ReviewCreative(
    CommercialBrandReviewProof brand,
    CommercialQualityReviewProof quality,
    CommercialLocalizationReviewProof locale,
    CommercialPolicyReviewProof compliance) =>
    CommercialCreativeQaGate.Evaluate(dna, brand, qualityTeacher,
        quality, locale, compliance, geometryQa);

var failedLights = qualities.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
failedLights["lighting craftsmanship"] = false;
var creativeQaCases = new[]
{
    new { caseId = "FICTIONAL_DOUBLE_PASS", output = ReviewCreative(
        brandReview, qualityReview, localization, policy) },
    new { caseId = "T14_QUALITY_PASS_BRAND_FAIL", output = ReviewCreative(
        brandReview with { LogoRightsVerified = false }, qualityReview, localization, policy) },
    new { caseId = "T15_BRAND_PASS_QUALITY_FAIL", output = ReviewCreative(
        brandReview, qualityReview with { ParityDimensions = failedLights }, localization, policy) },
    new { caseId = "T16_UNSUPPORTED_ADVERTISING_CLAIM", output = ReviewCreative(
        brandReview, qualityReview, localization, policy with { ClaimsSubstantiated = false }) },
    new { caseId = "T17_LOCALIZATION_REVIEW_REQUIRED", output = ReviewCreative(
        brandReview, qualityReview,
        localization with { MarketContextVerified = false }, policy) },
};
if (creativeQaCases[0].output.BrandStatus != CommercialCreativeQaGate.Passed
    || creativeQaCases[0].output.QualityStatus != CommercialCreativeQaGate.Passed
    || creativeQaCases.Skip(1).Any(x => x.output.SyntheticCandidateEligibleForAdvertiserReview)
    || creativeQaCases.Any(x => x.output.ExternalUseAuthorized || x.output.CampaignReady
        || x.output.Delivery != "NOT_SENT"))
    throw new InvalidOperationException("Creative source fixture unexpectedly authorized external use.");

// Proposed stage is deliberately DISPLAY_VERIFIED, but the resulting
// immutable receipt records NOT_VERIFIED and zero actual observations.
var premium = conversations.Single(x => x.caseId == "T02");
var syntheticDeliveryReceipts = new[]
{
    SyntheticCommercialDeliveryEvidence.Simulate(
        premium.review.ProductReview
            ?? throw new InvalidOperationException("Missing fictional premium retrieval."),
        creativeQaCases[0].output, "FICTIONAL_CREATOR",
        "FICTIONAL_ADVERTISER", 15, 5, "DISPLAY_VERIFIED")
};
if (syntheticDeliveryReceipts.Any(x => x.ActualDisplayVerified
    || x.RealObservedOccurrences != 0 || x.ReservationAuthorized
    || x.PaymentAuthorized || x.ExternalUseAuthorized
    || x.DeliveryStatus != "NOT_SENT"))
    throw new InvalidOperationException("Synthetic delivery receipt falsely asserted real evidence.");

// Reconcile a full object graph in the EXISTING Economics quote schema.
// These deterministic IDs and synthetic accepted status have no DB authority.
// Actual quote-to-ARE-product binding is absent from the existing schema.
var fakeQuoteId = Guid.Parse("10000000-0000-4000-8000-000000000001");
var fakeVersionId = Guid.Parse("10000000-0000-4000-8000-000000000002");
var fakeOpportunity = Guid.Parse("10000000-0000-4000-8000-000000000003");
var fakeCreator = Guid.Parse("10000000-0000-4000-8000-000000000004");
var fakeSlotIds = new[]
{
    Guid.Parse("10000000-0000-4000-8000-000000000011"),
    Guid.Parse("10000000-0000-4000-8000-000000000012")
};
var fakeRecommendations = new[]
{
    Guid.Parse("10000000-0000-4000-8000-000000000021"),
    Guid.Parse("10000000-0000-4000-8000-000000000022")
};
var premiumProduct = board.Products.Single(p => p.ProductId == "ARE-P01");
if (premiumProduct.SlotIds.Count != fakeSlotIds.Length)
    throw new InvalidOperationException("The fictional quote cannot change the stored product slot count.");
var scope = new SyntheticQuoteProductBinding(
    premiumProduct.ProductId, premiumProduct.Version,
    premium.review.ProductReview?.Geometry?.GeometryVersion ?? "UNVERIFIED",
    5, fakeCreator, fakeOpportunity, fakeQuoteId, fakeVersionId,
    "synthetic://invented-quote-product-link", "FICTIONAL-OWNER-LINK-V1",
    premiumProduct.SlotIds.Select((slot, index) =>
        new SyntheticQuoteSlotBinding(slot, fakeSlotIds[index],
            fakeRecommendations[index])).ToArray());
var sampleQuote = new Quote
{
    Id = fakeQuoteId, CurrencyCode = "PHP", Status = QuoteStatuses.Accepted,
    AdvertiserOpportunityId = fakeOpportunity, CurrentVersionNumber = 1
};
var sampleVersion = new QuoteVersion
{
    Id = fakeVersionId, QuoteId = fakeQuoteId, VersionNumber = 1,
    CurrencyCode = "PHP", SubtotalAmount = 200m, TotalAmount = 200m,
    LineItems = fakeRecommendations.Select((recommendation, index) =>
        new QuoteLineItem
        {
            Id = Guid.Parse(index == 0
                ? "20000000-0000-4000-8000-000000000001"
                : "20000000-0000-4000-8000-000000000002"),
            QuoteVersionId = fakeVersionId,
            RateRecommendationId = recommendation,
            Quantity = 5m, UnitAmount = 20m, LineAmount = 100m,
            CurrencyCode = "PHP"
        }).ToArray()
};
var approved = new QuoteApprovalDecision
{
    QuoteId = fakeQuoteId, QuoteVersionId = fakeVersionId,
    Decision = QuoteApprovalDecisions.Approved, ReviewerLabel = "FICTIONAL_APPROVER",
    Rationale = "SYNTHETIC_NOT_A_REAL_APPROVAL"
};
var accepted = new QuoteOutcome
{
    QuoteId = fakeQuoteId, QuoteVersionId = fakeVersionId,
    Response = QuoteOutcomeResponses.Accepted, Amount = 200m,
    CurrencyCode = "PHP"
};
var recommend = fakeRecommendations.Select((id, index) => new RateRecommendation
{
    Id = id, CreatorId = fakeCreator,
    AdvertiserOpportunityId = fakeOpportunity, AdInventorySlotId = fakeSlotIds[index],
    CurrencyCode = "PHP"
}).ToArray();
var syntheticEconomicsQuoteCheck = CommercialEconomicsReadOnlyGate.Inspect(
    premium.review.ProductReview, premiumProduct, sampleQuote,
    sampleVersion, approved, accepted, recommend, scope);
if (syntheticEconomicsQuoteCheck.Status != CommercialEconomicsReadOnlyGate.LinkedFixture
    || syntheticEconomicsQuoteCheck.CanStatePriceToCustomer
    || syntheticEconomicsQuoteCheck.RealProductBindingVerified
    || syntheticEconomicsQuoteCheck.AutomatedNegotiationAuthorized
    || syntheticEconomicsQuoteCheck.ReservationAuthorized
    || syntheticEconomicsQuoteCheck.PaymentAuthorized
    || syntheticEconomicsQuoteCheck.DeliveryStatus != "NOT_SENT")
    throw new InvalidOperationException("Fictional Economics input caused unauthorized commercial quote output.");

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
    creativeQaStatus = "SEPARATE_SYNTHETIC_BRAND_AND_QUALITY_GATES",
    creativeQaCases,
    syntheticDeliveryReceipts,
    syntheticEconomicsQuoteCheck,
    realEconomicsProductQuoteBinding = "NOT_VERIFIED_NO_LIVE_SOURCE",
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
    + " creative_cases=" + creativeQaCases.Length
    + " synthetic_receipts=" + syntheticDeliveryReceipts.Length
    + " economics_product_binding=SIMULATED_NOT_AUTHORIZED"
    + " commercial_authority=false delivery=NOT_SENT pricing=PRICING_UNAVAILABLE");
return 0;
