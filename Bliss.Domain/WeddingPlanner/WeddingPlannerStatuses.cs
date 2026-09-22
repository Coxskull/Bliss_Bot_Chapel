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
}

public static class WeddingPlannerSchemaVersions
{
    public const string BrandDnaV1 = "brand-dna.v1";
    public const string ColorProfileV1 = "color-profile.v1";
    public const string ResearchBriefV1 = "research-brief.v1";
    public const string ResearchSourceCatalogV1 = "research-source-catalog.v1";
    public const string CuratorWorkerOutputV1 = "curator-worker-output.v1";
    public const string ResearchReportV1 = "research-report.v1";
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
