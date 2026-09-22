using System.Text.Json.Nodes;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerCreativeDepartmentValidationTests
{
    private static readonly Guid ConceptId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid BrandId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ColorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ResearchId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly IReadOnlySet<string> PaletteRoles =
        new HashSet<string>(StringComparer.Ordinal) { "primary", "secondary", "accent", "background" };

    private static readonly IReadOnlyList<CanonicalCreativeFactualClaim> PreservedClaims =
    [
        new("Couples often research venues months ahead.", ["src_1"])
    ];

    [Fact]
    public void Brief_enforces_formats_variant_count_and_job_kind_rules()
    {
        Assert.Throws<InvalidOperationException>(() => CanonicalBrief(formats: ["NOPE"]));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(formats: [WeddingPlannerChannelFormats.StaticSocialSquare, WeddingPlannerChannelFormats.StaticSocialSquare]));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(
                formats:
                [
                    WeddingPlannerChannelFormats.StaticSocialSquare,
                    WeddingPlannerChannelFormats.StaticSocialStory,
                    WeddingPlannerChannelFormats.StaticDisplayBanner
                ],
                requestedVariantCount: 2));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(jobKind: WeddingPlannerCreativeProductionJobKinds.Initial, revisionNotes: "nope"));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(
                jobKind: WeddingPlannerCreativeProductionJobKinds.Revision,
                revisionParentId: null,
                revisionNotes: "notes"));
        var ok = CanonicalBrief();
        Assert.Equal(2, ok.RequestedVariantCount);
        Assert.Equal(2, ok.Formats.Count);
    }

    [Fact]
    public void Client_canvas_overrides_are_rejected()
    {
        Assert.Throws<InvalidOperationException>(() => CanonicalBrief(canvasWidth: 1080, canvasHeight: 1080));
    }

    [Fact]
    public void Forbidden_brief_fields_fail_closed()
    {
        var node = JsonNode.Parse("""{"objective":"x","campaignId":"c-1"}""")!;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.RejectForbiddenBriefFields(node));
        var nested = JsonNode.Parse("""{"ok":true,"nested":{"base64":"abc"}}""")!;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.RejectForbiddenBriefFields(nested));
        var overrideNode = JsonNode.Parse("""{"conceptOverride":"concept_2"}""")!;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.RejectForbiddenBriefFields(overrideNode));
    }

    [Fact]
    public void Stage_ownership_and_synthetic_marker_are_enforced()
    {
        var direction = WeddingPlannerCreativeDepartmentValidation.CanonicalizeCreativeDirectionOutput(
            CreativeDirectionJson(), WeddingPlannerConceptIds.Concept1, true);
        Assert.Equal(2, direction.Contributions.Count);

        var missingMarker = CreativeDirectionJson().Replace(
            "\"marker\":\"SYNTHETIC DEVELOPMENT CREATIVE PACKAGE\",",
            string.Empty,
            StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.CanonicalizeCreativeDirectionOutput(
                missingMarker, WeddingPlannerConceptIds.Concept1, true));

        var wrongConcept = CreativeDirectionJson().Replace("concept_1", "concept_2", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.CanonicalizeCreativeDirectionOutput(
                wrongConcept, WeddingPlannerConceptIds.Concept1, true));
    }

    [Fact]
    public void Copy_system_preserves_only_exact_selected_concept_claims()
    {
        var good = CopySystemJson(includePreserved: true);
        var stage = WeddingPlannerCreativeDepartmentValidation.CanonicalizeCopySystemOutput(
            good,
            WeddingPlannerConceptIds.Concept1,
            2,
            PreservedClaims,
            ForbiddenProvenance(),
            true);
        Assert.Equal(3, stage.Contributions.Count);

        var invented = good.Replace(
            "Couples often research venues months ahead.",
            "Invented claim text.",
            StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.CanonicalizeCopySystemOutput(
                invented, WeddingPlannerConceptIds.Concept1, 2, PreservedClaims, ForbiddenProvenance(), true));

        var provenanceAsSource = good.Replace("\"src_1\"", $"\"{BrandId:D}\"", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.CanonicalizeCopySystemOutput(
                provenanceAsSource, WeddingPlannerConceptIds.Concept1, 2, PreservedClaims, ForbiddenProvenance(), true));
    }

    [Fact]
    public void Variant_production_requires_exact_variants_formats_and_canvas()
    {
        var brief = CanonicalBrief();
        var image = WeddingPlannerCreativeDepartmentValidation.CanonicalizeImageDirectionOutput(
            ImageDirectionJson(), WeddingPlannerConceptIds.Concept1, 2, true);
        var copy = WeddingPlannerCreativeDepartmentValidation.CanonicalizeCopySystemOutput(
            CopySystemJson(includePreserved: false), WeddingPlannerConceptIds.Concept1, 2, PreservedClaims, ForbiddenProvenance(), true);

        var good = VariantProductionJson();
        var stage = WeddingPlannerCreativeDepartmentValidation.CanonicalizeVariantProductionOutput(
            good, WeddingPlannerConceptIds.Concept1, brief, image, copy, PaletteRoles, true);
        Assert.Single(stage.Contributions);
        Assert.Equal(2, stage.Contributions[0].Variants!.Count);

        var badCanvas = good.Replace("1080", "999", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerCreativeDepartmentValidation.CanonicalizeVariantProductionOutput(
                badCanvas, WeddingPlannerConceptIds.Concept1, brief, image, copy, PaletteRoles, true));
    }

    [Fact]
    public void Merge_requires_exact_thirteen_contributions_and_disclaimer()
    {
        var brief = CanonicalBrief();
        var stages = BuildAllStages(brief);
        var selected = SampleSelectedConcept();
        var plan = WeddingPlannerCreativeDepartmentValidation.MergePackagePlan(
            brief, selected, stages, Guid.NewGuid(), requireSyntheticMarker: true);
        Assert.Equal(13, plan.ContributionSummaries.Count);
        Assert.Equal(2, plan.Variants.Count);

        var assets = plan.Variants.Select(v => new CanonicalCreativeAssetRef(
            v.Id, Guid.NewGuid(), WeddingPlannerCreativeAssetContentTypes.ImagePng, 100, "abc", v.Width, v.Height)).ToList();
        var package = WeddingPlannerCreativeDepartmentValidation.FinalizePackageDocument(plan, assets);
        Assert.Contains(WeddingPlannerCreativePackageDisclaimer.Text, package.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerCreativeDepartmentMarkers.SyntheticDevelopmentCreativePackage, package.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(13, package.ContributionSummaries.Count);
        Assert.DoesNotContain("\"bytes\"", package.DocumentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", package.DocumentJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Exactly_thirteen_roles_and_six_profiles_are_locked()
    {
        Assert.Equal(13, WeddingPlannerCreativeDepartmentLogicalRoles.AllInOrder.Count);
        Assert.Equal(6, WeddingPlannerCreativeDepartmentWorkerProfiles.All.Count);
        Assert.DoesNotContain(
            WeddingPlannerConceptWorkshopLogicalRoles.BrandStrategist,
            WeddingPlannerCreativeDepartmentLogicalRoles.AllInOrder);
    }

    private static CanonicalCreativeProductionBrief CanonicalBrief(
        string jobKind = WeddingPlannerCreativeProductionJobKinds.Initial,
        IReadOnlyList<string>? formats = null,
        int requestedVariantCount = 2,
        Guid? revisionParentId = null,
        string? revisionNotes = null,
        int? canvasWidth = null,
        int? canvasHeight = null) =>
        WeddingPlannerCreativeDepartmentValidation.CanonicalizeBrief(
            jobKind,
            "Advance calm venue inquiries",
            formats ??
            [
                WeddingPlannerChannelFormats.StaticSocialSquare,
                WeddingPlannerChannelFormats.StaticSocialStory
            ],
            requestedVariantCount,
            revisionParentId,
            revisionNotes,
            ConceptId,
            WeddingPlannerConceptIds.Concept1,
            BrandId,
            1,
            ColorId,
            1,
            ResearchId,
            1,
            canvasWidth,
            canvasHeight,
            WeddingPlannerAiProviderKinds.Local,
            WeddingPlannerWorkers.LocalDeterministicV1,
            WeddingPlannerCreativeAssetProviderKinds.Local,
            WeddingPlannerCreativeAssetWorkers.LocalDeterministicV1);

    private static IReadOnlySet<string> ForbiddenProvenance() =>
        new HashSet<string>(StringComparer.Ordinal)
        {
            BrandId.ToString("D"),
            ColorId.ToString("D"),
            ResearchId.ToString("D"),
            ConceptId.ToString("D")
        };

    private static SelectedConceptSnapshot SampleSelectedConcept() =>
        new(
            WeddingPlannerConceptIds.Concept1,
            "Quiet Confidence",
            "Leads with understated brand voice.",
            "Soft natural light.",
            ["primary", "background", "accent"],
            new CanonicalCreativeCopy(WeddingPlannerCopyKinds.CreativeNonFactual, "H", "B", "C"),
            PreservedClaims);

    private static IReadOnlyList<CanonicalCreativeStageOutput> BuildAllStages(CanonicalCreativeProductionBrief brief)
    {
        var direction = WeddingPlannerCreativeDepartmentValidation.CanonicalizeCreativeDirectionOutput(
            CreativeDirectionJson(), brief.SelectedConceptId, true);
        var strategy = WeddingPlannerCreativeDepartmentValidation.CanonicalizeStrategyAdaptationOutput(
            StrategyAdaptationJson(brief.Formats), brief.SelectedConceptId, brief.Formats, true);
        var visual = WeddingPlannerCreativeDepartmentValidation.CanonicalizeVisualSystemOutput(
            VisualSystemJson(), brief.SelectedConceptId, PaletteRoles, true);
        var image = WeddingPlannerCreativeDepartmentValidation.CanonicalizeImageDirectionOutput(
            ImageDirectionJson(), brief.SelectedConceptId, brief.RequestedVariantCount, true);
        var copy = WeddingPlannerCreativeDepartmentValidation.CanonicalizeCopySystemOutput(
            CopySystemJson(false), brief.SelectedConceptId, brief.RequestedVariantCount, PreservedClaims, ForbiddenProvenance(), true);
        var variant = WeddingPlannerCreativeDepartmentValidation.CanonicalizeVariantProductionOutput(
            VariantProductionJson(), brief.SelectedConceptId, brief, image, copy, PaletteRoles, true);
        return [direction, strategy, visual, image, copy, variant];
    }

    private static string CreativeDirectionJson() => """
        {"schemaVersion":"creative-direction-worker-output.v1","workerProfileVersion":"CREATIVE_DIRECTION_V1","marker":"SYNTHETIC DEVELOPMENT CREATIVE PACKAGE","selectedConceptId":"concept_1","contributions":[{"logicalRole":"CREATIVE_DIRECTOR","summary":"North star.","direction":{"northStar":"Quiet confidence.","principles":["space","cta"]}},{"logicalRole":"CAMPAIGN_STRATEGIST","summary":"Framing.","framing":{"objectiveEcho":"Advance.","formatPlanNotes":"Cover formats."}}]}
        """;

    private static string StrategyAdaptationJson(IReadOnlyList<string> formats)
    {
        var formatsJson = string.Join(",", formats.Select(f => $"\"{f}\""));
        return
            "{\"schemaVersion\":\"strategy-adaptation-worker-output.v1\",\"workerProfileVersion\":\"STRATEGY_ADAPTATION_V1\"," +
            "\"marker\":\"SYNTHETIC DEVELOPMENT CREATIVE PACKAGE\",\"selectedConceptId\":\"concept_1\",\"contributions\":[" +
            "{\"logicalRole\":\"AUDIENCE_STRATEGIST\",\"summary\":\"Audience.\",\"audienceAdaptation\":{\"notes\":\"Couples.\"}}," +
            "{\"logicalRole\":\"OFFER_STRATEGIST\",\"summary\":\"Offer.\",\"offerFraming\":{\"notes\":\"Next step.\"}}," +
            "{\"logicalRole\":\"CHANNEL_STRATEGIST\",\"summary\":\"Channel.\",\"channelAdaptation\":{\"formats\":[" + formatsJson + "]}}]}";
    }

    private static string VisualSystemJson() => """
        {"schemaVersion":"visual-system-worker-output.v1","workerProfileVersion":"VISUAL_SYSTEM_V1","marker":"SYNTHETIC DEVELOPMENT CREATIVE PACKAGE","selectedConceptId":"concept_1","contributions":[{"logicalRole":"VISUAL_DESIGNER","summary":"Visual.","visualSystem":{"paletteRoleRefs":["primary","background","accent"],"atmosphere":"Soft."}},{"logicalRole":"LAYOUT_DESIGNER","summary":"Layout.","layoutSystem":{"geometryLanguage":"centered stack"}},{"logicalRole":"TYPOGRAPHY_DESIGNER","summary":"Type.","typographySystem":{"headlineRole":"display","bodyRole":"text","ctaRole":"label"}}]}
        """;

    private static string ImageDirectionJson() => """
        {"schemaVersion":"image-direction-worker-output.v1","workerProfileVersion":"IMAGE_DIRECTION_V1","marker":"SYNTHETIC DEVELOPMENT CREATIVE PACKAGE","selectedConceptId":"concept_1","contributions":[{"logicalRole":"IMAGE_PROMPT_DESIGNER","summary":"Prompts.","imagePrompts":[{"variantId":"variant_1","prompt":"Soft light.","negativeConstraints":["no logos"]},{"variantId":"variant_2","prompt":"Dusk wash.","negativeConstraints":["no faces"]}]}]}
        """;

    private static string CopySystemJson(bool includePreserved)
    {
        var claims = includePreserved
            ? "[{\"statement\":\"Couples often research venues months ahead.\",\"sourceIds\":[\"src_1\"]}]"
            : "[]";
        return
            "{\"schemaVersion\":\"copy-system-worker-output.v1\",\"workerProfileVersion\":\"COPY_SYSTEM_V1\"," +
            "\"marker\":\"SYNTHETIC DEVELOPMENT CREATIVE PACKAGE\",\"selectedConceptId\":\"concept_1\",\"contributions\":[" +
            "{\"logicalRole\":\"HEADLINE_SPECIALIST\",\"summary\":\"Headlines.\",\"headlines\":[{\"variantId\":\"variant_1\",\"text\":\"Your day.\"},{\"variantId\":\"variant_2\",\"text\":\"A calm next step.\"}]}," +
            "{\"logicalRole\":\"BODY_COPY_SPECIALIST\",\"summary\":\"Bodies.\",\"bodies\":[{\"variantId\":\"variant_1\",\"text\":\"Explore options.\",\"factualClaims\":[]},{\"variantId\":\"variant_2\",\"text\":\"Host with clarity.\",\"factualClaims\":" + claims + "}]}," +
            "{\"logicalRole\":\"CTA_SPECIALIST\",\"summary\":\"CTAs.\",\"ctas\":[{\"variantId\":\"variant_1\",\"text\":\"Start planning\"},{\"variantId\":\"variant_2\",\"text\":\"Continue\"}]}]}";
    }

    private static string VariantProductionJson() => """
        {"schemaVersion":"variant-production-worker-output.v1","workerProfileVersion":"VARIANT_PRODUCTION_V1","marker":"SYNTHETIC DEVELOPMENT CREATIVE PACKAGE","selectedConceptId":"concept_1","contributions":[{"logicalRole":"VARIANT_PRODUCER","summary":"Specs.","variants":[{"id":"variant_1","format":"STATIC_SOCIAL_SQUARE","canvas":{"width":1080,"height":1080},"refs":{"direction":"contributions.CREATIVE_DIRECTOR","visualSystem":"contributions.VISUAL_DESIGNER","layout":"contributions.LAYOUT_DESIGNER","typography":"contributions.TYPOGRAPHY_DESIGNER","imagePrompt":"imagePrompts.variant_1","headline":"headlines.variant_1","body":"bodies.variant_1","cta":"ctas.variant_1"},"paletteRoleRefs":["primary","background","accent"]},{"id":"variant_2","format":"STATIC_SOCIAL_STORY","canvas":{"width":1080,"height":1920},"refs":{"direction":"contributions.CREATIVE_DIRECTOR","visualSystem":"contributions.VISUAL_DESIGNER","layout":"contributions.LAYOUT_DESIGNER","typography":"contributions.TYPOGRAPHY_DESIGNER","imagePrompt":"imagePrompts.variant_2","headline":"headlines.variant_2","body":"bodies.variant_2","cta":"ctas.variant_2"},"paletteRoleRefs":["primary","secondary","background"]}]}]}
        """;
}
