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

public sealed record AcceptanceGenerationOutcome(
    bool Succeeded,
    string JobId,
    string ImagePath,
    int ModelCalls,
    string CostStatus,
    decimal? Cost,
    string Currency,
    string Notice);

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
    IReadOnlyList<StoredRejection> Rejections,
    RegressionSuiteResult Regression,
    IReadOnlyList<AssetProvenance> Provenance,
    IReadOnlyList<StoredQualityReading> StoredQuality,
    bool ReferenceAssetsSentToProvider,
    IReadOnlyList<string> GlobalQualityDna,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<AcceptanceTraceStep> Trace,
    int ModelCalls,
    bool CampaignReady,
    string Delivery,
    string Notice);

/// <summary>
/// Runs one Academy acceptance voyage. Generation is authorized only after the
/// retrieved ACTIVE references already have LEARN and DO NOT COPY notes and the
/// purchased slots already have width and height. Candidate files never become
/// ACTIVE through this path.
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
        var retrieved = brief.Retrieval.Selected
            .Select(item => references.FirstOrDefault(record =>
                record.ReferenceId.Equals(item.ReferenceId, StringComparison.OrdinalIgnoreCase)))
            .OfType<AcademyReferenceRecord>()
            .ToList();
        var intelligenceComplete = retrieved.Count > 0
            && retrieved.All(item => !string.IsNullOrWhiteSpace(item.Learn)
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
                ? "Retrieved ACTIVE references have stored LEARN and DO NOT COPY intelligence. References that were not retrieved were not attached. Provenance remains separately governed."
                : "No reference was promoted. Normal production requires human-authorized ACTIVE references with stored LEARN and DO NOT COPY intelligence. Provenance is unrecorded.");

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
        var geometryReady = inventory.Status == "RECOMPOSED";
        var jobStatus = !firstGateComplete
            ? "BLOCKED_REFERENCE_GATE"
            : !providerConfigured
                ? "PROVIDER_CONFIGURATION_REQUIRED"
                : !geometryReady
                    ? "BLOCKED_INVENTORY_GEOMETRY"
                    : "READY_FOR_GENERATION";
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
            jobStatus switch
            {
                "READY_FOR_GENERATION" =>
                    "Generation is authorized but not started. Reference image files were not sent. Usage and cost were not invented.",
                "BLOCKED_INVENTORY_GEOMETRY" =>
                    "The retrieved references are ready, but purchased slot width and height are unrecorded. Generation was not started. None was invented.",
                "PROVIDER_CONFIGURATION_REQUIRED" =>
                    "The retrieved references are ready, but no image provider is configured. Generation was not started. Usage and cost were not invented.",
                _ => "No provider request was made. Usage and cost were not invented."
            });
        var brandDna = firstGateComplete
            ? new AcceptanceEvidence(
                "PREPARED",
                "New Brand DNA uses this advertiser. Palette: " + brief.Palette
                + ". Headline: " + brief.Headline
                + ". This is the advertiser brief, not a reference brand and not a recolored prototype.")
            : new AcceptanceEvidence(
                "NOT_CREATED",
                "No new Brand DNA was manufactured after the ACTIVE reference intelligence gate failed.");
        var originality = ReferenceLibrary.Compare("", "", "", "", references);
        var rejections = new List<StoredRejection>();
        if (inventory.Status != "RECOMPOSED")
        {
            rejections.Add(ReferenceLibrary.Reject("INVENTORY_GEOMETRY_FAILURE", product.ProductId));
        }

        var regression = ReferenceLibrary.ReadRegression(null);
        var provenance = references.Select(ReferenceLibrary.ReadProvenance).ToList();
        var storedQuality = references
            .Where(item => item.NicheKey.Equals(campaign.Niche, StringComparison.OrdinalIgnoreCase))
            .Select(ReferenceLibrary.ReadStoredQuality)
            .ToList();
        var assetsSent = provenance.Any(ReferenceLibrary.MaySendToProvider);
        var trace = new List<AcceptanceTraceStep>
        {
            new(1, "STORAGE", references.Count == 50 ? "RECORDED" : "INCOMPLETE",
                references.Count + " reference records; " + references.Count(item => item.AssetPresent) + " files on disk."),
            new(2, "REFERENCE_INTELLIGENCE", intelligence.Status, intelligence.Notice),
            new(3, "AUTOMATIC_RETRIEVAL", brief.Retrieval.Status, brief.Retrieval.Notice),
            new(4, "CREATIVE_REASONING", firstGateComplete ? "PREPARED" : "BLOCKED",
                firstGateComplete
                    ? brandDna.Notice
                    : "Brand DNA, concept, copy, and composition were not invented after the reference gate failed."),
            new(5, "ORIGINAL_GENERATION", jobStatus, providerJob.Notice),
            new(6, "INVENTORY_PREFLIGHT", inventory.Status, inventory.Notice),
            new(7, "QUALITY_QA", "NOT_RUN", "No finished image exists. A visual score was not invented."),
            new(8, "ORIGINALITY_QA", originality.Status, originality.Notice),
            new(9, "HUMAN_REVIEW", "NOT_REQUESTED", "No finished image exists for a human to review."),
            new(10, "DELIVERY", "NOT_SENT", "Campaign ready is false. Green does not send."),
            new(11, "REJECTION_TAXONOMY", rejections.Count == 0 ? "NONE" : "RECORDED",
                rejections.Count == 0
                    ? "No stored rejection code applies yet."
                    : string.Join(", ", rejections.Select(item => item.Code + " on " + item.SubjectId)) + ". Rejected work is not a positive reference."),
            new(12, "REGRESSION_SUITE", regression.Status, regression.Notice),
            new(13, "PROVENANCE_AND_COST", "UNRECORDED",
                "Provenance, rights, approving authority, usage, and cost are unrecorded. Reference assets sent to a provider: "
                + (assetsSent ? "Yes" : "No") + ". A model was not called to re-analyze stored quality.")
        };

        return new CreativeAcceptanceVoyage(
            jobStatus == "READY_FOR_GENERATION" ? "READY_FOR_GENERATION" : "BLOCKED",
            Open,
            campaign,
            intelligence,
            brief,
            brandDna,
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
            new AcceptanceEvidence(
                "NOT_RUN",
                firstGateComplete
                    ? "Brand DNA is prepared from the advertiser brief. Compliance waits for a finished image."
                    : "No new Brand DNA or finished image exists to compare."),
            new AcceptanceEvidence(
                active.Any(item => !string.IsNullOrWhiteSpace(item.DoNotCopy)) ? "NOT_RUN" : "BLOCKED",
                active.Any(item => !string.IsNullOrWhiteSpace(item.DoNotCopy))
                    ? "A finished image is required before compliance can be judged."
                    : "No ACTIVE reference has stored DO NOT COPY intelligence."),
            new AcceptanceEvidence("NOT_REQUESTED", "No human approval was claimed."),
            rejections,
            regression,
            provenance,
            storedQuality,
            assetsSent,
            ReferenceLibrary.QualityDna,
            blockers.Distinct(StringComparer.Ordinal).ToList(),
            trace,
            0,
            false,
            "NOT_SENT",
            jobStatus == "READY_FOR_GENERATION"
                ? "The voyage is authorized to generate one original advertisement. Generation has not started. ACA-8 is not accepted. Delivery remains NOT_SENT."
                : "The voyage stopped at the first incomplete acceptance gate. Refusal to invent missing intelligence is a pass condition, but ACA-8 is not accepted. Delivery remains NOT_SENT.");
    }

    /// <summary>
    /// Records deterministic visual-release evidence after a generated image exists.
    /// A passing result only advances the draft to human review; it never authorizes delivery.
    /// </summary>
    public static CreativeAcceptanceVoyage ApplyVisualQualityEvidence(
        CreativeAcceptanceVoyage voyage,
        int reportedScore,
        IEnumerable<string>? defectCodes,
        bool visualEvidenceRecorded,
        bool brandDnaCompliant,
        bool originalityConfirmed,
        bool inventoryGeometryVerified,
        bool qrVerified)
    {
        ArgumentNullException.ThrowIfNull(voyage);
        if (voyage.FinishedCreative.Status != "GENERATED_PENDING_REVIEW")
        {
            throw new InvalidOperationException(
                "Visual release evidence requires a stored generated draft. None was invented.");
        }

        if (voyage.HumanReview.Status is "APPROVE" or "REJECT")
        {
            throw new InvalidOperationException(
                "Visual QA cannot overwrite a recorded human decision. An explicit reopen workflow is required.");
        }

        var decision = GlobalVisualDnaGate.Evaluate(
            reportedScore,
            defectCodes,
            visualEvidenceRecorded,
            brandDnaCompliant,
            originalityConfirmed,
            inventoryGeometryVerified,
            qrVerified);
        var blockers = voyage.Blockers
            .Where(item => item != "VISUAL_QUALITY_QA_REQUIRED"
                && !item.StartsWith("GLOBAL_VISUAL_DNA_", StringComparison.Ordinal)
                && !item.StartsWith("VISUAL_DEFECT_", StringComparison.Ordinal))
            .ToList();
        if (decision.Status != GlobalVisualDnaGate.Pass)
        {
            blockers.Add("VISUAL_QUALITY_QA_REQUIRED");
            blockers.Add("GLOBAL_VISUAL_DNA_" + decision.Status);
            blockers.AddRange(decision.BlockingDefects.Select(code => "VISUAL_DEFECT_" + code));
        }

        var trace = ReplaceSteps(
            voyage.Trace,
            new AcceptanceTraceStep(
                7,
                "QUALITY_QA",
                decision.Status,
                decision.Notice + " Score: " + decision.ReportedScore
                + ". Blocking defects: "
                + (decision.BlockingDefects.Count == 0 ? "none" : string.Join(", ", decision.BlockingDefects))
                + ". Campaign ready remains false; delivery remains NOT_SENT."));
        return voyage with
        {
            Status = decision.Status == GlobalVisualDnaGate.Pass ? "AWAITING_REVIEW" : "BLOCKED",
            QualityQa = new AcceptanceEvidence(decision.Status, trace.Single(item => item.Sequence == 7).Evidence),
            Blockers = blockers.Distinct(StringComparer.Ordinal).ToList(),
            Trace = trace,
            CampaignReady = false,
            Delivery = "NOT_SENT",
            Notice = decision.Status == GlobalVisualDnaGate.Pass
                ? "Visual release checks passed. The draft is still awaiting human review and recorded usage cost. Delivery remains NOT_SENT."
                : "Visual release checks did not pass. Repair or record missing evidence before human approval. Delivery remains NOT_SENT."
        };
    }

    /// <summary>
    /// Records an explicit human decision after visual QA has passed.
    /// Approval remains pending separate release authorization and never sends.
    /// </summary>
    public static CreativeAcceptanceVoyage RecordHumanReview(
        CreativeAcceptanceVoyage voyage,
        string reviewerId,
        string decision,
        string notes,
        DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(voyage);
        if (!string.Equals(voyage.Status, "AWAITING_REVIEW", StringComparison.Ordinal)
            || !string.Equals(voyage.FinishedCreative.Status, "GENERATED_PENDING_REVIEW", StringComparison.Ordinal)
            || !string.Equals(voyage.QualityQa.Status, GlobalVisualDnaGate.Pass, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Human review requires a stored draft that passed visual QA and is awaiting review.");
        }

        if (string.IsNullOrWhiteSpace(reviewerId) || reviewerId.Trim().Length > 128)
        {
            throw new InvalidOperationException("A verified reviewer identity of 1–128 characters is required.");
        }

        if (string.IsNullOrWhiteSpace(notes) || notes.Trim().Length > 2000)
        {
            throw new InvalidOperationException("Human review notes of 1–2000 characters are required.");
        }

        var normalizedDecision = decision?.Trim().ToUpperInvariant();
        if (normalizedDecision is not ("APPROVE" or "REJECT"))
        {
            throw new InvalidOperationException("Human review decision must be APPROVE or REJECT.");
        }

        var blockers = voyage.Blockers
            .Where(item => item is not "HUMAN_REVIEW_REQUIRED"
                and not "EXPLICIT_RELEASE_AUTHORIZATION_REQUIRED"
                and not "HUMAN_REVIEW_REJECTED")
            .ToList();

        if (normalizedDecision == "APPROVE")
        {
            if (blockers.Count > 0)
            {
                throw new InvalidOperationException(
                    "Human approval is blocked until all non-review acceptance blockers are cleared: "
                    + string.Join(", ", blockers));
            }

            blockers.Add("EXPLICIT_RELEASE_AUTHORIZATION_REQUIRED");
        }
        else
        {
            blockers.Add("HUMAN_REVIEW_REJECTED");
        }

        var status = normalizedDecision == "APPROVE"
            ? "HUMAN_APPROVED_PENDING_RELEASE"
            : "HUMAN_REVIEW_REJECTED";
        var evidence = status + " by " + reviewerId.Trim()
            + " at " + reviewedAt.ToUniversalTime().ToString("O")
            + ". Notes: " + notes.Trim()
            + ". CampaignReady remains false; Delivery remains NOT_SENT.";
        return voyage with
        {
            Status = status,
            HumanReview = new AcceptanceEvidence(normalizedDecision, evidence),
            Blockers = blockers.Distinct(StringComparer.Ordinal).ToList(),
            Trace = ReplaceSteps(
                voyage.Trace,
                new AcceptanceTraceStep(9, "HUMAN_REVIEW", normalizedDecision, evidence),
                new AcceptanceTraceStep(10, "DELIVERY", "NOT_SENT",
                    "Human review does not authorize release. CampaignReady remains false; Delivery remains NOT_SENT.")),
            CampaignReady = false,
            Delivery = "NOT_SENT",
            Notice = normalizedDecision == "APPROVE"
                ? "Human review was recorded. Explicit release authorization is still required; delivery remains NOT_SENT."
                : "Human review rejected this draft. Repair and a new review are required; delivery remains NOT_SENT."
        };
    }

    public static CreativeAcceptanceVoyage WithGeneration(
        CreativeAcceptanceVoyage voyage,
        AcceptanceGenerationOutcome outcome,
        IReadOnlyList<AcademyReferenceRecord> references)
    {
        ArgumentNullException.ThrowIfNull(voyage);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(references);
        if (voyage.ProviderJob.Status != "READY_FOR_GENERATION")
        {
            throw new InvalidOperationException(
                "The voyage is not authorized to generate. None was invented.");
        }

        if (outcome.Succeeded
            && (outcome.ModelCalls <= 0
                || string.IsNullOrWhiteSpace(outcome.ImagePath)
                || string.IsNullOrWhiteSpace(outcome.JobId)))
        {
            throw new InvalidOperationException("No finished image was stored. None was invented.");
        }

        if (!outcome.Succeeded)
        {
            var failedJob = voyage.ProviderJob with
            {
                Status = "FAILED",
                JobId = string.IsNullOrWhiteSpace(outcome.JobId) ? "NOT_STARTED" : outcome.JobId.Trim(),
                Attempts = 1,
                ModelCalls = 0,
                CostStatus = string.IsNullOrWhiteSpace(outcome.CostStatus) ? "UNRECORDED" : outcome.CostStatus.Trim(),
                Cost = outcome.Cost,
                Currency = outcome.Currency ?? string.Empty,
                Notice = outcome.Notice.Trim()
                    + " Reference image files were not sent. A rejection code was not invented for a provider failure."
            };
            return voyage with
            {
                Status = "BLOCKED",
                AmendmentStatus = Open,
                ProviderJob = failedJob,
                FinishedCreative = new AcceptanceEvidence(
                    "NOT_CREATED",
                    "No finished original advertisement was stored. A placeholder was not substituted."),
                ReferenceAssetsSentToProvider = false,
                ModelCalls = 0,
                CampaignReady = false,
                Delivery = "NOT_SENT",
                Trace = ReplaceSteps(
                    voyage.Trace,
                    new AcceptanceTraceStep(5, "ORIGINAL_GENERATION", "FAILED", failedJob.Notice),
                    new AcceptanceTraceStep(7, "QUALITY_QA", "NOT_RUN", "No finished image exists. A visual score was not invented."),
                    new AcceptanceTraceStep(9, "HUMAN_REVIEW", "NOT_REQUESTED", "No finished image exists for a human to review.")),
                Notice = "The provider did not store a finished image. ACA-8 is not accepted. Delivery remains NOT_SENT."
            };
        }

        var jobId = outcome.JobId.Trim();
        var imagePath = outcome.ImagePath.Trim();
        var originality = ReferenceLibrary.Compare(
            voyage.ProductionBrief.BrandName,
            voyage.ProductionBrief.Headline,
            string.Empty,
            string.Empty,
            references);
        var rejections = voyage.Rejections.ToList();
        var copied = originality.Status is "REGENERATE" or "RECOMPOSE";
        if (copied)
        {
            rejections.Add(ReferenceLibrary.Reject("REFERENCE_TOO_SIMILAR", jobId));
        }

        var geometryReady = voyage.InventoryPreflight.Status == "RECOMPOSED";
        var status = geometryReady && originality.Status == "PASS" && !copied
            ? "AWAITING_REVIEW"
            : "BLOCKED";
        var costStatus = string.IsNullOrWhiteSpace(outcome.CostStatus) ? "UNRECORDED" : outcome.CostStatus.Trim();
        var job = voyage.ProviderJob with
        {
            Status = "GENERATED",
            JobId = jobId,
            Attempts = 1,
            ModelCalls = outcome.ModelCalls,
            CostStatus = costStatus,
            Cost = outcome.Cost,
            Currency = outcome.Currency ?? string.Empty,
            Notice = "The provider stored one draft from the text recipe. Reference image files were not sent. "
                + (costStatus == "UNRECORDED" ? "Usage cost was not invented. " : "Provider cost was recorded. ")
                + outcome.Notice.Trim()
        };
        var finished = new AcceptanceEvidence(
            "GENERATED_PENDING_REVIEW",
            "One original draft is stored at " + imagePath
            + ". It is not campaign ready and not a contract. Reference image files were not sent.");
        var inventoryQa = geometryReady
            ? new AcceptanceEvidence(
                "RECORDED",
                "Purchased slot width and height are on the adaptation. The Academy prototype was not scaled. The provider draft was not resized into a second placement file; those pixels were not invented.")
            : new AcceptanceEvidence("BLOCKED", voyage.InventoryPreflight.Notice);
        var brandCompliance = originality.Status == "PASS"
            ? new AcceptanceEvidence(
                "PASS",
                "The stored brand and headline do not reproduce a reference identity. This is not a visual score.")
            : new AcceptanceEvidence(copied ? "FAIL" : "BLOCKED", originality.Notice);
        var doNotCopy = originality.Status == "PASS"
            ? new AcceptanceEvidence("PASS", originality.Notice)
            : new AcceptanceEvidence(copied ? "FAIL" : "BLOCKED", originality.Notice);
        var blockers = voyage.Blockers
            .Where(item => item is not "ACTIVE_REFERENCE_INTELLIGENCE_REQUIRED"
                and not "ACTIVE_REFERENCE_RETRIEVAL_REQUIRED"
                and not "PROVIDER_CONFIGURATION_REQUIRED"
                and not "INVENTORY_GEOMETRY_REQUIRED")
            .ToList();
        if (!geometryReady)
        {
            blockers.Add("INVENTORY_GEOMETRY_REQUIRED");
        }

        if (outcome.Cost is not null && costStatus != "UNRECORDED")
        {
            blockers.Remove("USAGE_COST_UNRECORDED");
        }

        var notice = status == "AWAITING_REVIEW"
            ? "One draft is stored and awaits visual QA and human review. ACA-8 is not accepted. Campaign ready is false. Delivery remains NOT_SENT."
            : copied
                ? "The draft reproduces stored do-not-copy intelligence and was rejected. The failed attempt is kept. ACA-8 is not accepted. Delivery remains NOT_SENT."
                : "The draft is stored, but an acceptance gate is still incomplete. ACA-8 is not accepted. Delivery remains NOT_SENT.";
        return voyage with
        {
            Status = status,
            AmendmentStatus = Open,
            ProviderJob = job,
            FinishedCreative = finished,
            Originality = originality,
            InventoryQa = inventoryQa,
            BrandDnaCompliance = brandCompliance,
            DoNotCopyCompliance = doNotCopy,
            QualityQa = new AcceptanceEvidence(
                "NOT_RUN",
                "A visual score was not invented. The draft awaits a human benchmark."),
            HumanReview = new AcceptanceEvidence(
                "NOT_REQUESTED",
                "Human review was not claimed. The draft is stored for a later review."),
            Rejections = rejections,
            ReferenceAssetsSentToProvider = false,
            Blockers = blockers.Distinct(StringComparer.Ordinal).ToList(),
            ModelCalls = outcome.ModelCalls,
            CampaignReady = false,
            Delivery = "NOT_SENT",
            Trace = ReplaceSteps(
                voyage.Trace,
                new AcceptanceTraceStep(5, "ORIGINAL_GENERATION", "GENERATED", job.Notice),
                new AcceptanceTraceStep(7, "QUALITY_QA", "NOT_RUN", "A visual score was not invented. The draft awaits a human benchmark."),
                new AcceptanceTraceStep(8, "ORIGINALITY_QA", originality.Status, originality.Notice),
                new AcceptanceTraceStep(9, "HUMAN_REVIEW", "NOT_REQUESTED", "Human review was not claimed."),
                new AcceptanceTraceStep(11, "REJECTION_TAXONOMY", rejections.Count == 0 ? "NONE" : "RECORDED",
                    rejections.Count == 0
                        ? "No stored rejection code applies yet."
                        : string.Join(", ", rejections.Select(item => item.Code + " on " + item.SubjectId))
                          + ". Rejected work is not a positive reference.")),
            Notice = notice
        };
    }

    private static IReadOnlyList<AcceptanceTraceStep> ReplaceSteps(
        IReadOnlyList<AcceptanceTraceStep> trace,
        params AcceptanceTraceStep[] replacements)
    {
        var mapped = replacements.ToDictionary(item => item.Sequence);
        return trace.Select(step => mapped.TryGetValue(step.Sequence, out var replacement) ? replacement : step).ToList();
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
