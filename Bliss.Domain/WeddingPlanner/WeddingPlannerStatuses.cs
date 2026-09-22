namespace Bliss.Domain.WeddingPlanner;

public static class WeddingPlannerStatuses
{
    public const string Active = "ACTIVE";
    public const string Open = "OPEN";
}

public static class WeddingPlannerActorTypes
{
    public const string Advertiser = "ADVERTISER";
    public const string Operator = "OPERATOR";
    public const string System = "SYSTEM";
    public const string Planner = "PLANNER";
}

public static class WeddingPlannerAuditActions
{
    public const string WorkspaceOpened = "WORKSPACE_OPENED";
    public const string SessionCreated = "SESSION_CREATED";
    public const string MessageAppended = "MESSAGE_APPENDED";
    public const string AccessDenied = "ACCESS_DENIED";
    public const string AgentRunStarted = "AGENT_RUN_STARTED";
    public const string AgentRunSucceeded = "AGENT_RUN_SUCCEEDED";
    public const string AgentRunFailed = "AGENT_RUN_FAILED";
    public const string AgentRunReplayed = "AGENT_RUN_REPLAYED";
    public const string BrandDnaProposed = "BRAND_DNA_PROPOSED";
    public const string BrandDnaApproved = "BRAND_DNA_APPROVED";
    public const string BrandDnaRejected = "BRAND_DNA_REJECTED";
    public const string BrandDnaReplayed = "BRAND_DNA_REPLAYED";
    public const string ColorProfileProposed = "COLOR_PROFILE_PROPOSED";
    public const string ColorProfileApproved = "COLOR_PROFILE_APPROVED";
    public const string ColorProfileRejected = "COLOR_PROFILE_REJECTED";
    public const string ColorProfileSuperseded = "COLOR_PROFILE_SUPERSEDED";
    public const string ColorProfileReplayed = "COLOR_PROFILE_REPLAYED";
    public const string ResearchJobStarted = "RESEARCH_JOB_STARTED";
    public const string ResearchJobSucceeded = "RESEARCH_JOB_SUCCEEDED";
    public const string ResearchJobFailed = "RESEARCH_JOB_FAILED";
    public const string ResearchJobReplayed = "RESEARCH_JOB_REPLAYED";
    public const string ResearchReportProposed = "RESEARCH_REPORT_PROPOSED";
    public const string ResearchReportApproved = "RESEARCH_REPORT_APPROVED";
    public const string ResearchReportRejected = "RESEARCH_REPORT_REJECTED";
    public const string ResearchReportSuperseded = "RESEARCH_REPORT_SUPERSEDED";
    public const string ResearchReportReplayed = "RESEARCH_REPORT_REPLAYED";
    public const string WorkshopJobStarted = "WORKSHOP_JOB_STARTED";
    public const string WorkshopJobSucceeded = "WORKSHOP_JOB_SUCCEEDED";
    public const string WorkshopJobFailed = "WORKSHOP_JOB_FAILED";
    public const string WorkshopJobReplayed = "WORKSHOP_JOB_REPLAYED";
    public const string ConceptPackageProposed = "CONCEPT_PACKAGE_PROPOSED";
    public const string ConceptPackageApproved = "CONCEPT_PACKAGE_APPROVED";
    public const string ConceptPackageRejected = "CONCEPT_PACKAGE_REJECTED";
    public const string ConceptPackageSuperseded = "CONCEPT_PACKAGE_SUPERSEDED";
    public const string ConceptPackageReplayed = "CONCEPT_PACKAGE_REPLAYED";
    public const string CreativeProductionJobStarted = "CREATIVE_PRODUCTION_JOB_STARTED";
    public const string CreativeProductionJobSucceeded = "CREATIVE_PRODUCTION_JOB_SUCCEEDED";
    public const string CreativeProductionJobFailed = "CREATIVE_PRODUCTION_JOB_FAILED";
    public const string CreativeProductionJobReplayed = "CREATIVE_PRODUCTION_JOB_REPLAYED";
    public const string CreativeAssetProviderAttempted = "CREATIVE_ASSET_PROVIDER_ATTEMPTED";
    public const string CreativeAssetProviderSucceeded = "CREATIVE_ASSET_PROVIDER_SUCCEEDED";
    public const string CreativeAssetProviderFailed = "CREATIVE_ASSET_PROVIDER_FAILED";
    public const string CreativePackageProposed = "CREATIVE_PACKAGE_PROPOSED";
    public const string CreativePackageApproved = "CREATIVE_PACKAGE_APPROVED";
    public const string CreativePackageRejected = "CREATIVE_PACKAGE_REJECTED";
    public const string CreativePackageSuperseded = "CREATIVE_PACKAGE_SUPERSEDED";
    public const string CreativePackageReplayed = "CREATIVE_PACKAGE_REPLAYED";
    public const string CreativeAssetContentRead = "CREATIVE_ASSET_CONTENT_READ";
}

public static class WeddingPlannerOutcomes
{
    public const string Created = "CREATED";
    public const string Replayed = "REPLAYED";
    public const string Appended = "APPENDED";
    public const string Denied = "DENIED";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerAgentRoles
{
    public const string Concierge = "CONCIERGE";
    public const string BrandDnaInterpreter = "BRAND_DNA_INTERPRETER";

    /// <summary>Phase 4 Curator stage LogicalRole values (exactly 3 executable stages).</summary>
    public const string CuratorResearch = "CURATOR_RESEARCH";
    public const string CuratorEvidence = "CURATOR_EVIDENCE";
    public const string CuratorSynthesisRisk = "CURATOR_SYNTHESIS_RISK";

    /// <summary>Phase 5 Concept Workshop stage LogicalRole values (exactly 3 executable stages).</summary>
    public const string ConceptStrategy = "CONCEPT_STRATEGY";
    public const string ConceptCreative = "CONCEPT_CREATIVE";
    public const string PrototypeProduction = "PROTOTYPE_PRODUCTION";

    /// <summary>Phase 6 Creative Production stage LogicalRole values (exactly 6 executable stages).</summary>
    public const string CreativeDirection = "CREATIVE_DIRECTION";
    public const string StrategyAdaptation = "STRATEGY_ADAPTATION";
    public const string VisualSystem = "VISUAL_SYSTEM";
    public const string ImageDirection = "IMAGE_DIRECTION";
    public const string CopySystem = "COPY_SYSTEM";
    public const string VariantProduction = "VARIANT_PRODUCTION";
}

/// <summary>Exactly four durable Concept Workshop logical roles (not agent-run identities).</summary>
public static class WeddingPlannerConceptWorkshopLogicalRoles
{
    public const string BrandStrategist = "BRAND_STRATEGIST";
    public const string ArtDirector = "ART_DIRECTOR";
    public const string Copywriter = "COPYWRITER";
    public const string ProductionArtist = "PRODUCTION_ARTIST";

    public static readonly IReadOnlyList<string> AllInOrder =
    [
        BrandStrategist,
        ArtDirector,
        Copywriter,
        ProductionArtist
    ];
}

/// <summary>Exactly three executable Concept Workshop worker profiles.</summary>
public static class WeddingPlannerConceptWorkshopWorkerProfiles
{
    public const string ConceptStrategyV1 = "CONCEPT_STRATEGY_V1";
    public const string ConceptCreativeV1 = "CONCEPT_CREATIVE_V1";
    public const string PrototypeProductionV1 = "PROTOTYPE_PRODUCTION_V1";

    public static readonly IReadOnlyList<string> All =
    [
        ConceptStrategyV1,
        ConceptCreativeV1,
        PrototypeProductionV1
    ];

    public static IReadOnlyList<string> AssignedRoles(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            ConceptStrategyV1 => [WeddingPlannerConceptWorkshopLogicalRoles.BrandStrategist],
            ConceptCreativeV1 =>
            [
                WeddingPlannerConceptWorkshopLogicalRoles.ArtDirector,
                WeddingPlannerConceptWorkshopLogicalRoles.Copywriter
            ],
            PrototypeProductionV1 => [WeddingPlannerConceptWorkshopLogicalRoles.ProductionArtist],
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Concept Workshop worker profile.")
        };

    public static string StageLogicalRole(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            ConceptStrategyV1 => WeddingPlannerAgentRoles.ConceptStrategy,
            ConceptCreativeV1 => WeddingPlannerAgentRoles.ConceptCreative,
            PrototypeProductionV1 => WeddingPlannerAgentRoles.PrototypeProduction,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Concept Workshop worker profile.")
        };

    public static string PromptPack(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            ConceptStrategyV1 => WeddingPlannerPromptPacks.ConceptStrategyV1,
            ConceptCreativeV1 => WeddingPlannerPromptPacks.ConceptCreativeV1,
            PrototypeProductionV1 => WeddingPlannerPromptPacks.PrototypeProductionV1,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Concept Workshop worker profile.")
        };
}

/// <summary>Exactly thirteen NEW durable Creative Production logical roles (not agent-run identities).</summary>
public static class WeddingPlannerCreativeDepartmentLogicalRoles
{
    public const string CreativeDirector = "CREATIVE_DIRECTOR";
    public const string CampaignStrategist = "CAMPAIGN_STRATEGIST";
    public const string AudienceStrategist = "AUDIENCE_STRATEGIST";
    public const string OfferStrategist = "OFFER_STRATEGIST";
    public const string ChannelStrategist = "CHANNEL_STRATEGIST";
    public const string VisualDesigner = "VISUAL_DESIGNER";
    public const string LayoutDesigner = "LAYOUT_DESIGNER";
    public const string TypographyDesigner = "TYPOGRAPHY_DESIGNER";
    public const string ImagePromptDesigner = "IMAGE_PROMPT_DESIGNER";
    public const string HeadlineSpecialist = "HEADLINE_SPECIALIST";
    public const string BodyCopySpecialist = "BODY_COPY_SPECIALIST";
    public const string CtaSpecialist = "CTA_SPECIALIST";
    public const string VariantProducer = "VARIANT_PRODUCER";

    public static readonly IReadOnlyList<string> AllInOrder =
    [
        CreativeDirector,
        CampaignStrategist,
        AudienceStrategist,
        OfferStrategist,
        ChannelStrategist,
        VisualDesigner,
        LayoutDesigner,
        TypographyDesigner,
        ImagePromptDesigner,
        HeadlineSpecialist,
        BodyCopySpecialist,
        CtaSpecialist,
        VariantProducer
    ];
}

/// <summary>Exactly six executable Creative Production worker profiles.</summary>
public static class WeddingPlannerCreativeDepartmentWorkerProfiles
{
    public const string CreativeDirectionV1 = "CREATIVE_DIRECTION_V1";
    public const string StrategyAdaptationV1 = "STRATEGY_ADAPTATION_V1";
    public const string VisualSystemV1 = "VISUAL_SYSTEM_V1";
    public const string ImageDirectionV1 = "IMAGE_DIRECTION_V1";
    public const string CopySystemV1 = "COPY_SYSTEM_V1";
    public const string VariantProductionV1 = "VARIANT_PRODUCTION_V1";

    public static readonly IReadOnlyList<string> All =
    [
        CreativeDirectionV1,
        StrategyAdaptationV1,
        VisualSystemV1,
        ImageDirectionV1,
        CopySystemV1,
        VariantProductionV1
    ];

    public static IReadOnlyList<string> AssignedRoles(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            CreativeDirectionV1 =>
            [
                WeddingPlannerCreativeDepartmentLogicalRoles.CreativeDirector,
                WeddingPlannerCreativeDepartmentLogicalRoles.CampaignStrategist
            ],
            StrategyAdaptationV1 =>
            [
                WeddingPlannerCreativeDepartmentLogicalRoles.AudienceStrategist,
                WeddingPlannerCreativeDepartmentLogicalRoles.OfferStrategist,
                WeddingPlannerCreativeDepartmentLogicalRoles.ChannelStrategist
            ],
            VisualSystemV1 =>
            [
                WeddingPlannerCreativeDepartmentLogicalRoles.VisualDesigner,
                WeddingPlannerCreativeDepartmentLogicalRoles.LayoutDesigner,
                WeddingPlannerCreativeDepartmentLogicalRoles.TypographyDesigner
            ],
            ImageDirectionV1 => [WeddingPlannerCreativeDepartmentLogicalRoles.ImagePromptDesigner],
            CopySystemV1 =>
            [
                WeddingPlannerCreativeDepartmentLogicalRoles.HeadlineSpecialist,
                WeddingPlannerCreativeDepartmentLogicalRoles.BodyCopySpecialist,
                WeddingPlannerCreativeDepartmentLogicalRoles.CtaSpecialist
            ],
            VariantProductionV1 => [WeddingPlannerCreativeDepartmentLogicalRoles.VariantProducer],
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Creative Department worker profile.")
        };

    public static string StageLogicalRole(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            CreativeDirectionV1 => WeddingPlannerAgentRoles.CreativeDirection,
            StrategyAdaptationV1 => WeddingPlannerAgentRoles.StrategyAdaptation,
            VisualSystemV1 => WeddingPlannerAgentRoles.VisualSystem,
            ImageDirectionV1 => WeddingPlannerAgentRoles.ImageDirection,
            CopySystemV1 => WeddingPlannerAgentRoles.CopySystem,
            VariantProductionV1 => WeddingPlannerAgentRoles.VariantProduction,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Creative Department worker profile.")
        };

    public static string PromptPack(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            CreativeDirectionV1 => WeddingPlannerPromptPacks.CreativeDirectionV1,
            StrategyAdaptationV1 => WeddingPlannerPromptPacks.StrategyAdaptationV1,
            VisualSystemV1 => WeddingPlannerPromptPacks.VisualSystemV1,
            ImageDirectionV1 => WeddingPlannerPromptPacks.ImageDirectionV1,
            CopySystemV1 => WeddingPlannerPromptPacks.CopySystemV1,
            VariantProductionV1 => WeddingPlannerPromptPacks.VariantProductionV1,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Creative Department worker profile.")
        };
}

/// <summary>
/// Internal run/package idempotency suffixes. Public job keys must leave room for the longest suffix
/// within the 128-character IdempotencyKey column.
/// </summary>
public static class WeddingPlannerCreativeDepartmentIdempotency
{
    public const string CreativeDirectionStageSuffix = ":CREATIVE_DIRECTION";
    public const string StrategyAdaptationStageSuffix = ":STRATEGY_ADAPTATION";
    public const string VisualSystemStageSuffix = ":VISUAL_SYSTEM";
    public const string ImageDirectionStageSuffix = ":IMAGE_DIRECTION";
    public const string CopySystemStageSuffix = ":COPY_SYSTEM";
    public const string VariantProductionStageSuffix = ":VARIANT_PRODUCTION";
    public const string PackageSuffix = ":PACKAGE";

    /// <summary>Longest stage suffix length (<see cref="StrategyAdaptationStageSuffix"/>).</summary>
    public const int LongestSuffixLength = 21;

    /// <summary>Max public creative-production-job IdempotencyKey length (128 − longest suffix).</summary>
    public const int MaxJobIdempotencyKeyLength = 128 - LongestSuffixLength;

    public static string StageKey(string jobKey, string workerProfileVersion) =>
        workerProfileVersion switch
        {
            WeddingPlannerCreativeDepartmentWorkerProfiles.CreativeDirectionV1 => jobKey + CreativeDirectionStageSuffix,
            WeddingPlannerCreativeDepartmentWorkerProfiles.StrategyAdaptationV1 => jobKey + StrategyAdaptationStageSuffix,
            WeddingPlannerCreativeDepartmentWorkerProfiles.VisualSystemV1 => jobKey + VisualSystemStageSuffix,
            WeddingPlannerCreativeDepartmentWorkerProfiles.ImageDirectionV1 => jobKey + ImageDirectionStageSuffix,
            WeddingPlannerCreativeDepartmentWorkerProfiles.CopySystemV1 => jobKey + CopySystemStageSuffix,
            WeddingPlannerCreativeDepartmentWorkerProfiles.VariantProductionV1 => jobKey + VariantProductionStageSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion))
        };

    public static string PackageKey(string jobKey) => jobKey + PackageSuffix;
}

/// <summary>
/// Internal run/package idempotency suffixes. Public job keys must leave room for the longest suffix
/// within the 128-character IdempotencyKey column.
/// </summary>
public static class WeddingPlannerConceptWorkshopIdempotency
{
    public const string ConceptStrategyStageSuffix = ":CONCEPT_STRATEGY";
    public const string ConceptCreativeStageSuffix = ":CONCEPT_CREATIVE";
    public const string PrototypeProductionStageSuffix = ":PROTOTYPE_PRODUCTION";
    public const string PackageSuffix = ":PACKAGE";

    /// <summary>Longest stage suffix length (<see cref="PrototypeProductionStageSuffix"/>).</summary>
    public const int LongestSuffixLength = 21;

    /// <summary>Max public workshop-job IdempotencyKey length (128 − longest suffix).</summary>
    public const int MaxJobIdempotencyKeyLength = 128 - LongestSuffixLength;

    public static string StageKey(string jobKey, string workerProfileVersion) =>
        workerProfileVersion switch
        {
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptStrategyV1 => jobKey + ConceptStrategyStageSuffix,
            WeddingPlannerConceptWorkshopWorkerProfiles.ConceptCreativeV1 => jobKey + ConceptCreativeStageSuffix,
            WeddingPlannerConceptWorkshopWorkerProfiles.PrototypeProductionV1 => jobKey + PrototypeProductionStageSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion))
        };

    public static string PackageKey(string jobKey) => jobKey + PackageSuffix;
}

/// <summary>Exactly eight durable Curator logical roles (not agent-run identities).</summary>
public static class WeddingPlannerCuratorLogicalRoles
{
    public const string MarketLandscapeResearcher = "MARKET_LANDSCAPE_RESEARCHER";
    public const string AudienceContextResearcher = "AUDIENCE_CONTEXT_RESEARCHER";
    public const string CompetitorSignalsResearcher = "COMPETITOR_SIGNALS_RESEARCHER";
    public const string ChannelFormatResearcher = "CHANNEL_FORMAT_RESEARCHER";
    public const string EvidenceAnalyst = "EVIDENCE_ANALYST";
    public const string SourceVerifier = "SOURCE_VERIFIER";
    public const string ClaimsRiskReviewer = "CLAIMS_RISK_REVIEWER";
    public const string ResearchSynthesizer = "RESEARCH_SYNTHESIZER";

    public static readonly IReadOnlyList<string> AllInOrder =
    [
        MarketLandscapeResearcher,
        AudienceContextResearcher,
        CompetitorSignalsResearcher,
        ChannelFormatResearcher,
        EvidenceAnalyst,
        SourceVerifier,
        ClaimsRiskReviewer,
        ResearchSynthesizer
    ];
}

/// <summary>Exactly three executable Curator worker profiles.</summary>
public static class WeddingPlannerCuratorWorkerProfiles
{
    public const string ResearchV1 = "CURATOR_RESEARCH_V1";
    public const string EvidenceV1 = "CURATOR_EVIDENCE_V1";
    public const string SynthesisRiskV1 = "CURATOR_SYNTHESIS_RISK_V1";

    public static readonly IReadOnlyList<string> All =
    [
        ResearchV1,
        EvidenceV1,
        SynthesisRiskV1
    ];

    public static IReadOnlyList<string> AssignedRoles(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            ResearchV1 =>
            [
                WeddingPlannerCuratorLogicalRoles.MarketLandscapeResearcher,
                WeddingPlannerCuratorLogicalRoles.AudienceContextResearcher,
                WeddingPlannerCuratorLogicalRoles.CompetitorSignalsResearcher,
                WeddingPlannerCuratorLogicalRoles.ChannelFormatResearcher
            ],
            EvidenceV1 =>
            [
                WeddingPlannerCuratorLogicalRoles.EvidenceAnalyst,
                WeddingPlannerCuratorLogicalRoles.SourceVerifier
            ],
            SynthesisRiskV1 =>
            [
                WeddingPlannerCuratorLogicalRoles.ClaimsRiskReviewer,
                WeddingPlannerCuratorLogicalRoles.ResearchSynthesizer
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Curator worker profile.")
        };

    public static string StageLogicalRole(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            ResearchV1 => WeddingPlannerAgentRoles.CuratorResearch,
            EvidenceV1 => WeddingPlannerAgentRoles.CuratorEvidence,
            SynthesisRiskV1 => WeddingPlannerAgentRoles.CuratorSynthesisRisk,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Curator worker profile.")
        };

    public static string PromptPack(string workerProfileVersion) =>
        workerProfileVersion switch
        {
            ResearchV1 => WeddingPlannerPromptPacks.CuratorResearchV1,
            EvidenceV1 => WeddingPlannerPromptPacks.CuratorEvidenceV1,
            SynthesisRiskV1 => WeddingPlannerPromptPacks.CuratorSynthesisRiskV1,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion), workerProfileVersion, "Unknown Curator worker profile.")
        };
}

/// <summary>
/// Internal run/report idempotency suffixes. Public job keys must leave room for the longest suffix
/// within the 128-character IdempotencyKey column.
/// </summary>
public static class WeddingPlannerCuratorIdempotency
{
    public const string ResearchStageSuffix = ":CURATOR_RESEARCH";
    public const string EvidenceStageSuffix = ":CURATOR_EVIDENCE";
    public const string SynthesisRiskStageSuffix = ":CURATOR_SYNTHESIS_RISK";
    public const string ReportSuffix = ":REPORT";

    /// <summary>Longest stage suffix length (<see cref="SynthesisRiskStageSuffix"/>).</summary>
    public const int LongestSuffixLength = 23;

    /// <summary>Max public research-job IdempotencyKey length (128 − longest suffix).</summary>
    public const int MaxJobIdempotencyKeyLength = 128 - LongestSuffixLength;

    public static string StageKey(string jobKey, string workerProfileVersion) =>
        workerProfileVersion switch
        {
            WeddingPlannerCuratorWorkerProfiles.ResearchV1 => jobKey + ResearchStageSuffix,
            WeddingPlannerCuratorWorkerProfiles.EvidenceV1 => jobKey + EvidenceStageSuffix,
            WeddingPlannerCuratorWorkerProfiles.SynthesisRiskV1 => jobKey + SynthesisRiskStageSuffix,
            _ => throw new ArgumentOutOfRangeException(nameof(workerProfileVersion))
        };

    public static string ReportKey(string jobKey) => jobKey + ReportSuffix;
}

public static class WeddingPlannerAgentRunStatuses
{
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

public static class WeddingPlannerBrandDnaStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerBrandDnaDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class WeddingPlannerPromptPacks
{
    public const string ConciergeV1 = "wp-phase2.concierge.v1";
    public const string BrandDnaV1 = "wp-phase2.brand-dna.v1";
    public const string CuratorResearchV1 = "wp-phase4.curator-research.v1";
    public const string CuratorEvidenceV1 = "wp-phase4.curator-evidence.v1";
    public const string CuratorSynthesisRiskV1 = "wp-phase4.curator-synthesis-risk.v1";
    public const string ConceptStrategyV1 = "wp-phase5.concept-strategy.v1";
    public const string ConceptCreativeV1 = "wp-phase5.concept-creative.v1";
    public const string PrototypeProductionV1 = "wp-phase5.prototype-production.v1";
    public const string CreativeDirectionV1 = "wp-phase6.creative-direction.v1";
    public const string StrategyAdaptationV1 = "wp-phase6.strategy-adaptation.v1";
    public const string VisualSystemV1 = "wp-phase6.visual-system.v1";
    public const string ImageDirectionV1 = "wp-phase6.image-direction.v1";
    public const string CopySystemV1 = "wp-phase6.copy-system.v1";
    public const string VariantProductionV1 = "wp-phase6.variant-production.v1";
}

public static class WeddingPlannerSchemaVersions
{
    public const string BrandDnaV1 = "brand-dna.v1";
    public const string ColorProfileV1 = "color-profile.v1";
    public const string ResearchBriefV1 = "research-brief.v1";
    public const string ResearchSourceCatalogV1 = "research-source-catalog.v1";
    public const string CuratorWorkerOutputV1 = "curator-worker-output.v1";
    public const string ResearchReportV1 = "research-report.v1";
    public const string WorkshopBriefV1 = "workshop-brief.v1";
    public const string ConceptStrategyWorkerOutputV1 = "concept-strategy-worker-output.v1";
    public const string ConceptCreativeWorkerOutputV1 = "concept-creative-worker-output.v1";
    public const string PrototypeProductionWorkerOutputV1 = "prototype-production-worker-output.v1";
    public const string PrototypeSpecV1 = "prototype-spec.v1";
    public const string ConceptPackageV1 = "concept-package.v1";
    public const string CreativeProductionBriefV1 = "creative-production-brief.v1";
    public const string CreativeDirectionWorkerOutputV1 = "creative-direction-worker-output.v1";
    public const string StrategyAdaptationWorkerOutputV1 = "strategy-adaptation-worker-output.v1";
    public const string VisualSystemWorkerOutputV1 = "visual-system-worker-output.v1";
    public const string ImageDirectionWorkerOutputV1 = "image-direction-worker-output.v1";
    public const string CopySystemWorkerOutputV1 = "copy-system-worker-output.v1";
    public const string VariantProductionWorkerOutputV1 = "variant-production-worker-output.v1";
    public const string CreativePackageV1 = "creative-package.v1";
}

public static class WeddingPlannerCreativeDepartmentContractVersions
{
    public const string CreativeDepartmentOrchestrationV1 = "wp-creative-department-orchestration.v1";
    public const string CreativeAssetProviderV1 = "wp-creative-asset-provider.v1";
}

public static class WeddingPlannerConceptWorkshopContractVersions
{
    public const string ConceptWorkshopOrchestrationV1 = "wp-concept-workshop-orchestration.v1";
}

public static class WeddingPlannerResearchContractVersions
{
    public const string ResearchProviderV1 = "wp-research-provider.v1";
    public const string CuratorOrchestrationV1 = "wp-curator-orchestration.v1";
}

public static class WeddingPlannerResearchJobStatuses
{
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

public static class WeddingPlannerResearchReportStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerResearchReportDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class WeddingPlannerFindingTypes
{
    public const string Fact = "FACT";
    public const string Inference = "INFERENCE";
    public const string Gap = "GAP";
    public const string Risk = "RISK";
}

public static class WeddingPlannerResearchDisclaimer
{
    public const string Text =
        "Approval of this report is research approval only. It is not creative, campaign, claim, legal, matching, accessibility, or compliance approval. Source verification checks metadata and internal consistency only; live URL content is not fetched or certified.";
}

public static class WeddingPlannerWorkshopJobStatuses
{
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

public static class WeddingPlannerConceptPackageStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerConceptPackageDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class WeddingPlannerChannelFormats
{
    public const string StaticSocialSquare = "STATIC_SOCIAL_SQUARE";
    public const string StaticSocialStory = "STATIC_SOCIAL_STORY";
    public const string StaticDisplayBanner = "STATIC_DISPLAY_BANNER";
    public const string EmailHero = "EMAIL_HERO";

    public static readonly IReadOnlyList<string> All =
    [
        StaticSocialSquare,
        StaticSocialStory,
        StaticDisplayBanner,
        EmailHero
    ];
}

public static class WeddingPlannerConceptIds
{
    public const string Concept1 = "concept_1";
    public const string Concept2 = "concept_2";
    public const string Concept3 = "concept_3";

    public static readonly IReadOnlyList<string> All =
    [
        Concept1,
        Concept2,
        Concept3
    ];
}

public static class WeddingPlannerCopyKinds
{
    public const string CreativeNonFactual = "CREATIVE_NON_FACTUAL";
}

public static class WeddingPlannerPrototypeTemplates
{
    public const string LofiStackV1 = "LOFI_STACK_V1";
    public const string LofiSplitV1 = "LOFI_SPLIT_V1";
    public const string LofiBannerV1 = "LOFI_BANNER_V1";

    public static readonly IReadOnlyList<string> All =
    [
        LofiStackV1,
        LofiSplitV1,
        LofiBannerV1
    ];
}

public static class WeddingPlannerPrototypeRegionTypes
{
    public const string Hero = "HERO";
    public const string Header = "HEADER";
    public const string Body = "BODY";
    public const string Headline = "HEADLINE";
    public const string Subhead = "SUBHEAD";
    public const string Cta = "CTA";
    public const string Footer = "FOOTER";
    public const string LogoSlot = "LOGO_SLOT";

    public static readonly IReadOnlyList<string> All =
    [
        Hero,
        Header,
        Body,
        Headline,
        Subhead,
        Cta,
        Footer,
        LogoSlot
    ];
}

public static class WeddingPlannerPrototypeTextRefs
{
    public const string CopyHeadline = "copy.headline";
    public const string CopyBody = "copy.body";
    public const string CopyCta = "copy.cta";

    public static readonly IReadOnlyList<string> All =
    [
        CopyHeadline,
        CopyBody,
        CopyCta
    ];
}

public static class WeddingPlannerAssetPlaceholderKinds
{
    public const string HeroImage = "HERO_IMAGE";
    public const string Logo = "LOGO";
    public const string Product = "PRODUCT";
    public const string Decorative = "DECORATIVE";

    public static readonly IReadOnlyList<string> All =
    [
        HeroImage,
        Logo,
        Product,
        Decorative
    ];
}

public static class WeddingPlannerConceptWorkshopMarkers
{
    public const string SyntheticDevelopmentPrototype = "SYNTHETIC DEVELOPMENT PROTOTYPE";
}

public static class WeddingPlannerCreativeDepartmentMarkers
{
    public const string SyntheticDevelopmentCreativePackage = "SYNTHETIC DEVELOPMENT CREATIVE PACKAGE";
}

public static class WeddingPlannerConceptPackageDisclaimer
{
    public const string Text =
        "Approval of this package is concept-direction approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, asset, QA, or production-artwork approval. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim cites source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Prototypes are structured low-fi specs only; no images are generated.";
}

public static class WeddingPlannerCreativePackageDisclaimer
{
    public const string Text =
        "Approval of this package is draft creative approval only. It is not research, claim, legal, matching, accessibility, compliance, campaign-ready, QA, or final production-artwork approval. It does not authorize Bliss matching or placement. Marketing copy is CREATIVE_NON_FACTUAL unless a factual claim exactly preserves a cited claim from the pinned selected concept using source IDs from the pinned approved research report. Brand DNA and Color Profile are creative constraints, not factual evidence. Draft PNG assets are provider-generated renditions for review only; Wedding Planner orchestrates providers and is not itself an image generator. Phase 5 concept contributions remain pinned provenance and are not re-approved here.";
}

public static class WeddingPlannerCreativeProductionJobKinds
{
    public const string Initial = "INITIAL";
    public const string Revision = "REVISION";

    public static readonly IReadOnlyList<string> All = [Initial, Revision];
}

public static class WeddingPlannerCreativeProductionJobStatuses
{
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}

public static class WeddingPlannerCreativePackageStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerCreativePackageDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class WeddingPlannerVariantIds
{
    public static IReadOnlyList<string> ForCount(int count)
    {
        if (count is < 1 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Variant count must be 1–4.");
        }

        return Enumerable.Range(1, count).Select(i => $"variant_{i}").ToArray();
    }
}

public static class WeddingPlannerCreativeAssetContentTypes
{
    public const string ImagePng = "image/png";
}

public static class WeddingPlannerCreativeAssetProviderKinds
{
    public const string Local = "Local";
    public const string RemoteHttp = "RemoteHttp";
}

public static class WeddingPlannerCreativeAssetWorkers
{
    public const string LocalDeterministicV1 = "wp-creative-asset-local-deterministic.v1";
    public const string RemoteHttpV1 = "wp-creative-asset-remote-http.v1";
}

public static class WeddingPlannerResearchProviderKinds
{
    public const string Local = "Local";
    public const string RemoteHttp = "RemoteHttp";
}

public static class WeddingPlannerResearchWorkers
{
    public const string LocalDeterministicV1 = "wp-research-local-deterministic.v1";
    public const string RemoteHttpV1 = "wp-research-remote-http.v1";
}

public static class WeddingPlannerAlgorithmVersions
{
    public const string AciHslV1 = "aci.hsl.v1";
}

public static class WeddingPlannerColorProfileStatuses
{
    public const string Proposed = "PROPOSED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Superseded = "SUPERSEDED";
}

public static class WeddingPlannerColorProfileDecisions
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class WeddingPlannerResponseFormats
{
    public const string Text = "text";
    public const string Json = "json";
}

public static class WeddingPlannerWorkers
{
    public const string LocalDeterministicV1 = "wp-local-deterministic.v1";
    public const string OpenAiCompatibleV1 = "wp-openai-compatible.v1";
}

public static class WeddingPlannerAiProviderKinds
{
    public const string Local = "Local";
    public const string OpenAiCompatible = "OpenAiCompatible";
}
