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


