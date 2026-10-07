using Bliss.Domain.CreativeAcademy;

namespace Bliss.Domain.AdvertisingRealEstate;

public sealed record AcceptanceConversation(
    string CaseId,
    string Prompt,
    string Intent,
    IReadOnlyList<string> ProductIds,
    IReadOnlyList<string> ShowcaseIds,
    bool ShowcaseDisplayed,
    bool InventedProduct,
    bool InventedPrice,
    bool HumanEscalation,
    int? RequestedOccurrences,
    bool IntegrityPassed,
    bool AcceptancePassed,
    string Notice);

public sealed record JointGate(
    string GateId,
    string Status,
    string Evidence);

public sealed record MissionControlReading(
    string ProductId,
    string Version,
    string Tier,
    string OccupancyStatus,
    int? OccupancyBasisPoints,
    string Exclusivity,
    int Capacity,
    bool CreatorAuthorized,
    string DeviceStatus,
    string PlatformStatus,
    string Disclosure,
    string Availability,
    string Reservations,
    string Duration,
    string ScheduledOccurrences,
    string DeliveredOccurrences,
    string Exceptions,
    string EconomicsPricingVersion,
    string AskAlphaStatus,
    string ProofOfDelivery,
    string HumanEscalation);

public sealed record JointAcceptanceReading(
    string Status,
    string CatalogAmendment,
    string AcademyAmendment,
    string HostedAcceptance,
    IReadOnlyList<AcceptanceConversation> Conversations,
    IReadOnlyList<JointGate> Gates,
    IReadOnlyList<MissionControlReading> MissionControl,
    IReadOnlyList<RetrievedReference> RetrievedReferences,
    string EconomicsSource,
    string RetrievalStatus,
    string AdaptationStatus,
    string AdaptedProductId,
    int ModelCalls,
    bool CampaignReady,
    string Delivery,
    string Notice);

/// <summary>
/// Reads the catalog conversations, Academy retrieval, and inventory adaptation
/// already stored. It does not promote a product, approve a reference, invent a
/// price, or claim either amendment.
/// </summary>
public static class JointAcceptance
{
    public const string Blocked = "BLOCKED";
    public const string AwaitingReview = "AWAITING_REVIEW";
    public const string Unclaimed = "UNCLAIMED";

    private static readonly (string CaseId, string Prompt, string Kind)[] Cases =
    [
        ("ARE-11-1", "This is my first campaign. I want to try Alpha, but I don't want to spend very much.", "OFFER"),
        ("ARE-11-2", "We want a premium campaign with a much larger visual presence.", "OFFER"),
        ("ARE-11-3", "We don't want another advertiser displayed beside us.", "OFFER"),
        ("ARE-11-4", "Will this same advertising configuration work on mobile?", "DEVICE"),
        ("ARE-11-5", "What if I want the advertisement displayed 20 times instead of 5?", "FREQUENCY"),
        ("ARE-11-6", "How will I know my advertisement actually ran?", "DELIVERY"),
        ("ARE-11-7", "I want a configuration covering 70% of the podcast screen for $10.", "CRITICAL")
    ];

    public static JointAcceptanceReading Read(
        CatalogBoard board,
        IReadOnlyList<AcademyReferenceRecord> references,
        IReadOnlyList<CatalogSlot>? slots = null,
        string? availability = null,
        int openOwnerNeeds = 0)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(references);
        var geometry = slots ?? board.Slots;
        var conversations = Cases.Select(item => ReadConversation(item.CaseId, item.Prompt, item.Kind, board)).ToList();
        var retrieval = ReferenceLibrary.Select(
            "pharmacy",
            references,
            ["product realism", "lighting and depth", "typography"]);
        var product = board.Products.FirstOrDefault(item => item.ProductId == "ARE-P01")
            ?? throw new InvalidOperationException("ARE-P01 is not stored. None was invented.");
        var adaptation = RealEstateCatalog.Adapt(product, geometry, false);
        var notesComplete = retrieval.Status == "RETRIEVED"
            && retrieval.Selected.Count is >= 2 and <= 5
            && retrieval.Selected.All(item =>
                !string.IsNullOrWhiteSpace(item.Learn) && !string.IsNullOrWhiteSpace(item.DoNotCopy));
        var economicsStatus = EconomicsStatus(board.Economics);
        var creatorReady = board.CreatorAuthorized
            && board.Products.All(item => item.DeviceStatus != RealEstateCatalog.NeedsReview
                && item.PlatformStatus != RealEstateCatalog.NeedsReview);
        var availabilityStatus = string.IsNullOrWhiteSpace(availability) ? "UNRECORDED" : "RECORDED";
        var gates = new List<JointGate>
        {
            new("CATALOG_AMENDMENT", "OPEN", board.AmendmentStatus),
            new("ACADEMY_AMENDMENT", "OPEN", CreativeAcceptance.Open),
            new("HOSTED_ACCEPTANCE", Unclaimed, "Hosted acceptance stays unclaimed until a separate owner acceptance is recorded."),
            new(
                "ARE11_CONVERSATIONS",
                conversations.All(item => item.IntegrityPassed && item.AcceptancePassed) ? "RECORDED" : "BLOCKED",
                conversations.Count(item => item.AcceptancePassed) + " of " + conversations.Count
                + " acceptance conversations met their stored-data rule. Integrity refusals are separate from acceptance."),
            new(
                "ACTIVE_PRODUCT",
                conversations.Any(item => item.ShowcaseDisplayed) ? "RECORDED" : "BLOCKED",
                conversations.Any(item => item.ShowcaseDisplayed)
                    ? "An ACTIVE authorized product displayed its stored showcase."
                    : "No ACTIVE authorized product displayed a showcase. Draft artwork was not offered."),
            new(
                "ECONOMICS",
                economicsStatus,
                economicsStatus == "BLOCKED"
                    ? "A price figure in this reading was refused. Economics was not asked to invent one."
                    : economicsStatus == "NO_AUTHORIZED_PRICE"
                        ? board.Economics
                        : "Economics source " + board.Economics + " was supplied outside this module. This reading did not calculate it."),
            new(
                "ACADEMY_RETRIEVAL",
                notesComplete ? "RECORDED" : "BLOCKED",
                retrieval.Notice),
            new(
                "INVENTORY_ADAPTATION",
                adaptation.Status == "RECOMPOSED" ? "RECORDED" : "BLOCKED",
                adaptation.Notice),
            new(
                "CREATOR_AND_PLATFORM",
                creatorReady ? "RECORDED" : "BLOCKED",
                board.CreatorAuthorized
                    ? "Creator authorization is stored. Device " + product.DeviceStatus + ". Platform " + product.PlatformStatus + "."
                    : "Creator authorization is not stored. Device " + product.DeviceStatus + ". Platform " + product.PlatformStatus + ". None was invented."),
            new(
                "AVAILABILITY",
                availabilityStatus,
                availabilityStatus == "RECORDED"
                    ? "Stored availability: " + availability!.Trim() + "."
                    : "Availability is not stored. None was invented."),
            new(
                "DELIVERY",
                board.Delivery == "NOT_SENT" ? "RECORDED" : "BLOCKED",
                board.Deliveries.Count == 0
                    ? "No display is stored. Delivery remains NOT_SENT. Impressions were not invented."
                    : board.Deliveries.Count + " stored delivery rows were read. Delivery remains " + board.Delivery + "."),
            new(
                "OWNER_INPUTS",
                openOwnerNeeds == 0 ? "RECORDED" : "OPEN",
                openOwnerNeeds + " owner needs remain open. This reading does not fill them.")
        };
        var incomplete = gates.Any(item =>
            item.GateId is not "CATALOG_AMENDMENT" and not "ACADEMY_AMENDMENT" and not "HOSTED_ACCEPTANCE"
            && item.Status is "BLOCKED" or "UNRECORDED" or "OPEN");
        var mission = board.Products.Select(item => ReadMission(item, board, availabilityStatus)).ToList();
        return new JointAcceptanceReading(
            incomplete ? Blocked : AwaitingReview,
            board.AmendmentStatus,
            CreativeAcceptance.Open,
            Unclaimed,
            conversations,
            gates,
            mission,
            retrieval.Selected,
            board.Economics,
            retrieval.Status,
            adaptation.Status,
            product.ProductId,
            0,
            false,
            "NOT_SENT",
            incomplete
                ? "Joint acceptance is blocked on stored facts that are still missing. Refusal to invent them is not acceptance. Both amendments stay OPEN. Delivery remains NOT_SENT."
                : "The stored facts for this reading are present. Both amendments stay OPEN until the owner reviews them. Hosted acceptance is unclaimed. Delivery remains NOT_SENT.");
    }

    private static AcceptanceConversation ReadConversation(string caseId, string prompt, string kind, CatalogBoard board)
    {
        var turn = RealEstateCatalog.Reply(prompt, board);
        var integrity = !turn.InventedProduct
            && !turn.InventedPrice
            && turn.ModelCalls == 0
            && !turn.CampaignReady
            && turn.Delivery == "NOT_SENT"
            && !turn.Reply.Contains('$', StringComparison.Ordinal);
        if (kind == "CRITICAL")
        {
            integrity = integrity && turn.Intent == "UNSUPPORTED" && turn.HumanEscalation && !turn.Reply.Contains("70%", StringComparison.Ordinal);
        }

        var acceptance = kind switch
        {
            "OFFER" => turn.ShowcaseDisplayed && turn.ProductIds.Count > 0 && turn.ShowcaseIds.Count > 0,
            "DEVICE" => turn.Intent == "DEVICE"
                && board.Products.All(item => turn.Reply.Contains(item.DeviceStatus, StringComparison.Ordinal)
                    && turn.Reply.Contains(item.PlatformStatus, StringComparison.Ordinal)),
            "FREQUENCY" => turn.Intent == "FREQUENCY" && turn.RequestedOccurrences == 20 && !turn.InventedPrice,
            "DELIVERY" => turn.Intent == "DELIVERY" && turn.Reply.Contains("will not invent", StringComparison.Ordinal),
            "CRITICAL" => turn.Intent == "UNSUPPORTED" && turn.HumanEscalation && !turn.InventedProduct && !turn.InventedPrice,
            _ => false
        };
        var pairing = "Products " + (turn.ProductIds.Count == 0 ? "none" : string.Join(", ", turn.ProductIds))
            + ". Showcases " + (turn.ShowcaseIds.Count == 0 ? "none" : string.Join(", ", turn.ShowcaseIds)) + ".";
        return new AcceptanceConversation(
            caseId,
            prompt,
            turn.Intent,
            turn.ProductIds,
            turn.ShowcaseIds,
            turn.ShowcaseDisplayed,
            turn.InventedProduct,
            turn.InventedPrice,
            turn.HumanEscalation,
            turn.RequestedOccurrences,
            integrity,
            acceptance,
            pairing + " Showcase displayed " + (turn.ShowcaseDisplayed ? "Yes" : "No") + ". " + turn.Reply);
    }

    private static MissionControlReading ReadMission(CatalogProduct product, CatalogBoard board, string availabilityStatus)
    {
        var facts = board.Deliveries.Where(item => item.ProductId == product.ProductId).ToList();
        return new MissionControlReading(
            product.ProductId,
            product.Version,
            product.Tier,
            product.OccupancyStatus,
            product.OccupancyBasisPoints,
            product.Exclusivity,
            product.MaximumAdvertisers,
            board.CreatorAuthorized,
            product.DeviceStatus,
            product.PlatformStatus,
            "UNRECORDED",
            availabilityStatus == "RECORDED" ? "STORED" : "UNRECORDED",
            "UNRECORDED",
            string.Join(", ", product.DurationSeconds) + " seconds stored. Duration price UNRECORDED.",
            facts.Count == 0 ? "UNRECORDED" : string.Join(", ", facts.Select(item => item.ScheduledOccurrences?.ToString() ?? "UNRECORDED")),
            facts.Count == 0 ? "UNRECORDED" : string.Join(", ", facts.Select(item => item.DeliveredOccurrences?.ToString() ?? "UNRECORDED")),
            "UNRECORDED",
            EconomicsStatus(board.Economics) == "NO_AUTHORIZED_PRICE" ? "NO_AUTHORIZED_PRICE" : board.Economics,
            product.Lifecycle,
            facts.Count == 0 ? "NOT_RECORDED" : string.Join(", ", facts.Select(item => item.Status)),
            "NONE_STORED");
    }

    private static string EconomicsStatus(string source)
    {
        if (string.IsNullOrWhiteSpace(source) || source == RealEstateCatalog.NoPrice)
        {
            return "NO_AUTHORIZED_PRICE";
        }

        return source.Contains('$', StringComparison.Ordinal) ? "BLOCKED" : "RECORDED";
    }
}
