using Bliss.Domain.AdvertisingRealEstate;

namespace Bliss.Domain.CreativeAcademy;

public sealed record AcceptanceCampaignBrief(
    string VoyageKey,
    string AdvertiserName,
    string City,
    string Market,
    string Niche,
    string Objective,
    string InventoryProductId);

public sealed record AcceptanceReferenceIntelligence(
    int Registered,
    int Uploaded,
    int Candidate,
    int Active,
    int ActiveWithLearn,
    int ActiveWithDoNotCopy,
    string ProvenanceStatus,
    IReadOnlyList<AcceptanceReference> ActiveReferences,
    string Status,
    string Notice);

public sealed record AcceptanceReference(
    string ReferenceId,
    string Niche,
    string Lifecycle,
    string QualityStrengths,
    string Learn,
    string DoNotCopy,
    string QualityDnaVersion,
    string Provenance);

public sealed record AcceptanceEvidence(
    string Status,
    string Notice);

public sealed record AcceptanceProviderJob(
    string Status,
    string Provider,
    string Model,
    string JobId,
    int Attempts,
    int ModelCalls,
    string CostStatus,
    decimal? Cost,
    string Currency,
    string Notice);

public sealed record AcceptanceTraceStep(
    int Sequence,
    string Step,
    string Status,
    string Evidence);

public sealed record CreativeAcceptanceVoyage(
    string Status,
    string AmendmentStatus,
    AcceptanceCampaignBrief CampaignBrief,
    AcceptanceReferenceIntelligence ReferenceIntelligence,
    ProductionBrief ProductionBrief,
    AcceptanceEvidence BrandDna,
    AcceptanceProviderJob ProviderJob,
    AcceptanceEvidence FinishedCreative,
    InventoryAdaptation InventoryPreflight,
    SimilarityResult Originality,
    AcceptanceEvidence QualityQa,
    AcceptanceEvidence InventoryQa,
    AcceptanceEvidence BrandDnaCompliance,
    AcceptanceEvidence DoNotCopyCompliance,
    AcceptanceEvidence HumanReview,
    IReadOnlyList<string> GlobalQualityDna,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<AcceptanceTraceStep> Trace,
    int ModelCalls,
    bool CampaignReady,
    string Delivery,
    string Notice);

/// <summary>
/// Runs the pre-provider portion of one Academy acceptance voyage. The voyage
/// stops before generation when the reference gate is incomplete. Candidate
/// files never become ACTIVE through this path.
/// </summary>
public static class CreativeAcceptance
{
    public const string Open = "OPEN / PENDING AUTONOMOUS PRODUCTION ACCEPTANCE";

    public static CreativeAcceptanceVoyage Run(
        AcceptanceCampaignBrief campaign,
        IReadOnlyList<AcademyReferenceRecord> references,
        CatalogProduct product,
        IReadOnlyList<CatalogSlot> slots,
        string? provider,
        string? model,
        bool providerConfigured)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(references);
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(slots);
        RequireCampaign(campaign);

        if (CreativeAcademy.NamesReferenceIdentity(campaign.AdvertiserName))
        {
            throw new InvalidOperationException(
                "The acceptance advertiser reproduces a reference identity. A new advertiser was not invented.");
        }

        var active = references
            .Where(item => item.Lifecycle == ReferenceLibrary.Active && item.AssetPresent)
            .ToList();
        var activeReferences = active.Select(item => new AcceptanceReference(
            item.ReferenceId,
            item.NicheKey,
            item.Lifecycle,
            item.QualityStatus,
            item.Learn,
            item.DoNotCopy,
            ReferenceLibrary.QualityDnaVersion,
            "UNRECORDED")).ToList();
        var intelligenceComplete = active.Count > 0
            && active.All(item => !string.IsNullOrWhiteSpace(item.Learn)
                && !string.IsNullOrWhiteSpace(item.DoNotCopy));
        var intelligence = new AcceptanceReferenceIntelligence(
            references.Count,
            references.Count(item => item.AssetPresent),
            references.Count(item => item.Lifecycle == ReferenceLibrary.Candidate),
            active.Count,
            active.Count(item => !string.IsNullOrWhiteSpace(item.Learn)),
            active.Count(item => !string.IsNullOrWhiteSpace(item.DoNotCopy)),
            "UNRECORDED",
            activeReferences,
            intelligenceComplete ? "READY" : "REFERENCE_INTELLIGENCE_INCOMPLETE",
            intelligenceComplete
                ? "ACTIVE references have stored LEARN and DO NOT COPY intelligence. Provenance remains separately governed."
                : "No reference was promoted. Normal production requires human-authorized ACTIVE references with stored LEARN and DO NOT COPY intelligence. Provenance is unrecorded.");

        var brief = CreativeAcademy.PrepareProductionBrief(
            campaign.Niche,
            campaign.AdvertiserName,
            null,
            null,
            null,
            null,
            campaign.Market,
            null,
            campaign.Objective + ". Required inventory product " + campaign.InventoryProductId + ".",
            true,
            false,
            false,
            references);
        var inventory = RealEstateCatalog.Adapt(product, slots, false);

        var blockers = new List<string>();
        if (!intelligenceComplete)
        {
            blockers.Add("ACTIVE_REFERENCE_INTELLIGENCE_REQUIRED");
        }

        if (brief.Retrieval.Status != "RETRIEVED")
        {
            blockers.Add("ACTIVE_REFERENCE_RETRIEVAL_REQUIRED");
        }

        if (!providerConfigured)
        {
            blockers.Add("PROVIDER_CONFIGURATION_REQUIRED");
        }

        if (inventory.Status != "RECOMPOSED")
        {
            blockers.Add("INVENTORY_GEOMETRY_REQUIRED");
        }

        blockers.Add("VISUAL_QUALITY_QA_REQUIRED");
        blockers.Add("HUMAN_REVIEW_REQUIRED");
        blockers.Add("USAGE_COST_UNRECORDED");

        var firstGateComplete = intelligenceComplete && brief.Retrieval.Status == "RETRIEVED";
        var jobStatus = firstGateComplete
            ? providerConfigured ? "READY_FOR_GENERATION" : "PROVIDER_CONFIGURATION_REQUIRED"
            : "BLOCKED_REFERENCE_GATE";
        var providerJob = new AcceptanceProviderJob(
            jobStatus,
            string.IsNullOrWhiteSpace(provider) ? "UNCONFIGURED" : provider.Trim(),
            string.IsNullOrWhiteSpace(model) ? "UNRECORDED" : model.Trim(),
            "NOT_STARTED",
            0,
            0,
            "UNRECORDED",
            null,
            "",
            "No provider request was made. Usage and cost were not invented.");
        var originality = ReferenceLibrary.Compare("", "", "", "", references);
        var trace = new List<AcceptanceTraceStep>
        {
            new(1, "STORAGE", references.Count == 50 ? "RECORDED" : "INCOMPLETE",
                references.Count + " reference records; " + references.Count(item => item.AssetPresent) + " files on disk."),
            new(2, "REFERENCE_INTELLIGENCE", intelligence.Status, intelligence.Notice),
            new(3, "AUTOMATIC_RETRIEVAL", brief.Retrieval.Status, brief.Retrieval.Notice),
            new(4, "CREATIVE_REASONING", "BLOCKED", "Brand DNA, concept, copy, and composition were not invented after the reference gate failed."),
            new(5, "ORIGINAL_GENERATION", jobStatus, providerJob.Notice),
            new(6, "INVENTORY_PREFLIGHT", inventory.Status, inventory.Notice),
            new(7, "QUALITY_QA", "NOT_RUN", "No finished image exists. A visual score was not invented."),
            new(8, "ORIGINALITY_QA", originality.Status, originality.Notice),
            new(9, "HUMAN_REVIEW", "NOT_REQUESTED", "No finished image exists for a human to review."),
            new(10, "DELIVERY", "NOT_SENT", "Campaign ready is false. Green does not send.")
        };

        return new CreativeAcceptanceVoyage(
            "BLOCKED",
            Open,
            campaign,
            intelligence,
            brief,
            new AcceptanceEvidence(
                "NOT_CREATED",
                "No new Brand DNA was manufactured after the ACTIVE reference intelligence gate failed."),
            providerJob,
            new AcceptanceEvidence(
                "NOT_CREATED",
                "No finished original advertisement exists. A placeholder was not substituted."),
            inventory,
            originality,
            new AcceptanceEvidence("NOT_RUN", "No finished image exists. Quality QA and a visual benchmark were not invented."),
            new AcceptanceEvidence(
                inventory.Status == "RECOMPOSED" ? "READY" : "BLOCKED",
                inventory.Notice),
            new AcceptanceEvidence("NOT_RUN", "No new Brand DNA or finished image exists to compare."),
            new AcceptanceEvidence(
                active.Any(item => !string.IsNullOrWhiteSpace(item.DoNotCopy)) ? "NOT_RUN" : "BLOCKED",
                active.Any(item => !string.IsNullOrWhiteSpace(item.DoNotCopy))
                    ? "A finished image is required before compliance can be judged."
                    : "No ACTIVE reference has stored DO NOT COPY intelligence."),
            new AcceptanceEvidence("NOT_REQUESTED", "No human approval was claimed."),
            ReferenceLibrary.QualityDna,
            blockers.Distinct(StringComparer.Ordinal).ToList(),
            trace,
            0,
            false,
            "NOT_SENT",
            "The voyage stopped at the first incomplete acceptance gate. Refusal to invent missing intelligence is a pass condition, but ACA-8 is not accepted. Delivery remains NOT_SENT.");
    }

    private static void RequireCampaign(AcceptanceCampaignBrief campaign)
    {
        var values = new[]
        {
            campaign.VoyageKey,
            campaign.AdvertiserName,
            campaign.City,
            campaign.Market,
            campaign.Niche,
            campaign.Objective,
            campaign.InventoryProductId
        };
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                "Voyage key, advertiser, city, market, niche, objective, and inventory product are required. None was invented.");
        }

        if (campaign.VoyageKey.Length is < 8 or > 80
            || campaign.VoyageKey.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character == '-')))
        {
            throw new InvalidOperationException("A valid voyage key is required. None was invented.");
        }
    }
}
