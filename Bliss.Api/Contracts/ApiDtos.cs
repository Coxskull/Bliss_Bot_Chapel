namespace Bliss.Api.Contracts;

public sealed record CreatorListDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? PrimaryLanguage,
    int? AudienceSize,
    decimal? FemalePercentage,
    decimal? MalePercentage);

public sealed record CreatorDetailDto(
    Guid Id,
    string Name,
    string? CountryCode,
    string? PrimaryLanguage,
    int? AudienceSize,
    decimal? FemalePercentage,
    decimal? MalePercentage,
    string? PrimaryAgeRange,
    string? PrimaryGeography,
    string? EngagementLevel,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CreatorPlatformDto> Platforms,
    IReadOnlyList<ContentItemDetailDto> ContentItems,
    IReadOnlyList<BlissMatchSummaryDto> BlissMatches);

public sealed record CreatorPlatformDto(
    Guid Id,
    string Platform,
    string? ExternalProfileId,
    string? ProfileUrl,
    int? Followers,
    DateTime? LastCollectedAt);

public sealed record ContentItemListDto(
    Guid Id,
    Guid CreatorId,
    string ContentType,
    string Title,
    string? ExternalContentId,
    string? Url);

public sealed record ContentItemDetailDto(
    Guid Id,
    Guid CreatorId,
    string ContentType,
    string Title,
    string? ExternalContentId,
    string? Url,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    IReadOnlyList<AdInventorySlotDto> AdInventorySlots);

public sealed record AdInventorySlotDto(
    Guid Id,
    string SlotType,
    int? StartSecond,
    int? DurationSeconds,
    bool IsAvailable);

public sealed record AdvertiserListDto(
    Guid Id,
    string Name,
    string? Website,
    string? CountryCode);

public sealed record AdvertiserDetailDto(
    Guid Id,
    string Name,
    string? Website,
    string? CountryCode,
    string? Description,
    DateTime CreatedAt,
    IReadOnlyList<AdvertiserProgramListDto> Programs);

public sealed record AdvertiserProgramListDto(
    Guid Id,
    Guid AdvertiserId,
    string Name,
    string? ExternalProgramId,
    string Status);

public sealed record AdvertiserOpportunityListDto(
    Guid Id,
    Guid AdvertiserProgramId,
    string Name,
    string? ProductName,
    string? Category,
    string Status);

public sealed record BlissMatchSummaryDto(
    Guid Id,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid RuleVersionId,
    string Status,
    decimal? OverallScore,
    decimal? ConfidenceScore,
    DateTime CreatedAt);

public sealed record BlissMatchDetailDto(
    Guid Id,
    string Status,
    decimal? OverallScore,
    decimal? ConfidenceScore,
    DateTime CreatedAt,
    CreatorListDto Creator,
    AdvertiserOpportunityDetailDto AdvertiserOpportunity,
    RuleVersionDto RuleVersion,
    IReadOnlyList<MatchScoreComponentDto> ScoreComponents,
    IReadOnlyList<EligibilityCheckDto> EligibilityChecks);

public sealed record AdvertiserOpportunityDetailDto(
    Guid Id,
    string Name,
    string? ProductName,
    string? Category,
    string? Description,
    string Status,
    AdvertiserProgramDetailDto AdvertiserProgram);

public sealed record AdvertiserProgramDetailDto(
    Guid Id,
    string Name,
    string Status,
    AdvertiserListDto Advertiser);

public sealed record RuleVersionDto(
    Guid Id,
    string Version,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string? DocumentJson = null);

public sealed record MatchScoreComponentDto(
    Guid Id,
    string ComponentName,
    decimal? Score,
    decimal? Weight,
    string? Explanation);

public sealed record EligibilityCheckDto(
    Guid Id,
    string CheckType,
    string Result,
    string? ReasonCode,
    string? Explanation);

public sealed record MatchEvaluationRunSummaryDto(
    Guid Id,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid RuleVersionId,
    string AlgorithmVersion,
    string Status,
    string? MatchStatus,
    decimal? OverallScore,
    decimal? ConfidenceScore,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record MatchEvaluationRunDetailDto(
    Guid Id,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid RuleVersionId,
    string AlgorithmVersion,
    string Status,
    string? MatchStatus,
    decimal? OverallScore,
    decimal? ConfidenceScore,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? InputSnapshot,
    string? OutputSnapshot);

public sealed record CreatorIngestionRequest(
    string SourceSystem,
    string IdempotencyKey,
    string Platform,
    string ExternalProfileId,
    string CreatorName,
    string? ProfileUrl,
    string? CountryCode,
    string? PrimaryLanguage,
    int? AudienceSize,
    int? Followers,
    decimal? FemalePercentage,
    decimal? MalePercentage,
    string? PrimaryAgeRange,
    string? PrimaryGeography,
    string? EngagementLevel,
    string? SourceUrl,
    string? ConfidenceLevel,
    DateTime? CollectedAt);

public sealed record CreatorIngestionResultDto(
    Guid RunId,
    Guid CreatorId,
    Guid CreatorPlatformId,
    string IdentityKey,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string Outcome,
    DateTime CompletedAt,
    bool IsReplay);

public sealed record CreatorIngestionRunSummaryDto(
    Guid Id,
    Guid CreatorId,
    Guid CreatorPlatformId,
    string SourceSystem,
    string IdempotencyKey,
    string IdentityKey,
    string Status,
    string Outcome,
    DateTime CompletedAt);

public sealed record CreatorIngestionRunDetailDto(
    Guid Id,
    Guid CreatorId,
    Guid CreatorPlatformId,
    string SourceSystem,
    string IdempotencyKey,
    string IdentityKey,
    string Status,
    string Outcome,
    DateTime StartedAt,
    DateTime CompletedAt,
    string InputSnapshot);

public sealed record MatchFormationRequest(
    string SourceSystem,
    string IdempotencyKey,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid RuleVersionId,
    bool EvaluateOnCreate);

public sealed record MatchFormationResultDto(
    Guid RunId,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid RuleVersionId,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string Outcome,
    string MatchStatus,
    bool EvaluateOnCreate,
    DateTime CompletedAt,
    bool IsReplay);

public sealed record MatchFormationRunSummaryDto(
    Guid Id,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid RuleVersionId,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string Outcome,
    bool EvaluateOnCreate,
    DateTime CompletedAt);

public sealed record MatchFormationRunDetailDto(
    Guid Id,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid RuleVersionId,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string Outcome,
    bool EvaluateOnCreate,
    DateTime StartedAt,
    DateTime CompletedAt,
    string InputSnapshot);

public sealed record MatchReviewRequest(
    string SourceSystem,
    string IdempotencyKey,
    Guid BlissMatchId,
    string ReviewerLabel,
    string Decision,
    string Rationale);

public sealed record MatchReviewResultDto(
    Guid DecisionId,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid? MatchEvaluationRunId,
    string SourceSystem,
    string IdempotencyKey,
    string ReviewerLabel,
    string Decision,
    string ResultingMatchStatus,
    string Status,
    DateTime CompletedAt,
    bool IsReplay);

public sealed record MatchReviewDecisionSummaryDto(
    Guid Id,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid? MatchEvaluationRunId,
    string SourceSystem,
    string IdempotencyKey,
    string ReviewerLabel,
    string Decision,
    string ResultingMatchStatus,
    DateTime CompletedAt);

public sealed record MatchReviewDecisionDetailDto(
    Guid Id,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid? MatchEvaluationRunId,
    string SourceSystem,
    string IdempotencyKey,
    string ReviewerLabel,
    string Decision,
    string ResultingMatchStatus,
    string Rationale,
    string Status,
    DateTime StartedAt,
    DateTime CompletedAt,
    string InputSnapshot);

public sealed record MatchReviewQueueItemDto(
    Guid BlissMatchId,
    Guid CreatorId,
    string CreatorName,
    Guid AdvertiserOpportunityId,
    string OpportunityName,
    Guid RuleVersionId,
    string Status,
    decimal? OverallScore,
    decimal? ConfidenceScore,
    DateTime CreatedAt,
    int EvaluationRunCount);

public sealed record AffiliateNetworkDto(
    Guid Id,
    string Name,
    string? Website,
    string Status);

public sealed record NetworkAccessDto(
    Guid Id,
    Guid AdvertiserId,
    Guid AffiliateNetworkId,
    string Status,
    string? ExternalAccountId,
    DateTime? ApprovedAt);

public sealed record ProgramAccessDto(
    Guid Id,
    Guid AdvertiserProgramId,
    string Status,
    DateTime? ApprovedAt);

public sealed record DataProvenanceDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string FieldName,
    string SourceType,
    string? SourceName,
    string? SourceUrl,
    string ConfidenceLevel,
    DateTime CollectedAt,
    string? Notes);

public sealed record CampaignListDto(
    Guid Id,
    Guid? AdvertiserOpportunityId,
    string Name,
    string Status,
    DateTime CreatedAt);

public sealed record CampaignPlacementDto(
    Guid Id,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid? BlissMatchId,
    string Status,
    DateTime? StartAt,
    DateTime? EndAt);

public sealed record CampaignDetailDto(
    Guid Id,
    Guid? AdvertiserOpportunityId,
    string Name,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<CampaignPlacementDto> Placements);

public sealed record CampaignPlacementRequest(
    string SourceSystem,
    string IdempotencyKey,
    string OperatorLabel,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId);

public sealed record CampaignPlacementResultDto(
    Guid RunId,
    Guid CampaignPlacementId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    string SourceSystem,
    string IdempotencyKey,
    string OperatorLabel,
    string Status,
    string Outcome,
    DateTime CompletedAt,
    bool IsReplay);

public sealed record CampaignPlacementRunSummaryDto(
    Guid Id,
    Guid CampaignPlacementId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    string SourceSystem,
    string IdempotencyKey,
    string OperatorLabel,
    string Status,
    string Outcome,
    DateTime CompletedAt);

public sealed record CampaignPlacementRunDetailDto(
    Guid Id,
    Guid CampaignPlacementId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    string SourceSystem,
    string IdempotencyKey,
    string OperatorLabel,
    string Status,
    string Outcome,
    DateTime StartedAt,
    DateTime CompletedAt,
    string InputSnapshot);

public sealed record CampaignBindingQueueItemDto(
    Guid BlissMatchId,
    Guid CreatorId,
    string CreatorName,
    Guid AdvertiserOpportunityId,
    string OpportunityName,
    decimal? OverallScore,
    decimal? ConfidenceScore,
    int ContentItemCount,
    DateTime CreatedAt);

public sealed record OpenWeddingPlannerWorkspaceRequest(
    Guid AdvertiserId,
    string SourceSystem,
    string IdempotencyKey);

public sealed record CreateWeddingPlannerSessionRequest(
    string SourceSystem,
    string IdempotencyKey);

public sealed record AppendWeddingPlannerMessageRequest(
    string ActorType,
    string Body,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerWorkspaceDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    string AdvertiserName,
    bool IsPrimary,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsReplay);

public sealed record WeddingPlannerSessionDto(
    Guid SessionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MessageCount,
    bool IsReplay);

public sealed record WeddingPlannerMessageDto(
    Guid MessageId,
    Guid SessionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    int SequenceNumber,
    string ActorType,
    string ActorLabel,
    string Body,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    bool IsReplay);

public sealed record WeddingPlannerAuditEventDto(
    Guid Id,
    Guid AdvertiserId,
    Guid? WorkspaceId,
    Guid? SessionId,
    Guid? MessageId,
    string Action,
    string ActorType,
    string ActorLabel,
    string Outcome,
    string? RequestId,
    string? Detail,
    DateTime OccurredAt);

public sealed record WeddingPlannerTurnRequest(
    string Body,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerAgentRunDto(
    Guid AgentRunId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid? SessionId,
    string LogicalRole,
    string WorkerKey,
    string PromptPackVersion,
    string? ProviderKey,
    string? ModelId,
    string? AdapterVersion,
    Guid? TriggerMessageId,
    Guid? OutputMessageId,
    Guid? OutputBrandDnaVersionId,
    string? WorkerProfileVersion,
    string? AssignedRolesJson,
    Guid? OutputResearchReportVersionId,
    Guid? OutputConceptPackageVersionId,
    Guid? OutputCreativePackageVersionId,
    Guid? OutputQaReviewReportVersionId,
    Guid? OutputMeasurementLearningReportVersionId,
    string? RequestId,
    string? ProviderRequestId,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string? Outcome,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int? PromptTokens,
    int? CompletionTokens,
    int? TotalTokens,
    decimal? EstimatedCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerTurnDto(
    Guid AgentRunId,
    Guid SessionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    WeddingPlannerMessageDto HumanMessage,
    WeddingPlannerMessageDto? PlannerMessage,
    WeddingPlannerAgentRunDto AgentRun,
    bool IsReplay);

public sealed record InterpretWeddingPlannerBrandDnaRequest(
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerBrandDnaVersionDto(
    Guid BrandDnaVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingAgentRunId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    bool IsReplay);

public sealed record WeddingPlannerBrandDnaListDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedBrandDnaVersionId,
    IReadOnlyList<WeddingPlannerBrandDnaVersionDto> Versions);

public sealed record WeddingPlannerBrandDnaDecisionRequest(
    string Decision,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerBrandDnaDecisionDto(
    Guid DecisionId,
    Guid BrandDnaVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerBrandDnaVersionDto Version,
    bool IsReplay);

public sealed record ComputeWeddingPlannerColorProfileRequest(
    string PrimaryHex,
    string? SecondaryHex,
    string? AccentHex,
    string? BackgroundHex,
    string? SurfaceHex,
    string? Notes,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerColorProfileVersionDto(
    Guid ColorProfileVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string AlgorithmVersion,
    Guid ApprovedBrandDnaVersionId,
    string DocumentJson,
    string Summary,
    string InputJson,
    string InputSha256,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    bool IsReplay);

public sealed record WeddingPlannerColorProfileListDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedColorProfileVersionId,
    IReadOnlyList<WeddingPlannerColorProfileVersionDto> Versions);

public sealed record WeddingPlannerColorProfileDecisionRequest(
    string Decision,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerColorProfileDecisionDto(
    Guid DecisionId,
    Guid ColorProfileVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerColorProfileVersionDto Version,
    bool IsReplay);

public sealed record CreateWeddingPlannerResearchJobRequest(
    string Topic,
    string Objective,
    IReadOnlyList<string> Questions,
    string Geography,
    string Language,
    IReadOnlyList<string>? AllowedDomains,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerResearchJobDto(
    Guid ResearchJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string Topic,
    string Objective,
    IReadOnlyList<string> Questions,
    string Geography,
    string Language,
    IReadOnlyList<string> AllowedDomains,
    string InputJson,
    string InputSha256,
    Guid ApprovedBrandDnaVersionId,
    Guid? ApprovedColorProfileVersionId,
    string? ResearchProviderKey,
    string? ResearchAdapterVersion,
    string? ResearchProviderRequestId,
    string? ResearchWorkerKey,
    decimal? ResearchEstimatedCostUsd,
    string? SourceCatalogJson,
    Guid? ResearchAgentRunId,
    Guid? EvidenceAgentRunId,
    Guid? SynthesisRiskAgentRunId,
    Guid? OutputResearchReportVersionId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool IsReplay);

public sealed record WeddingPlannerResearchReportVersionDto(
    Guid ResearchReportVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingResearchJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedBrandDnaVersionId,
    Guid? ApprovedColorProfileVersionId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerResearchReportListDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedResearchReportVersionId,
    IReadOnlyList<WeddingPlannerResearchReportVersionDto> Versions);

public sealed record WeddingPlannerResearchRoleContributionDto(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid ResearchReportVersionId,
    Guid ResearchJobId,
    string LogicalRole,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerResearchReportDecisionRequest(
    string Decision,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerResearchReportDecisionDto(
    Guid DecisionId,
    Guid ResearchReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerResearchReportVersionDto Version,
    bool IsReplay);

public sealed record CreateWeddingPlannerWorkshopJobRequest(
    string Objective,
    string CampaignGoal,
    string AudienceFocus,
    string ChannelFormat,
    IReadOnlyList<string> Deliverables,
    string Cta,
    IReadOnlyList<string>? Constraints,
    int? CanvasWidth,
    int? CanvasHeight,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerWorkshopJobDto(
    Guid WorkshopJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string Objective,
    string CampaignGoal,
    string AudienceFocus,
    string ChannelFormat,
    int CanvasWidth,
    int CanvasHeight,
    IReadOnlyList<string> Deliverables,
    string Cta,
    IReadOnlyList<string> Constraints,
    string InputJson,
    string InputSha256,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    Guid? StrategyAgentRunId,
    Guid? CreativeAgentRunId,
    Guid? ProductionAgentRunId,
    Guid? OutputConceptPackageVersionId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool IsReplay);

public sealed record WeddingPlannerConceptPackageVersionDto(
    Guid ConceptPackageVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingWorkshopJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string ChannelFormat,
    int CanvasWidth,
    int CanvasHeight,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerConceptPackageListDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedConceptPackageVersionId,
    IReadOnlyList<WeddingPlannerConceptPackageVersionDto> Versions);

public sealed record WeddingPlannerConceptRoleContributionDto(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid ConceptPackageVersionId,
    Guid WorkshopJobId,
    string LogicalRole,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerConceptPackageDecisionRequest(
    string Decision,
    string Rationale,
    string? SelectedConceptId,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerConceptPackageDecisionDto(
    Guid DecisionId,
    Guid ConceptPackageVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string? SelectedConceptId,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerConceptPackageVersionDto Version,
    bool IsReplay);

public sealed record CreateWeddingPlannerCreativeProductionJobRequest(
    string JobKind,
    string Objective,
    IReadOnlyList<string> Formats,
    int RequestedVariantCount,
    Guid? RevisionParentCreativePackageVersionId,
    string? RevisionNotes,
    int? CanvasWidth,
    int? CanvasHeight,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerCreativeProductionJobDto(
    Guid CreativeProductionJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string JobKind,
    string Objective,
    IReadOnlyList<string> Formats,
    int RequestedVariantCount,
    Guid? RevisionParentCreativePackageVersionId,
    string? RevisionNotes,
    string InputJson,
    string InputSha256,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    Guid? CreativeDirectionAgentRunId,
    Guid? StrategyAdaptationAgentRunId,
    Guid? VisualSystemAgentRunId,
    Guid? ImageDirectionAgentRunId,
    Guid? CopySystemAgentRunId,
    Guid? VariantProductionAgentRunId,
    string? AssetProviderKey,
    string? AssetProviderAdapterVersion,
    string? AssetProviderRequestId,
    decimal? AssetProviderEstimatedCostUsd,
    Guid? OutputCreativePackageVersionId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool IsReplay);

public sealed record WeddingPlannerCreativePackageVersionDto(
    Guid CreativePackageVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingCreativeProductionJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string JobKind,
    Guid? ParentCreativePackageVersionId,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentApproved,
    decimal? EstimatedTotalCostUsd,
    decimal? EstimatedAssetCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerCreativePackageListDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentApprovedCreativePackageVersionId,
    IReadOnlyList<WeddingPlannerCreativePackageVersionDto> Versions);

public sealed record WeddingPlannerCreativeRoleContributionDto(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid CreativePackageVersionId,
    Guid CreativeProductionJobId,
    string LogicalRole,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerCreativeAssetDto(
    Guid CreativeAssetId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid CreativePackageVersionId,
    Guid CreativeProductionJobId,
    string VariantId,
    string Format,
    int Width,
    int Height,
    string ContentType,
    int ByteSize,
    string Sha256,
    string ProviderKey,
    string AdapterVersion,
    string? ProviderRequestId,
    decimal? EstimatedCostUsd,
    DateTime CreatedAt);

public sealed record WeddingPlannerCreativePackageDecisionRequest(
    string Decision,
    string Rationale,
    string? SelectedVariantId,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerCreativePackageDecisionDto(
    Guid DecisionId,
    Guid CreativePackageVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string? SelectedVariantId,
    string ActorType,
    string ActorLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerCreativePackageVersionDto Version,
    bool IsReplay);

public sealed record CreateWeddingPlannerQaReviewJobRequest(
    string ReviewObjective,
    IReadOnlyList<string> FocusAreas,
    string? Notes,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerQaReviewJobDto(
    Guid QaReviewJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string ReviewObjective,
    IReadOnlyList<string> FocusAreas,
    string? Notes,
    string InputJson,
    string InputSha256,
    Guid ApprovedCreativePackageVersionId,
    string CreativePackageDocumentSha256,
    Guid CreativePackageDecisionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    string SelectedCreativeAssetSha256,
    string SelectedCreativeAssetContentType,
    int SelectedCreativeAssetByteSize,
    int SelectedCreativeAssetWidth,
    int SelectedCreativeAssetHeight,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string? RulesFindingsJson,
    string? RulesOverallSeverity,
    Guid? ChaperoneReviewAgentRunId,
    Guid? QaInspectionAgentRunId,
    Guid? OutputQaReviewReportVersionId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool IsReplay);

public sealed record WeddingPlannerQaReviewReportVersionDto(
    Guid QaReviewReportVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingQaReviewJobId,
    Guid ProducingAgentRunId,
    Guid ApprovedCreativePackageVersionId,
    string CreativePackageDocumentSha256,
    Guid CreativePackageDecisionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    string SelectedCreativeAssetSha256,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentAccepted,
    string? RulesOverallSeverity,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerQaReviewReportListDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentAcceptedQaReviewReportVersionId,
    IReadOnlyList<WeddingPlannerQaReviewReportVersionDto> Versions);

public sealed record WeddingPlannerQaRoleContributionDto(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid QaReviewReportVersionId,
    Guid QaReviewJobId,
    string LogicalRole,
    string ContributionSource,
    Guid? ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerQaReviewDecisionRequest(
    string Decision,
    string Rationale,
    string SelectedVariantId,
    bool? VisualReviewConfirmed,
    bool? CopyReviewConfirmed,
    bool? ProvenanceReviewConfirmed,
    bool? SyntheticMarkerAcknowledged,
    string? EscalationCategory,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerQaReviewDecisionDto(
    Guid DecisionId,
    Guid QaReviewReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string SelectedVariantId,
    string Rationale,
    bool? VisualReviewConfirmed,
    bool? CopyReviewConfirmed,
    bool? ProvenanceReviewConfirmed,
    bool? SyntheticMarkerAcknowledged,
    string? EscalationCategory,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerQaReviewReportVersionDto Version,
    Guid? EscalationCaseId,
    bool IsReplay);

public sealed record WeddingPlannerQaEscalationCaseDto(
    Guid EscalationCaseId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid QaReviewReportVersionId,
    Guid QaReviewDecisionId,
    string Category,
    string Status,
    string RationaleSnapshot,
    string SelectedVariantId,
    Guid ApprovedCreativePackageVersionId,
    Guid SelectedCreativeAssetId,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime CreatedAt,
    Guid? ResolutionId,
    bool IsReplay);

public sealed record WeddingPlannerQaEscalationResolutionRequest(
    string Resolution,
    string Rationale,
    string? ExceptionRationale,
    bool? ExceptionAcknowledged,
    IReadOnlyList<string>? AcknowledgedBlockerCodes,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerQaEscalationResolutionDto(
    Guid ResolutionId,
    Guid EscalationCaseId,
    Guid QaReviewReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Resolution,
    string Rationale,
    string? ExceptionRationale,
    bool? ExceptionAcknowledged,
    IReadOnlyList<string>? AcknowledgedBlockerCodes,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerQaEscalationCaseDto Case,
    WeddingPlannerQaReviewReportVersionDto Version,
    bool IsReplay);

public sealed record CommitWeddingPlannerCampaignReadinessHandshakeRequest(
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    string Rationale,
    bool DisclaimerAcknowledged,
    bool? SyntheticMarkerAcknowledged,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerCampaignReadinessDecisionRequest(
    string Decision,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerCampaignReadinessHandshakeDto(
    Guid CampaignReadinessHandshakeVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    string Status,
    Guid QaReviewReportVersionId,
    Guid QaAcceptDecisionId,
    Guid ApprovedCreativePackageVersionId,
    string CreativePackageDocumentSha256,
    Guid CreativePackageDecisionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    string SelectedCreativeAssetSha256,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid CampaignPlacementId,
    Guid CampaignPlacementRunId,
    string RulesFindingsJson,
    string Rationale,
    bool DisclaimerAcknowledged,
    bool? SyntheticMarkerAcknowledged,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrent,
    bool IsReplay);

public sealed record WeddingPlannerCampaignReadinessDecisionDto(
    Guid CampaignReadinessDecisionId,
    Guid CampaignReadinessHandshakeVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    string Decision,
    string Rationale,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerCampaignReadinessHandshakeDto Version,
    bool IsReplay);

public sealed record WeddingPlannerCampaignReadinessEligibilityDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    bool HasCurrentQaPointer,
    Guid? CurrentAcceptedQaReviewReportVersionId,
    string? CurrentQaStatus,
    bool IsCleanAccepted,
    bool HasCleanAcceptDecision,
    Guid? CurrentApprovedCreativePackageVersionId,
    bool PackageReady,
    bool HasSyntheticUpstream,
    Guid? CurrentCampaignReadinessHandshakeVersionId,
    string NoReservationDisclosure,
    IReadOnlyList<CampaignReadinessMatchCandidateDto> Candidates);

public sealed record CampaignReadinessMatchCandidateDto(
    Guid BlissMatchId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    string MatchStatus,
    decimal? OverallScore,
    string OpportunityStatus,
    string OpportunityName,
    IReadOnlyList<CampaignReadinessCampaignCandidateDto> Campaigns,
    IReadOnlyList<CampaignReadinessContentCandidateDto> ContentItems);

public sealed record CampaignReadinessCampaignCandidateDto(
    Guid CampaignId,
    string Name,
    string Status,
    Guid? AdvertiserOpportunityId,
    bool OpportunityCompatible);

public sealed record CampaignReadinessContentCandidateDto(
    Guid ContentItemId,
    string Title,
    string ContentType,
    IReadOnlyList<CampaignReadinessSlotCandidateDto> Slots);

public sealed record CampaignReadinessSlotCandidateDto(
    Guid AdInventorySlotId,
    string SlotType,
    bool IsAvailable,
    string AvailabilityNote);

public sealed record CreateWeddingPlannerMeasurementLearningJobRequest(
    Guid CampaignReadinessHandshakeVersionId,
    DateTime ObservationStart,
    DateTime ObservationEnd,
    string SourceLabel,
    string SourceSystem,
    bool AttestationAcknowledged,
    long Impressions,
    long Clicks,
    long Conversions,
    decimal Spend,
    decimal? Revenue,
    string CurrencyCode,
    string? Notes,
    string IdempotencyKey);

public sealed record WeddingPlannerMeasurementLearningJobDto(
    Guid MeasurementLearningJobId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid CampaignReadinessHandshakeVersionId,
    string HandshakeStatusSnapshot,
    bool HandshakeWasCurrentAtJobStart,
    Guid CampaignPlacementId,
    Guid CampaignPlacementRunId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid QaReviewReportVersionId,
    Guid ApprovedCreativePackageVersionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    DateTime ObservationStart,
    DateTime ObservationEnd,
    string SourceLabel,
    bool AttestationAcknowledged,
    long Impressions,
    long Clicks,
    long Conversions,
    decimal Spend,
    decimal? Revenue,
    string CurrencyCode,
    string? Notes,
    string InputJson,
    string InputSha256,
    string? MetricsJson,
    string? RulesFindingsJson,
    string? RulesOverallSeverity,
    Guid? PerformanceAnalysisAgentRunId,
    Guid? LearningSynthesisAgentRunId,
    Guid? OutputMeasurementLearningReportVersionId,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime StartedAt,
    DateTime? CompletedAt,
    bool IsReplay);

public sealed record WeddingPlannerMeasurementLearningReportVersionDto(
    Guid MeasurementLearningReportVersionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    int VersionNumber,
    string SchemaVersion,
    string DocumentJson,
    string Summary,
    Guid ProducingMeasurementLearningJobId,
    Guid ProducingAgentRunId,
    Guid PerformanceAnalysisAgentRunId,
    Guid LearningSynthesisAgentRunId,
    Guid CampaignReadinessHandshakeVersionId,
    string HandshakeStatusSnapshot,
    bool HandshakeWasCurrentAtJobStart,
    Guid CampaignPlacementId,
    Guid CampaignPlacementRunId,
    Guid BlissMatchId,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    Guid QaReviewReportVersionId,
    Guid ApprovedCreativePackageVersionId,
    string SelectedVariantId,
    Guid SelectedCreativeAssetId,
    Guid ApprovedConceptPackageVersionId,
    string SelectedConceptId,
    Guid ApprovedBrandDnaVersionId,
    int ApprovedBrandDnaVersionNumber,
    Guid ApprovedColorProfileVersionId,
    int ApprovedColorProfileVersionNumber,
    Guid ApprovedResearchReportVersionId,
    int ApprovedResearchReportVersionNumber,
    DateTime ObservationStart,
    DateTime ObservationEnd,
    string SourceLabel,
    string ObservationSourceSystem,
    long Impressions,
    long Clicks,
    long Conversions,
    decimal Spend,
    decimal? Revenue,
    string CurrencyCode,
    string MetricsJson,
    string RulesFindingsJson,
    string Status,
    string SourceSystem,
    string IdempotencyKey,
    string ActorType,
    string ActorLabel,
    DateTime CreatedAt,
    bool IsCurrentAccepted,
    decimal? EstimatedTotalCostUsd,
    bool IsReplay);

public sealed record WeddingPlannerMeasurementLearningReportListDto(
    Guid WorkspaceId,
    Guid AdvertiserId,
    Guid? CurrentAcceptedMeasurementLearningReportVersionId,
    IReadOnlyList<WeddingPlannerMeasurementLearningReportVersionDto> Versions);

public sealed record WeddingPlannerMeasurementLearningRoleContributionDto(
    Guid ContributionId,
    Guid AdvertiserId,
    Guid WorkspaceId,
    Guid MeasurementLearningReportVersionId,
    Guid MeasurementLearningJobId,
    string LogicalRole,
    string ContributionSource,
    Guid ProducingAgentRunId,
    string ContributionJson,
    DateTime CreatedAt);

public sealed record WeddingPlannerMeasurementLearningDecisionRequest(
    string Decision,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey);

public sealed record WeddingPlannerMeasurementLearningDecisionDto(
    Guid DecisionId,
    Guid MeasurementLearningReportVersionId,
    Guid WorkspaceId,
    Guid AdvertiserId,
    string Decision,
    string Rationale,
    string ActorType,
    string ActorLabel,
    string SourceSystem,
    string IdempotencyKey,
    DateTime OccurredAt,
    WeddingPlannerMeasurementLearningReportVersionDto Version,
    bool IsReplay);

