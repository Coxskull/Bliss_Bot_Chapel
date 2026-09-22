using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;

namespace Bliss.Tests.WeddingPlanner;

public sealed class WeddingPlannerQaRulesEngineTests
{
    [Fact]
    public void Provenance_chain_blocks_when_pinned_rows_missing_or_cross_tenant()
    {
        var package = MinimalPackage();
        var baseCtx = BaseContext(package, provenancePinsValid: true);
        var pass = WeddingPlannerQaRulesEngine.Evaluate(baseCtx);
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Pass,
            pass.Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.ProvenanceChain).Severity);

        var blocked = WeddingPlannerQaRulesEngine.Evaluate(baseCtx with { ProvenancePinsValid = false });
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Block,
            blocked.Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.ProvenanceChain).Severity);
        Assert.Equal(WeddingPlannerQaFindingSeverities.Block, blocked.OverallSeverity);
    }

    [Fact]
    public void Variant_refs_requires_nonfactual_copy_known_palette_and_asset_meta()
    {
        var package = MinimalPackage();
        var goodVariant = GoodVariant();
        var known = new HashSet<string>(StringComparer.Ordinal) { "primary", "accent" };
        var pass = WeddingPlannerQaRulesEngine.Evaluate(
            BaseContext(package, provenancePinsValid: true, variant: goodVariant, palette: known));
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Pass,
            pass.Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.VariantRefs).Severity);

        var unknownPalette = goodVariant with { PaletteRoleRefs = ["primary", "unknown_role"] };
        var blockedPalette = WeddingPlannerQaRulesEngine.Evaluate(
            BaseContext(package, true, unknownPalette, known));
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Block,
            blockedPalette.Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.VariantRefs).Severity);

        var emptyPalette = goodVariant with { PaletteRoleRefs = Array.Empty<string>() };
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Block,
            WeddingPlannerQaRulesEngine.Evaluate(BaseContext(package, true, emptyPalette, known))
                .Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.VariantRefs).Severity);

        var badKind = goodVariant with { CopyKind = "FACTUAL" };
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Block,
            WeddingPlannerQaRulesEngine.Evaluate(BaseContext(package, true, badKind, known))
                .Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.VariantRefs).Severity);

        var missingAsset = goodVariant with { PackageAssetId = null, PackageAssetSha256 = null };
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Block,
            WeddingPlannerQaRulesEngine.Evaluate(BaseContext(package, true, missingAsset, known))
                .Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.VariantRefs).Severity);
    }

    [Fact]
    public void Claim_preservation_uses_set_equivalence_and_empty_claims_pass()
    {
        Assert.True(WeddingPlannerQaRulesEngine.SourceIdSetsEquivalent(
            ["s1", "s2"],
            ["s2", "s1", "s1"]));
        Assert.False(WeddingPlannerQaRulesEngine.SourceIdSetsEquivalent(
            ["s1", "s2"],
            ["s1"]));

        var package = MinimalPackage();
        var variantEmpty = GoodVariant() with { FactualClaims = Array.Empty<CanonicalCreativeFactualClaim>() };
        var emptyPass = WeddingPlannerQaRulesEngine.Evaluate(
            BaseContext(package, true, variantEmpty, new HashSet<string>(StringComparer.Ordinal) { "primary", "accent" })
                with { SelectedConcept = null });
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Pass,
            emptyPass.Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.ClaimPreservation).Severity);

        var concept = new SelectedConceptSnapshot(
            "concept_1",
            "Name",
            "Rationale",
            "Visual",
            ["primary"],
            new CanonicalCreativeCopy(WeddingPlannerCopyKinds.CreativeNonFactual, "h", "b", "c"),
            [new CanonicalCreativeFactualClaim("Claim A", ["src_b", "src_a"])]);
        var variantReordered = GoodVariant() with
        {
            FactualClaims = [new CanonicalCreativeFactualClaim("Claim A", ["src_a", "src_b", "src_a"])]
        };
        var setPass = WeddingPlannerQaRulesEngine.Evaluate(
            BaseContext(package, true, variantReordered, new HashSet<string>(StringComparer.Ordinal) { "primary", "accent" })
                with { SelectedConcept = concept });
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Pass,
            setPass.Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.ClaimPreservation).Severity);

        var mismatched = GoodVariant() with
        {
            FactualClaims = [new CanonicalCreativeFactualClaim("Claim A", ["src_a", "src_extra"])]
        };
        var blocked = WeddingPlannerQaRulesEngine.Evaluate(
            BaseContext(package, true, mismatched, new HashSet<string>(StringComparer.Ordinal) { "primary", "accent" })
                with { SelectedConcept = concept });
        Assert.Equal(
            WeddingPlannerQaFindingSeverities.Block,
            blocked.Findings.Single(f => f.Code == WeddingPlannerQaRuleCodes.ClaimPreservation).Severity);
    }

    private static WeddingPlannerQaRulesContext BaseContext(
        WeddingPlannerCreativePackageVersion package,
        bool provenancePinsValid,
        SelectedVariantQaSnapshot? variant = null,
        IReadOnlySet<string>? palette = null)
    {
        var selected = variant ?? GoodVariant();
        var assetId = selected.PackageAssetId ?? Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var asset = new WeddingPlannerCreativeAsset
        {
            Id = assetId,
            AdvertiserId = package.AdvertiserId,
            WorkspaceId = package.WorkspaceId,
            CreativePackageVersionId = package.Id,
            CreativeProductionJobId = package.ProducingCreativeProductionJobId,
            VariantId = "variant_1",
            Format = WeddingPlannerChannelFormats.StaticSocialSquare,
            Width = 1080,
            Height = 1080,
            ContentType = WeddingPlannerCreativeAssetContentTypes.ImagePng,
            Bytes = [0x89, 0x50, 0x4E, 0x47],
            ByteSize = selected.PackageAssetByteSize ?? 4,
            Sha256 = selected.PackageAssetSha256 ?? new string('a', 64),
            ProviderKey = "local",
            AdapterVersion = "v1",
            CreatedAt = DateTime.UtcNow
        };

        // Minimal PNG revalidation will BLOCK on tiny bytes — acceptable for unit tests focused on other codes.
        // Provide enough fields so other BLOCK codes that we assert against are independent.
        return new WeddingPlannerQaRulesContext(
            package.WorkspaceId,
            package.AdvertiserId,
            package.Id,
            package.Id,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "variant_1",
            asset.Id,
            asset.Sha256,
            asset.ContentType,
            asset.ByteSize,
            asset.Width,
            asset.Height,
            package.SelectedConceptId,
            package.ApprovedBrandDnaVersionId,
            package.ApprovedBrandDnaVersionNumber,
            package.ApprovedColorProfileVersionId,
            package.ApprovedColorProfileVersionNumber,
            package.ApprovedResearchReportVersionId,
            package.ApprovedResearchReportVersionNumber,
            package,
            new WeddingPlannerCreativePackageDecision
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                AdvertiserId = package.AdvertiserId,
                WorkspaceId = package.WorkspaceId,
                CreativePackageVersionId = package.Id,
                Decision = WeddingPlannerCreativePackageDecisions.Approve,
                SelectedVariantId = "variant_1",
                ActorType = "OPERATOR",
                ActorLabel = "t",
                Rationale = "ok",
                SourceSystem = "TEST",
                IdempotencyKey = "d",
                OccurredAt = DateTime.UtcNow
            },
            asset,
            1,
            13,
            6,
            Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToArray(),
            selected,
            null,
            provenancePinsValid,
            palette ?? new HashSet<string>(StringComparer.Ordinal) { "primary", "accent" });
    }

    private static SelectedVariantQaSnapshot GoodVariant()
    {
        var assetId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        return new SelectedVariantQaSnapshot(
            "variant_1",
            WeddingPlannerChannelFormats.StaticSocialSquare,
            1080,
            1080,
            WeddingPlannerCopyKinds.CreativeNonFactual,
            "Headline",
            "Body copy",
            "CTA",
            Array.Empty<CanonicalCreativeFactualClaim>(),
            ["primary", "accent"],
            assetId,
            WeddingPlannerCreativeAssetContentTypes.ImagePng,
            1234,
            new string('a', 64),
            1080,
            1080);
    }

    private static WeddingPlannerCreativePackageVersion MinimalPackage()
    {
        var ws = Guid.NewGuid();
        var adv = Guid.NewGuid();
        var dna = Guid.NewGuid();
        var color = Guid.NewGuid();
        var research = Guid.NewGuid();
        var concept = Guid.NewGuid();
        var document = $$"""
            {
              "schemaVersion":"creative-package.v1",
              "disclaimer":{{System.Text.Json.JsonSerializer.Serialize(WeddingPlannerCreativePackageDisclaimer.Text)}},
              "provenance":{
                "approvedConceptPackageVersionId":"{{concept:D}}",
                "selectedConceptId":"concept_1",
                "approvedBrandDnaVersionId":"{{dna:D}}",
                "approvedBrandDnaVersionNumber":1,
                "approvedColorProfileVersionId":"{{color:D}}",
                "approvedColorProfileVersionNumber":1,
                "approvedResearchReportVersionId":"{{research:D}}",
                "approvedResearchReportVersionNumber":1,
                "creativeProductionJobId":"{{Guid.NewGuid():D}}",
                "jobKind":"INITIAL",
                "parentCreativePackageVersionId":null
              },
              "variants":[]
            }
            """;
        return new WeddingPlannerCreativePackageVersion
        {
            Id = Guid.NewGuid(),
            AdvertiserId = adv,
            WorkspaceId = ws,
            VersionNumber = 1,
            SchemaVersion = WeddingPlannerSchemaVersions.CreativePackageV1,
            DocumentJson = document,
            Summary = "summary",
            ProducingCreativeProductionJobId = Guid.NewGuid(),
            ProducingAgentRunId = Guid.NewGuid(),
            ApprovedConceptPackageVersionId = concept,
            SelectedConceptId = "concept_1",
            ApprovedBrandDnaVersionId = dna,
            ApprovedBrandDnaVersionNumber = 1,
            ApprovedColorProfileVersionId = color,
            ApprovedColorProfileVersionNumber = 1,
            ApprovedResearchReportVersionId = research,
            ApprovedResearchReportVersionNumber = 1,
            JobKind = WeddingPlannerCreativeProductionJobKinds.Initial,
            Status = WeddingPlannerCreativePackageStatuses.Approved,
            SourceSystem = "TEST",
            IdempotencyKey = "pkg",
            ActorType = "OPERATOR",
            ActorLabel = "t",
            CreatedAt = DateTime.UtcNow
        };
    }
}
