using Bliss.Domain.AdvertisingRealEstate;
using Bliss.Domain.CreativeAcademy;

namespace Bliss.Tests.Academy;

public sealed class CreativeAcceptanceVoyageTests
{
    [Fact]
    public void Candidate_files_do_not_cross_the_reference_gate()
    {
        var references = Enumerable.Range(1, 50)
            .Select(number => new AcademyReferenceRecord(
                $"ACA-{number:000}-V1",
                number,
                number == 1 ? "pharmacy" : "other-" + number,
                number == 1 ? "Pharmacy" : "Other",
                $"Reference-{number}.jpeg",
                ReferenceLibrary.Candidate,
                ReferenceLibrary.Uploaded,
                number != 5,
                "",
                ""))
            .ToList();
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var campaign = new AcceptanceCampaignBrief(
            "one-voyage-test",
            "Harborlight Pharmacy",
            "Panama City",
            "Panama",
            "pharmacy",
            "Introduce prescription pickup",
            "ARE-P01");

        var voyage = CreativeAcceptance.Run(
            campaign,
            references,
            product,
            board.Slots,
            "OpenAI",
            "gpt-image-1",
            true);

        Assert.Equal("BLOCKED", voyage.Status);
        Assert.Equal(CreativeAcceptance.Open, voyage.AmendmentStatus);
        Assert.Equal(0, voyage.ReferenceIntelligence.Active);
        Assert.Equal("REFERENCE_INTELLIGENCE_INCOMPLETE", voyage.ReferenceIntelligence.Status);
        Assert.Equal("NICHE_REFERENCE_NOT_ACTIVE", voyage.ProductionBrief.Retrieval.Status);
        Assert.Equal("NOT_CREATED", voyage.BrandDna.Status);
        Assert.Equal("BLOCKED_REFERENCE_GATE", voyage.ProviderJob.Status);
        Assert.Equal("NOT_STARTED", voyage.ProviderJob.JobId);
        Assert.Equal(0, voyage.ProviderJob.ModelCalls);
        Assert.Equal("NOT_CREATED", voyage.FinishedCreative.Status);
        Assert.Equal("GEOMETRY_UNRECORDED", voyage.InventoryPreflight.Status);
        Assert.Equal("HUMAN REVIEW", voyage.Originality.Status);
        Assert.Equal("NOT_RUN", voyage.QualityQa.Status);
        Assert.Equal("NOT_REQUESTED", voyage.HumanReview.Status);
        Assert.Contains("ACTIVE_REFERENCE_INTELLIGENCE_REQUIRED", voyage.Blockers);
        Assert.Contains("INVENTORY_GEOMETRY_REQUIRED", voyage.Blockers);
        Assert.Contains("USAGE_COST_UNRECORDED", voyage.Blockers);
        Assert.Equal(13, voyage.Trace.Count);
        Assert.Equal("INVENTORY_GEOMETRY_FAILURE", voyage.Rejections.Single().Code);
        Assert.False(voyage.Rejections.Single().PositiveReference);
        Assert.Equal("BASELINE_NOT_RECORDED", voyage.Regression.Status);
        Assert.False(voyage.Regression.Passed);
        Assert.Equal(12, voyage.Regression.Cases.Count);
        Assert.All(voyage.Provenance, item => Assert.Equal("NOT_AUTHORIZED", item.PermittedProviderUse));
        Assert.False(voyage.ReferenceAssetsSentToProvider);
        Assert.Equal("UNCLASSIFIED", voyage.StoredQuality.Single().Status);
        Assert.Equal(0, voyage.StoredQuality.Single().ModelCalls);
        Assert.Equal("GQD-1", voyage.ProductionBrief.QualityDnaVersion);
        Assert.Equal(ReferenceLibrary.QualityDna.Count, voyage.GlobalQualityDna.Count);
        Assert.False(voyage.CampaignReady);
        Assert.Equal(0, voyage.ModelCalls);
        Assert.Equal("NOT_SENT", voyage.Delivery);
    }

    [Fact]
    public void A_reference_identity_is_not_accepted_as_the_new_advertiser()
    {
        var board = RealEstateCatalog.Board();
        var campaign = new AcceptanceCampaignBrief(
            "copied-voyage",
            "VidaCare Pharmacy",
            "Panama City",
            "Panama",
            "pharmacy",
            "Introduce prescription pickup",
            "ARE-P01");

        var error = Assert.Throws<InvalidOperationException>(() => CreativeAcceptance.Run(
            campaign,
            [],
            board.Products.Single(item => item.ProductId == "ARE-P01"),
            board.Slots,
            "",
            "",
            false));

        Assert.Contains("reference identity", error.Message);
    }

    [Fact]
    public void Retrieved_intelligence_and_recorded_slots_authorize_one_generation()
    {
        var board = RealEstateCatalog.Board();
        var product = board.Products.Single(item => item.ProductId == "ARE-P01");
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();
        var references = AuthorizedReferences("VidaCare");
        references.Add(Active("ACA-010-V1", 10, "motorcycle", "Motorcycle", "", ""));

        var voyage = CreativeAcceptance.Run(
            Harborlight(),
            references,
            product,
            slots,
            "OpenAI",
            "gpt-image-1",
            true);

        Assert.Equal("READY_FOR_GENERATION", voyage.Status);
        Assert.Equal("READY", voyage.ReferenceIntelligence.Status);
        Assert.Equal(4, voyage.ReferenceIntelligence.Active);
        Assert.Equal(3, voyage.ProductionBrief.Retrieval.Selected.Count);
        Assert.Contains(voyage.ProductionBrief.Retrieval.Selected, item =>
            item.ReferenceId == "ACA-001-V1" && item.Reason == "niche match" && item.Learn == "premium product lighting");
        Assert.Contains(voyage.ProductionBrief.Retrieval.Selected, item =>
            item.ReferenceId == "ACA-006-V1" && item.Reason == "lighting and depth");
        Assert.Contains(voyage.ProductionBrief.Retrieval.Selected, item =>
            item.ReferenceId == "ACA-008-V1" && item.Reason == "typography");
        Assert.DoesNotContain(voyage.ProductionBrief.Retrieval.Selected, item => item.ReferenceId == "ACA-010-V1");
        Assert.Equal("PREPARED", voyage.BrandDna.Status);
        Assert.Contains("not a reference brand", voyage.BrandDna.Notice);
        Assert.Equal("READY_FOR_GENERATION", voyage.ProviderJob.Status);
        Assert.Equal("NOT_STARTED", voyage.ProviderJob.JobId);
        Assert.Equal(0, voyage.ProviderJob.ModelCalls);
        Assert.Equal("NOT_CREATED", voyage.FinishedCreative.Status);
        Assert.Equal("RECOMPOSED", voyage.InventoryPreflight.Status);
        Assert.False(voyage.InventoryPreflight.ScaledFromPrototype);
        Assert.Equal(180, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "LEFT_VERTICAL").Width);
        Assert.Equal(640, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "LEFT_VERTICAL").Height);
        Assert.Equal(1280, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "BOTTOM_FULL").Width);
        Assert.Equal(160, voyage.InventoryPreflight.Slots.Single(item => item.SlotId == "BOTTOM_FULL").Height);
        Assert.False(voyage.ReferenceAssetsSentToProvider);
        Assert.False(voyage.CampaignReady);
        Assert.Equal("NOT_SENT", voyage.Delivery);

        var reviewed = CreativeAcceptance.WithGeneration(
            voyage,
            new AcceptanceGenerationOutcome(
                true,
                "job-harborlight-1",
                "/operations/generated/harborlight.png",
                1,
                "UNRECORDED",
                null,
                "",
                "One draft was stored."),
            references);

        Assert.Equal("AWAITING_REVIEW", reviewed.Status);
        Assert.Equal(CreativeAcceptance.Open, reviewed.AmendmentStatus);
        Assert.Equal("GENERATED", reviewed.ProviderJob.Status);
        Assert.Equal(1, reviewed.ProviderJob.ModelCalls);
        Assert.Equal("UNRECORDED", reviewed.ProviderJob.CostStatus);
        Assert.Null(reviewed.ProviderJob.Cost);
        Assert.Equal("GENERATED_PENDING_REVIEW", reviewed.FinishedCreative.Status);
        Assert.Contains("/operations/generated/harborlight.png", reviewed.FinishedCreative.Notice);
        Assert.Equal("PASS", reviewed.Originality.Status);
        Assert.Equal("RECORDED", reviewed.InventoryQa.Status);
        Assert.Equal("PASS", reviewed.BrandDnaCompliance.Status);
        Assert.Equal("PASS", reviewed.DoNotCopyCompliance.Status);
        Assert.Equal("NOT_RUN", reviewed.QualityQa.Status);
        Assert.Equal("NOT_REQUESTED", reviewed.HumanReview.Status);
        Assert.False(reviewed.ReferenceAssetsSentToProvider);
        Assert.Contains("VISUAL_QUALITY_QA_REQUIRED", reviewed.Blockers);
        Assert.Contains("HUMAN_REVIEW_REQUIRED", reviewed.Blockers);
        Assert.Contains("USAGE_COST_UNRECORDED", reviewed.Blockers);
        Assert.DoesNotContain("ACTIVE_REFERENCE_INTELLIGENCE_REQUIRED", reviewed.Blockers);
        Assert.Empty(reviewed.Rejections);
        Assert.Equal(1, reviewed.ModelCalls);
        Assert.False(reviewed.CampaignReady);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
        Assert.Equal(13, reviewed.Trace.Count);
    }

    [Fact]
    public void A_copied_draft_is_rejected_and_kept()
    {
        var board = RealEstateCatalog.Board();
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();
        var references = AuthorizedReferences("Harborlight");
        var voyage = CreativeAcceptance.Run(
            Harborlight(),
            references,
            board.Products.Single(item => item.ProductId == "ARE-P01"),
            slots,
            "OpenAI",
            "gpt-image-1",
            true);

        var reviewed = CreativeAcceptance.WithGeneration(
            voyage,
            new AcceptanceGenerationOutcome(
                true,
                "job-copied-1",
                "/operations/generated/copied.png",
                1,
                "UNRECORDED",
                null,
                "",
                "One draft was stored."),
            references);

        Assert.Equal("BLOCKED", reviewed.Status);
        Assert.Equal("REGENERATE", reviewed.Originality.Status);
        Assert.Equal("GENERATED_PENDING_REVIEW", reviewed.FinishedCreative.Status);
        Assert.Contains("/operations/generated/copied.png", reviewed.FinishedCreative.Notice);
        Assert.Equal("REFERENCE_TOO_SIMILAR", reviewed.Rejections.Single().Code);
        Assert.Equal("job-copied-1", reviewed.Rejections.Single().SubjectId);
        Assert.False(reviewed.Rejections.Single().PositiveReference);
        Assert.Equal("FAIL", reviewed.DoNotCopyCompliance.Status);
        Assert.False(reviewed.CampaignReady);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
    }

    [Fact]
    public void Missing_geometry_does_not_authorize_generation()
    {
        var board = RealEstateCatalog.Board();
        var voyage = CreativeAcceptance.Run(
            Harborlight(),
            AuthorizedReferences("VidaCare"),
            board.Products.Single(item => item.ProductId == "ARE-P01"),
            board.Slots,
            "OpenAI",
            "gpt-image-1",
            true);

        Assert.Equal("BLOCKED", voyage.Status);
        Assert.Equal("PREPARED", voyage.BrandDna.Status);
        Assert.Equal("BLOCKED_INVENTORY_GEOMETRY", voyage.ProviderJob.Status);
        Assert.Equal("GEOMETRY_UNRECORDED", voyage.InventoryPreflight.Status);
        Assert.Equal("NOT_CREATED", voyage.FinishedCreative.Status);
        Assert.Equal(0, voyage.ModelCalls);
        var error = Assert.Throws<InvalidOperationException>(() => CreativeAcceptance.WithGeneration(
            voyage,
            new AcceptanceGenerationOutcome(true, "job", "/operations/generated/x.png", 1, "UNRECORDED", null, "", "stored"),
            AuthorizedReferences("VidaCare")));
        Assert.Contains("not authorized to generate", error.Message);
    }

    [Fact]
    public void A_provider_failure_does_not_invent_a_finished_ad_or_a_rejection()
    {
        var board = RealEstateCatalog.Board();
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();
        var references = AuthorizedReferences("VidaCare");
        var voyage = CreativeAcceptance.Run(
            Harborlight(),
            references,
            board.Products.Single(item => item.ProductId == "ARE-P01"),
            slots,
            "OpenAI",
            "gpt-image-1",
            true);

        var failed = CreativeAcceptance.WithGeneration(
            voyage,
            new AcceptanceGenerationOutcome(
                false,
                "",
                "",
                0,
                "UNRECORDED",
                null,
                "",
                "The configured image provider could not be reached. No advertisement was stored."),
            references);

        Assert.Equal("BLOCKED", failed.Status);
        Assert.Equal("FAILED", failed.ProviderJob.Status);
        Assert.Equal("NOT_CREATED", failed.FinishedCreative.Status);
        Assert.Empty(failed.Rejections);
        Assert.Equal(0, failed.ModelCalls);
        Assert.False(failed.ReferenceAssetsSentToProvider);
        Assert.False(failed.CampaignReady);
        Assert.Equal("NOT_SENT", failed.Delivery);
    }

    [Fact]
    public void Visual_pass_then_human_approval_records_reviewer_but_never_releases_campaign()
    {
        var reviewed = ReadyForHumanReview(costRecorded: true);
        var approved = CreativeAcceptance.RecordHumanReview(
            reviewed, "reviewer-123", "APPROVE",
            "Originality, brand DNA, product geometry, and QR destination reviewed.",
            new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("HUMAN_APPROVED_PENDING_RELEASE", approved.Status);
        Assert.Equal("APPROVE", approved.HumanReview.Status);
        Assert.Contains("reviewer-123", approved.HumanReview.Notice);
        Assert.Contains("EXPLICIT_RELEASE_AUTHORIZATION_REQUIRED", approved.Blockers);
        Assert.DoesNotContain("HUMAN_REVIEW_REQUIRED", approved.Blockers);
        Assert.False(approved.CampaignReady);
        Assert.Equal("NOT_SENT", approved.Delivery);
        Assert.Equal("APPROVE", approved.Trace.Single(item => item.Sequence == 9).Status);
        Assert.Equal("NOT_SENT", approved.Trace.Single(item => item.Sequence == 10).Status);
    }

    [Fact]
    public void Human_rejection_is_recorded_and_keeps_delivery_blocked()
    {
        var reviewed = ReadyForHumanReview(costRecorded: false);
        var rejected = CreativeAcceptance.RecordHumanReview(
            reviewed, "reviewer-456", "REJECT",
            "QR destination could not be verified against the approved campaign.", DateTimeOffset.UtcNow);

        Assert.Equal("HUMAN_REVIEW_REJECTED", rejected.Status);
        Assert.Equal("REJECT", rejected.HumanReview.Status);
        Assert.Contains("HUMAN_REVIEW_REJECTED", rejected.Blockers);
        Assert.Contains("USAGE_COST_UNRECORDED", rejected.Blockers);
        Assert.False(rejected.CampaignReady);
        Assert.Equal("NOT_SENT", rejected.Delivery);
    }

    [Fact]
    public void Human_approval_is_refused_when_usage_cost_is_unrecorded()
    {
        var reviewed = ReadyForHumanReview(costRecorded: false);
        var error = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcceptance.RecordHumanReview(
                reviewed, "reviewer-123", "APPROVE", "Looks good.", DateTimeOffset.UtcNow));

        Assert.Contains("acceptance blockers", error.Message);
        Assert.False(reviewed.CampaignReady);
        Assert.Equal("NOT_SENT", reviewed.Delivery);
    }

    [Fact]
    public void Human_review_requires_a_reviewer_identity_and_notes()
    {
        var reviewed = ReadyForHumanReview(costRecorded: true);
        var noIdentity = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcceptance.RecordHumanReview(reviewed, " ", "APPROVE", "Reviewed", DateTimeOffset.UtcNow));
        Assert.Contains("reviewer identity", noIdentity.Message);
        var noNotes = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcceptance.RecordHumanReview(reviewed, "reviewer-123", "APPROVE", " ", DateTimeOffset.UtcNow));
        Assert.Contains("review notes", noNotes.Message);
    }

    [Fact]
    public void Human_review_cannot_be_recorded_before_visual_quality_passes()
    {
        var board = RealEstateCatalog.Board();
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();
        var references = AuthorizedReferences("VidaCare");
        var started = CreativeAcceptance.Run(
            Harborlight(), references, board.Products.Single(item => item.ProductId == "ARE-P01"),
            slots, "OpenAI", "gpt-image-1", true);
        var generated = CreativeAcceptance.WithGeneration(
            started,
            new AcceptanceGenerationOutcome(true, "job-review-gate", "/operations/generated/review.png",
                1, "RECORDED", 0.05m, "USD", "Stored draft."), references);

        var error = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcceptance.RecordHumanReview(generated, "reviewer-123", "APPROVE", "Reviewed", DateTimeOffset.UtcNow));
        Assert.Contains("passed visual QA", error.Message);
        Assert.False(generated.CampaignReady);
        Assert.Equal("NOT_SENT", generated.Delivery);
    }

    [Fact]
    public void Visual_qa_cannot_overwrite_a_recorded_human_decision()
    {
        var ready = ReadyForHumanReview(costRecorded: true);
        var approved = CreativeAcceptance.RecordHumanReview(
            ready, "reviewer-123", "APPROVE", "Approved after review.", DateTimeOffset.UtcNow);

        var approvalError = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcceptance.ApplyVisualQualityEvidence(approved, 100, [], true, true, true, true, true));

        Assert.Contains("cannot overwrite a recorded human decision", approvalError.Message);
        Assert.Equal("APPROVE", approved.HumanReview.Status);
        Assert.False(approved.CampaignReady);
        Assert.Equal("NOT_SENT", approved.Delivery);

        var rejectedReady = ReadyForHumanReview(costRecorded: false);
        var rejected = CreativeAcceptance.RecordHumanReview(
            rejectedReady, "reviewer-456", "REJECT", "Rejected after review.", DateTimeOffset.UtcNow);
        var rejectionError = Assert.Throws<InvalidOperationException>(() =>
            CreativeAcceptance.ApplyVisualQualityEvidence(rejected, 100, [], true, true, true, true, true));

        Assert.Contains("cannot overwrite a recorded human decision", rejectionError.Message);
        Assert.Equal("REJECT", rejected.HumanReview.Status);
        Assert.False(rejected.CampaignReady);
        Assert.Equal("NOT_SENT", rejected.Delivery);
    }

    private static CreativeAcceptanceVoyage ReadyForHumanReview(bool costRecorded)
    {
        var board = RealEstateCatalog.Board();
        var slots = board.Slots.Select(slot => slot.SlotId switch
        {
            "LEFT_VERTICAL" => slot with { Width = 180, Height = 640 },
            "BOTTOM_FULL" => slot with { Width = 1280, Height = 160 },
            _ => slot
        }).ToList();
        var references = AuthorizedReferences("VidaCare");
        var started = CreativeAcceptance.Run(
            Harborlight(), references, board.Products.Single(item => item.ProductId == "ARE-P01"),
            slots, "OpenAI", "gpt-image-1", true);
        var generated = CreativeAcceptance.WithGeneration(
            started,
            new AcceptanceGenerationOutcome(
                true, "job-human-review", "/operations/generated/human-review.png", 1,
                costRecorded ? "RECORDED" : "UNRECORDED",
                costRecorded ? 0.05m : null, costRecorded ? "USD" : string.Empty, "Stored draft."),
            references);
        return CreativeAcceptance.ApplyVisualQualityEvidence(
            generated, 100, [], true, true, true, true, true);
    }

    private static AcceptanceCampaignBrief Harborlight() =>
        new(
            "one-voyage-authorized",
            "Harborlight Pharmacy",
            "Panama City",
            "Panama",
            "pharmacy",
            "Introduce prescription pickup",
            "ARE-P01");

    private static List<AcademyReferenceRecord> AuthorizedReferences(string pharmacyDoNotCopy)
    {
        var references = new List<AcademyReferenceRecord>
        {
            Active("ACA-001-V1", 1, "pharmacy", "Pharmacy", "premium product lighting", pharmacyDoNotCopy),
            Active("ACA-006-V1", 6, "restaurant", "Restaurant", "lighting and depth", "exact bay"),
            Active("ACA-008-V1", 8, "auto-repair", "Auto repair", "typography", "reference headline")
        };
        return references;
    }

    private static AcademyReferenceRecord Active(
        string referenceId,
        int number,
        string nicheKey,
        string nicheName,
        string learn,
        string doNotCopy) =>
        new(
            referenceId,
            number,
            nicheKey,
            nicheName,
            referenceId + ".jpeg",
            ReferenceLibrary.Active,
            ReferenceLibrary.Uploaded,
            true,
            learn,
            doNotCopy);
}
