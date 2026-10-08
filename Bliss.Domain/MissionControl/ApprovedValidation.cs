using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.Demonstrations;

namespace Bliss.Domain.MissionControl;

public sealed record ValidationObservation(
    ApprovedValidationTest Test,
    string ClaimedResult,
    string Log,
    bool Held);

public static class ApprovedValidation
{
    public const string Observed = "OBSERVED";
    public const string Failed = "FAIL";

    public static ValidationObservation Observe(ApprovedValidationTest test)
    {
        try
        {
            var log = (test.Key ?? string.Empty).Trim() switch
            {
                "academy-retrieval" => AcademyRetrieval(),
                "academy-notes" => AcademyNotes(),
                "academy-originality" => AcademyOriginality(),
                "catalog-price" => CatalogPrice(),
                "catalog-occupancy" => CatalogOccupancy(),
                "ask-alpha-price" => AskAlphaPrice(),
                "economics-speech" => EconomicsSpeech(),
                "tenant-isolation" => TenantIsolation(),
                "conversation" => Conversation(),
                "evidence-retrieval" => EvidenceRetrieval(),
                _ => throw new InvalidOperationException("That test is not in the approved catalog. None was invented.")
            };
            if (string.IsNullOrWhiteSpace(log))
            {
                throw new InvalidOperationException("The check produced no observation. None was marked passed.");
            }

            return new ValidationObservation(test, Observed, log.Trim(), true);
        }
        catch (InvalidOperationException ex)
        {
            return new ValidationObservation(test, Failed, ex.Message, false);
        }
    }

    public static string Category(ApprovedValidationTest test) => test.Key switch
    {
        "catalog-price" or "ask-alpha-price" => "pricing",
        "economics-speech" => "economics",
        _ => test.Key
    };

    private static string AcademyRetrieval()
    {
        var library = new List<AcademyReferenceRecord>
        {
            new("ACA-011-V1", 11, "auto-parts", "Auto-Parts Store", "ACA-011-V1.png", "ACTIVE", "UPLOADED", true, "product realism", "exact counter"),
            new("ACA-008-V1", 8, "automotive", "Automotive Service", "ACA-008-V1.png", "ACTIVE", "UPLOADED", true, "lighting craftsmanship", "exact bay"),
            new("ACA-001-V1", 1, "pharmacy", "Pharmacy", "ACA-001-V1.png", "ACTIVE", "UPLOADED", true, "typography", "VidaCare"),
            new("ACA-016-V1", 16, "bakery", "Bakery", "ACA-016-V1.png", "CANDIDATE", "UPLOADED", true, "lighting craftsmanship", "Maison Fleur")
        };
        var retrieval = ReferenceLibrary.Select("auto-parts", library, ["lighting craftsmanship", "typography"]);
        Hold(retrieval.Status == "RETRIEVED", "Academy retrieval did not return RETRIEVED.");
        Hold(retrieval.Selected.Count == 3, "Academy retrieval did not keep three ACTIVE references.");
        Hold(retrieval.Selected[0].Reason == "niche match", "The niche reason was not recorded.");
        Hold(retrieval.Selected.All(item => item.ReferenceId != "ACA-016-V1"), "A candidate reference was retrieved.");
        Hold(retrieval.Delivery == "NOT_SENT", "Retrieval changed delivery.");
        return Lines(
            "Observed RETRIEVED.",
            "Selected " + string.Join(", ", retrieval.Selected.Select(item => item.ReferenceId + " (" + item.Reason + ")")) + ".",
            "Candidate ACA-016-V1 stayed out.",
            "Delivery NOT_SENT.",
            "This observation is not a review.");
    }

    private static string AcademyNotes()
    {
        var library = new List<AcademyReferenceRecord>
        {
            new("ACA-011-V1", 11, "auto-parts", "Auto-Parts Store", "Auto Parts.jpeg", "SUPERSEDED", "UPLOADED", true, "counter lighting", "Old Counter"),
            new("ACA-011-V2", 11, "auto-parts", "Auto-Parts Store", "Auto Parts.jpeg", "ACTIVE", "UPLOADED", true, "product realism", "Exact bay")
        };
        var retrieval = ReferenceLibrary.Select("auto-parts", library, ["counter lighting"]);
        Hold(retrieval.Selected.Count == 1 && retrieval.Selected[0].ReferenceId == "ACA-011-V2", "The superseded note was retrieved.");
        Hold(retrieval.Selected[0].Learn == "product realism" && retrieval.Selected[0].DoNotCopy == "Exact bay", "The stored notes were not returned.");
        return Lines(
            "Observed ACTIVE notes.",
            "Learn: product realism.",
            "Do not copy: Exact bay.",
            "Superseded ACA-011-V1 stayed readable and was not selected.",
            "This observation is not a review.");
    }

    private static string AcademyOriginality()
    {
        var library = new List<AcademyReferenceRecord>
        {
            new("ACA-001-V1", 1, "pharmacy", "Pharmacy", "ACA-001-V1.png", "ACTIVE", "UPLOADED", true, "color power", "VidaCare")
        };
        var copied = ReferenceLibrary.Judge("VidaCare Pharmacy", "Pickup today", library);
        var original = ReferenceLibrary.Judge("Norte Salud", "Retira tu receta", library);
        Hold(copied.Status == "REFERENCE_TOO_SIMILAR", "A do-not-copy identity was not refused.");
        Hold(original.Status == "PASS" && !original.CampaignReady && original.Delivery == "NOT_SENT", "An original name changed campaign readiness.");
        return Lines(
            "Observed a do-not-copy refusal: REFERENCE_TOO_SIMILAR.",
            "Observed a distinct name. Judge status was recorded. Campaign ready stayed false. Delivery NOT_SENT.",
            "The judge status is not this package's review.");
    }

    private static string CatalogPrice()
    {
        var turn = RealEstateCatalog.Reply(
            "This is my first campaign. I want to try Alpha, but I don't want to spend very much.",
            RealEstateCatalog.Board());
        Hold(!turn.InventedPrice && !turn.Reply.Contains('$'), "The draft catalog stated a price.");
        Hold(turn.Reply.Contains("nothing is offered for sale", StringComparison.Ordinal), "The draft catalog offered a product.");
        Hold(turn.Delivery == "NOT_SENT" && !turn.CampaignReady, "The draft catalog changed delivery.");
        return Lines(
            "Observed a draft catalog reply with no price and no sale.",
            "Delivery NOT_SENT.",
            "This observation is not an Economics authorization.");
    }

    private static string CatalogOccupancy()
    {
        var missing = RealEstateCatalog.Measure(null, null);
        Hold(!missing.Recorded && missing.Status == RealEstateCatalog.Unrecorded, "Missing occupancy was recorded.");
        return Lines(
            "Observed occupancy UNRECORDED.",
            missing.Notice,
            "No area was invented.");
    }

    private static string AskAlphaPrice()
    {
        var turn = DemonstrationConversation.Reply(Facts(), "How much does this cost?");
        Hold(turn.Reply.Contains("cannot invent a price", StringComparison.Ordinal), "Ask Alpha invented a price.");
        Hold(!turn.Reply.Contains('$') && turn.Gear == "integrity", "Ask Alpha left the integrity gear.");
        return Lines(
            "Observed an Ask Alpha price refusal.",
            "Gear: integrity.",
            "No number was stated.",
            "This observation is not a price.");
    }

    private static string EconomicsSpeech()
    {
        var reply = EconomicsPriceSpeech.PricingReply(null, null);
        Hold(reply.Contains("cannot invent a price", StringComparison.Ordinal), "A missing Economics result stated a price.");
        Hold(!reply.Any(char.IsDigit) && !reply.Contains('$'), "A missing Economics result contained a number.");
        return Lines(
            "Observed a missing Economics result with no number.",
            "No price was invented.",
            "This observation does not authorize a price.");
    }

    private static string TenantIsolation()
    {
        var rows = new[]
        {
            new HandoffRecord("casa-verde", Guid.NewGuid(), "https://example.com/casa-verde"),
            new HandoffRecord("mesa-norte", Guid.NewGuid(), "https://example.com/mesa-norte")
        };
        var visible = MarketplaceHandoff.Visible("mesa-norte", rows);
        Hold(visible.Count == 1 && visible[0].TenantKey == "mesa-norte", "The other advertiser was visible.");
        return Lines(
            "Observed one tenant row: mesa-norte.",
            "casa-verde stayed hidden.",
            "No other advertiser was invented.");
    }

    private static string Conversation()
    {
        var report = ConversationLaboratory.Run();
        Hold(report.ScenarioCount == 10 && report.Results.Count == 10, "The laboratory did not run the ten stored scenarios.");
        Hold(report.Notice.Contains("NOT_SENT", StringComparison.Ordinal), "The laboratory changed delivery.");
        if (!report.Passed)
        {
            var missed = string.Join(", ", report.Results.Where(item => !item.Passed).Select(item => item.Id));
            throw new InvalidOperationException("A stored persona scenario did not hold: " + missed);
        }

        return Lines(
            "Observed 10 stored persona scenarios.",
            report.Notice,
            "The laboratory result is not this package's review.");
    }

    private static string EvidenceRetrieval()
    {
        var dateOnlyRefused = false;
        try
        {
            EvidenceIdentity.Format(DateTime.UtcNow, [1, 2, 3]);
        }
        catch (InvalidOperationException)
        {
            dateOnlyRefused = true;
        }

        Hold(dateOnlyRefused, "The date alone was accepted as an evidence id.");
        var sample = EvidenceIdentity.Format(new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc), [1, 2, 3, 4, 5, 6]);
        Hold(EvidenceIdentity.IsWellFormed(sample), "A suffixed evidence id was not well formed.");
        return Lines(
            "Observed that a date without a random suffix is refused.",
            "Observed that a suffixed id is well formed.",
            "Drive " + EvidenceIdentity.NotConnected + ".",
            "ChatGPT retrieval " + EvidenceIdentity.NotRun + ".",
            "No shared Drive was searched. This observation is not a review.");
    }

    private static ProspectFacts Facts() => new(
        "Mesa Norte",
        "TIER_4",
        "No verified mailbox is on file.",
        BuyingRoleCatalog.RolesFor("restaurant"),
        ["Table Concept"]);

    private static void Hold(bool condition, string fact)
    {
        if (!condition)
        {
            throw new InvalidOperationException(fact);
        }
    }

    private static string Lines(params string[] lines) => string.Join('\n', lines);
}
