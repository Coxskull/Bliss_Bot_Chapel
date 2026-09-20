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
    string Name,
    string Status,
    DateTime CreatedAt);

public sealed record CampaignPlacementDto(
    Guid Id,
    Guid CampaignId,
    Guid ContentItemId,
    Guid AdInventorySlotId,
    string Status,
    DateTime? StartAt,
    DateTime? EndAt);

public sealed record CampaignDetailDto(
    Guid Id,
    string Name,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<CampaignPlacementDto> Placements);

