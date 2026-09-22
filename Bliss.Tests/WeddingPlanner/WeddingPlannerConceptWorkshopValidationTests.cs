using System.Text.Json.Nodes;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerConceptWorkshopValidationTests
{
    private static readonly Guid BrandId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ColorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ResearchId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly IReadOnlySet<string> PaletteRoles =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "primary", "secondary", "accent", "background", "neutral800"
        };

    private static readonly IReadOnlySet<string> SourceIds =
        new HashSet<string>(StringComparer.Ordinal) { "src_1", "src_2" };

    [Fact]
    public void Brief_requires_channel_format_and_deliverable_bounds()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(channelFormat: "NOPE"));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(deliverables: Array.Empty<string>()));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(deliverables: Enumerable.Range(1, 7).Select(i => $"d{i}").ToArray()));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(objective: " "));
    }

    [Fact]
    public void Canvas_is_derived_and_mismatched_client_canvas_fails()
    {
        var brief = CanonicalBrief();
        Assert.Equal(1080, brief.CanvasWidth);
        Assert.Equal(1080, brief.CanvasHeight);
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalBrief(canvasWidth: 100, canvasHeight: 100));
        var ok = CanonicalBrief(canvasWidth: 1080, canvasHeight: 1080);
        Assert.Equal(1080, ok.CanvasWidth);
    }

    [Fact]
    public void Forbidden_brief_fields_fail_closed()
    {
        var node = JsonNode.Parse("""{"objective":"x","campaignId":"c-1"}""")!;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.RejectForbiddenBriefFields(node));
        var nested = JsonNode.Parse("""{"ok":true,"nested":{"html":"<b>"}}""")!;
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.RejectForbiddenBriefFields(nested));
    }

    [Fact]
    public void Strategy_accepts_only_brand_strategist_and_exact_concept_ids()
    {
        var good = StrategyJson();
        var stage = WeddingPlannerConceptWorkshopValidation.CanonicalizeStrategyOutput(good, requireSyntheticMarker: true);
        Assert.Single(stage.Contributions);
        Assert.Equal(3, stage.ConceptIds.Count);

        var extraRole = good.Replace(
            "\"logicalRole\":\"BRAND_STRATEGIST\"",
            "\"logicalRole\":\"ART_DIRECTOR\"",
            StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeStrategyOutput(extraRole, true));

        var missingMarker = good.Replace(
            "\"marker\":\"SYNTHETIC DEVELOPMENT PROTOTYPE\",",
            string.Empty,
            StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeStrategyOutput(missingMarker, true));
    }

    [Fact]
    public void Creative_validates_copy_kind_palette_and_source_ids()
    {
        var strategyIds = WeddingPlannerConceptIds.All;
        var good = CreativeJson();
        var stage = WeddingPlannerConceptWorkshopValidation.CanonicalizeCreativeOutput(
            good, strategyIds, SourceIds, PaletteRoles, ForbiddenProvenance(), true);
        Assert.Equal(2, stage.Contributions.Count);

        var badKind = good.Replace("CREATIVE_NON_FACTUAL", "FACTUAL", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeCreativeOutput(
                badKind, strategyIds, SourceIds, PaletteRoles, ForbiddenProvenance(), true));

        var dangling = good.Replace("\"src_1\"", "\"missing\"", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeCreativeOutput(
                dangling, strategyIds, SourceIds, PaletteRoles, ForbiddenProvenance(), true));

        var provenanceAsSource = good.Replace("\"src_1\"", $"\"{BrandId:D}\"", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeCreativeOutput(
                provenanceAsSource, strategyIds, SourceIds, PaletteRoles, ForbiddenProvenance(), true));

        var unknownPalette = good.Replace("\"primary\"", "\"neonGlow\"", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeCreativeOutput(
                unknownPalette, strategyIds, SourceIds, PaletteRoles, ForbiddenProvenance(), true));
    }

    [Fact]
    public void Prototype_spec_enforces_canvas_bounds_template_textref_and_forbidden_keys()
    {
        var good = ProductionJson();
        var stage = WeddingPlannerConceptWorkshopValidation.CanonicalizeProductionOutput(
            good, WeddingPlannerConceptIds.All, WeddingPlannerChannelFormats.StaticSocialSquare,
            1080, 1080, PaletteRoles, true);
        Assert.Single(stage.Contributions);

        var badCanvas = good.Replace("\"width\": 1080", "\"width\": 999", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeProductionOutput(
                badCanvas, WeddingPlannerConceptIds.All, WeddingPlannerChannelFormats.StaticSocialSquare,
                1080, 1080, PaletteRoles, true));

        var badTextRef = good.Replace("copy.headline", "copy.unknown", StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeProductionOutput(
                badTextRef, WeddingPlannerConceptIds.All, WeddingPlannerChannelFormats.StaticSocialSquare,
                1080, 1080, PaletteRoles, true));

        var withHtml = good.Replace(
            "\"template\": \"LOFI_STACK_V1\"",
            "\"template\": \"LOFI_STACK_V1\", \"html\": \"<div/>\"",
            StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() =>
            WeddingPlannerConceptWorkshopValidation.CanonicalizeProductionOutput(
                withHtml, WeddingPlannerConceptIds.All, WeddingPlannerChannelFormats.StaticSocialSquare,
                1080, 1080, PaletteRoles, true));
    }

    [Fact]
    public void Merge_requires_three_concepts_four_contributions_disclaimer_and_marker()
    {
        var brief = CanonicalBrief();
        var strategy = WeddingPlannerConceptWorkshopValidation.CanonicalizeStrategyOutput(StrategyJson(), true);
        var creative = WeddingPlannerConceptWorkshopValidation.CanonicalizeCreativeOutput(
            CreativeJson(), strategy.ConceptIds, SourceIds, PaletteRoles, ForbiddenProvenance(), true);
        var production = WeddingPlannerConceptWorkshopValidation.CanonicalizeProductionOutput(
            ProductionJson(), strategy.ConceptIds, brief.ChannelFormat, brief.CanvasWidth, brief.CanvasHeight,
            PaletteRoles, true);

        var package = WeddingPlannerConceptWorkshopValidation.MergeAndCanonicalizePackage(
            brief, strategy, creative, production, Guid.NewGuid(), requireSyntheticMarker: true);
        Assert.Contains(WeddingPlannerConceptPackageDisclaimer.Text, package.DocumentJson, StringComparison.Ordinal);
        Assert.Contains(WeddingPlannerConceptWorkshopMarkers.SyntheticDevelopmentPrototype, package.DocumentJson, StringComparison.Ordinal);
        Assert.Equal(3, package.Concepts.Count);
        Assert.Equal(4, package.Contributions.Count);
        Assert.Equal(4, WeddingPlannerConceptWorkshopLogicalRoles.AllInOrder.Count);
        Assert.Equal(3, WeddingPlannerConceptWorkshopWorkerProfiles.All.Count);
        Assert.Equal(107, WeddingPlannerConceptWorkshopIdempotency.MaxJobIdempotencyKeyLength);
    }

    private static CanonicalWorkshopBrief CanonicalBrief(
        string objective = "Plan a calm social concept",
        string channelFormat = WeddingPlannerChannelFormats.StaticSocialSquare,
        IReadOnlyList<string>? deliverables = null,
        int? canvasWidth = null,
        int? canvasHeight = null) =>
        WeddingPlannerConceptWorkshopValidation.CanonicalizeBrief(
            objective,
            "Increase venue inquiries",
            "Engaged couples in Manila",
            channelFormat,
            deliverables ?? ["Static square creative"],
            "Start planning",
            Array.Empty<string>(),
            BrandId,
            1,
            ColorId,
            1,
            ResearchId,
            1,
            canvasWidth,
            canvasHeight,
            WeddingPlannerAiProviderKinds.Local,
            WeddingPlannerWorkers.LocalDeterministicV1);

    private static IReadOnlySet<string> ForbiddenProvenance() =>
        new HashSet<string>(StringComparer.Ordinal)
        {
            BrandId.ToString("D"),
            ColorId.ToString("D"),
            ResearchId.ToString("D")
        };

    private static string StrategyJson() => """
        {"schemaVersion":"concept-strategy-worker-output.v1","workerProfileVersion":"CONCEPT_STRATEGY_V1","marker":"SYNTHETIC DEVELOPMENT PROTOTYPE","contributions":[{"logicalRole":"BRAND_STRATEGIST","summary":"Three directions.","concepts":[{"id":"concept_1","name":"Quiet Confidence","rationale":"r1"},{"id":"concept_2","name":"Warm Invitation","rationale":"r2"},{"id":"concept_3","name":"Clear Next Step","rationale":"r3"}]}]}
        """;

    private static string CreativeJson() => """
        {"schemaVersion":"concept-creative-worker-output.v1","workerProfileVersion":"CONCEPT_CREATIVE_V1","marker":"SYNTHETIC DEVELOPMENT PROTOTYPE","contributions":[{"logicalRole":"ART_DIRECTOR","summary":"Visuals.","concepts":[{"id":"concept_1","visualDirection":"Centered quiet hero.","paletteRoleRefs":["primary","background","accent"]},{"id":"concept_2","visualDirection":"Split frame.","paletteRoleRefs":["primary","secondary","background"]},{"id":"concept_3","visualDirection":"Banner-forward.","paletteRoleRefs":["primary","accent","background"]}]},{"logicalRole":"COPYWRITER","summary":"Copy.","concepts":[{"id":"concept_1","copy":{"kind":"CREATIVE_NON_FACTUAL","headline":"H1","body":"B1","cta":"C1"},"factualClaims":[]},{"id":"concept_2","copy":{"kind":"CREATIVE_NON_FACTUAL","headline":"H2","body":"B2","cta":"C2"},"factualClaims":[{"statement":"Couples research early.","sourceIds":["src_1"]}]},{"id":"concept_3","copy":{"kind":"CREATIVE_NON_FACTUAL","headline":"H3","body":"B3","cta":"C3"}}]}]}
        """;

    private static string ProductionJson() =>
        """
        {
          "schemaVersion": "prototype-production-worker-output.v1",
          "workerProfileVersion": "PROTOTYPE_PRODUCTION_V1",
          "marker": "SYNTHETIC DEVELOPMENT PROTOTYPE",
          "contributions": [
            {
              "logicalRole": "PRODUCTION_ARTIST",
              "summary": "Specs.",
              "prototypes": [
                {
                  "conceptId": "concept_1",
                  "spec": {
                    "schemaVersion": "prototype-spec.v1",
                    "format": "STATIC_SOCIAL_SQUARE",
                    "canvas": { "width": 1080, "height": 1080 },
                    "template": "LOFI_STACK_V1",
                    "regions": [
                      {
                        "id": "r1",
                        "type": "HERO",
                        "bounds": { "x": 0, "y": 0, "w": 1080, "h": 640 },
                        "assetPlaceholder": { "kind": "HERO_IMAGE", "label": "Hero atmosphere placeholder" }
                      },
                      {
                        "id": "r2",
                        "type": "HEADLINE",
                        "bounds": { "x": 64, "y": 680, "w": 952, "h": 96 },
                        "textRef": "copy.headline",
                        "paletteRoleRef": "primary"
                      },
                      {
                        "id": "r3",
                        "type": "BODY",
                        "bounds": { "x": 64, "y": 792, "w": 952, "h": 120 },
                        "textRef": "copy.body",
                        "paletteRoleRef": "neutral800"
                      },
                      {
                        "id": "r4",
                        "type": "CTA",
                        "bounds": { "x": 64, "y": 940, "w": 360, "h": 72 },
                        "textRef": "copy.cta",
                        "paletteRoleRef": "accent"
                      },
                      {
                        "id": "r5",
                        "type": "LOGO_SLOT",
                        "bounds": { "x": 900, "y": 960, "w": 116, "h": 48 },
                        "assetPlaceholder": { "kind": "LOGO", "label": "Logo placeholder" }
                      }
                    ]
                  }
                },
                {
                  "conceptId": "concept_2",
                  "spec": {
                    "schemaVersion": "prototype-spec.v1",
                    "format": "STATIC_SOCIAL_SQUARE",
                    "canvas": { "width": 1080, "height": 1080 },
                    "template": "LOFI_SPLIT_V1",
                    "regions": [
                      {
                        "id": "r1",
                        "type": "HERO",
                        "bounds": { "x": 0, "y": 0, "w": 540, "h": 1080 },
                        "assetPlaceholder": { "kind": "HERO_IMAGE", "label": "Split hero" }
                      },
                      {
                        "id": "r2",
                        "type": "HEADLINE",
                        "bounds": { "x": 560, "y": 80, "w": 460, "h": 96 },
                        "textRef": "copy.headline",
                        "paletteRoleRef": "primary"
                      },
                      {
                        "id": "r3",
                        "type": "CTA",
                        "bounds": { "x": 560, "y": 900, "w": 360, "h": 72 },
                        "textRef": "copy.cta",
                        "paletteRoleRef": "accent"
                      }
                    ]
                  }
                },
                {
                  "conceptId": "concept_3",
                  "spec": {
                    "schemaVersion": "prototype-spec.v1",
                    "format": "STATIC_SOCIAL_SQUARE",
                    "canvas": { "width": 1080, "height": 1080 },
                    "template": "LOFI_BANNER_V1",
                    "regions": [
                      {
                        "id": "r1",
                        "type": "HEADER",
                        "bounds": { "x": 0, "y": 0, "w": 1080, "h": 200 },
                        "textRef": "copy.headline",
                        "paletteRoleRef": "primary"
                      },
                      {
                        "id": "r2",
                        "type": "CTA",
                        "bounds": { "x": 64, "y": 900, "w": 360, "h": 72 },
                        "textRef": "copy.cta",
                        "paletteRoleRef": "accent"
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;
}
