using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public class BlissDbContext : DbContext
{
    public BlissDbContext(DbContextOptions<BlissDbContext> options)
        : base(options)
    {
    }

    public DbSet<Creator> Creators => Set<Creator>();
    public DbSet<CreatorPlatform> CreatorPlatforms => Set<CreatorPlatform>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<AdInventorySlot> AdInventorySlots => Set<AdInventorySlot>();

    public DbSet<Advertiser> Advertisers => Set<Advertiser>();
    public DbSet<AdvertiserProgram> AdvertiserPrograms => Set<AdvertiserProgram>();
    public DbSet<AdvertiserOpportunity> AdvertiserOpportunities => Set<AdvertiserOpportunity>();

    public DbSet<AffiliateNetwork> AffiliateNetworks => Set<AffiliateNetwork>();
    public DbSet<NetworkAccess> NetworkAccesses => Set<NetworkAccess>();
    public DbSet<ProgramAccess> ProgramAccesses => Set<ProgramAccess>();

    public DbSet<BlissMatch> BlissMatches => Set<BlissMatch>();
    public DbSet<MatchScoreComponent> MatchScoreComponents => Set<MatchScoreComponent>();
    public DbSet<EligibilityCheck> EligibilityChecks => Set<EligibilityCheck>();
    public DbSet<RuleVersion> RuleVersions => Set<RuleVersion>();

    public DbSet<DataProvenance> DataProvenances => Set<DataProvenance>();
    public DbSet<CreatorIngestionRun> CreatorIngestionRuns => Set<CreatorIngestionRun>();
    public DbSet<MatchFormationRun> MatchFormationRuns => Set<MatchFormationRun>();
    public DbSet<MatchReviewDecision> MatchReviewDecisions => Set<MatchReviewDecision>();

    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignPlacement> CampaignPlacements => Set<CampaignPlacement>();
    public DbSet<CampaignPlacementRun> CampaignPlacementRuns => Set<CampaignPlacementRun>();

    public DbSet<MatchEvaluationRun> MatchEvaluationRuns => Set<MatchEvaluationRun>();

    public DbSet<WeddingPlannerWorkspace> WeddingPlannerWorkspaces => Set<WeddingPlannerWorkspace>();
    public DbSet<WeddingPlannerPlanningSession> WeddingPlannerPlanningSessions => Set<WeddingPlannerPlanningSession>();
    public DbSet<WeddingPlannerConversationMessage> WeddingPlannerConversationMessages => Set<WeddingPlannerConversationMessage>();
    public DbSet<WeddingPlannerAuditEvent> WeddingPlannerAuditEvents => Set<WeddingPlannerAuditEvent>();
    public DbSet<WeddingPlannerEconomicsRequest> WeddingPlannerEconomicsRequests =>
        Set<WeddingPlannerEconomicsRequest>();

    public DbSet<GeographicMarket> GeographicMarkets => Set<GeographicMarket>();
    public DbSet<PricingModel> PricingModels => Set<PricingModel>();
    public DbSet<ResearchSource> ResearchSources => Set<ResearchSource>();
    public DbSet<MarketBenchmarkObservation> MarketBenchmarkObservations => Set<MarketBenchmarkObservation>();
    public DbSet<CreatorAudienceSnapshot> CreatorAudienceSnapshots => Set<CreatorAudienceSnapshot>();
    public DbSet<CreatorPerformanceSnapshot> CreatorPerformanceSnapshots => Set<CreatorPerformanceSnapshot>();
    public DbSet<MarketEconomicProfile> MarketEconomicProfiles => Set<MarketEconomicProfile>();
    public DbSet<IndustryEconomicProfile> IndustryEconomicProfiles => Set<IndustryEconomicProfile>();
    public DbSet<InventoryRateBenchmark> InventoryRateBenchmarks => Set<InventoryRateBenchmark>();
    public DbSet<ExchangeRateObservation> ExchangeRateObservations => Set<ExchangeRateObservation>();
    public DbSet<PricingRuleVersion> PricingRuleVersions => Set<PricingRuleVersion>();
    public DbSet<RateRecommendation> RateRecommendations => Set<RateRecommendation>();
    public DbSet<RateRecommendationFactor> RateRecommendationFactors => Set<RateRecommendationFactor>();
    public DbSet<RateRecommendationSource> RateRecommendationSources => Set<RateRecommendationSource>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<QuoteVersion> QuoteVersions => Set<QuoteVersion>();
    public DbSet<QuoteLineItem> QuoteLineItems => Set<QuoteLineItem>();
    public DbSet<QuoteApprovalDecision> QuoteApprovalDecisions => Set<QuoteApprovalDecision>();
    public DbSet<QuoteOutcome> QuoteOutcomes => Set<QuoteOutcome>();
    public DbSet<CompensationRuleVersion> CompensationRuleVersions => Set<CompensationRuleVersion>();
    public DbSet<CompensationRuleAllocation> CompensationRuleAllocations => Set<CompensationRuleAllocation>();
    public DbSet<CompensationIllustration> CompensationIllustrations => Set<CompensationIllustration>();
    public DbSet<CompensationIllustrationLine> CompensationIllustrationLines => Set<CompensationIllustrationLine>();
    public DbSet<EconomicsResearchRun> EconomicsResearchRuns => Set<EconomicsResearchRun>();
    public DbSet<EconomicsResearchCandidate> EconomicsResearchCandidates => Set<EconomicsResearchCandidate>();
    public DbSet<EconomicsResearchReviewDecision> EconomicsResearchReviewDecisions => Set<EconomicsResearchReviewDecision>();
    public DbSet<HistoricalPlacementEconomics> HistoricalPlacementEconomics =>
        Set<HistoricalPlacementEconomics>();
    public DbSet<CampaignPerformanceEconomics> CampaignPerformanceEconomics =>
        Set<CampaignPerformanceEconomics>();
    public DbSet<ProspectMemory> ProspectMemories => Set<ProspectMemory>();
    public DbSet<SourceClipMemory> SourceClipMemories => Set<SourceClipMemory>();
    public DbSet<FactoryBatchMemory> FactoryBatchMemories => Set<FactoryBatchMemory>();
    public DbSet<LaneTempoState> LaneTempoStates => Set<LaneTempoState>();
    public DbSet<LaneTempoAudit> LaneTempoAudits => Set<LaneTempoAudit>();

    public DbSet<SubscriptionRegisterRow> SubscriptionRegisterRows => Set<SubscriptionRegisterRow>();
    public DbSet<SubscriptionLedgerAudit> SubscriptionLedgerAudits => Set<SubscriptionLedgerAudit>();
    public DbSet<FactoryBudgetState> FactoryBudgetStates => Set<FactoryBudgetState>();
    public DbSet<FactoryBudgetAudit> FactoryBudgetAudits => Set<FactoryBudgetAudit>();
    public DbSet<ContactRouteAuditRow> ContactRouteAudits => Set<ContactRouteAuditRow>();
    public DbSet<BatchMeasurementRow> BatchMeasurements => Set<BatchMeasurementRow>();
    public DbSet<MarketplaceHandoffRow> MarketplaceHandoffs => Set<MarketplaceHandoffRow>();
    public DbSet<CreativeApprovalRow> CreativeApprovals => Set<CreativeApprovalRow>();
    public DbSet<LearningNoteRow> LearningNotes => Set<LearningNoteRow>();
    public DbSet<RotationPeriodRow> RotationPeriods => Set<RotationPeriodRow>();
    public DbSet<EconomicsPhaseAcceptanceRow> EconomicsPhaseAcceptances => Set<EconomicsPhaseAcceptanceRow>();
    public DbSet<HostedAcceptanceRow> HostedAcceptanceReadings => Set<HostedAcceptanceRow>();
    public DbSet<CoverageWeekRow> CoverageWeeks => Set<CoverageWeekRow>();
    public DbSet<MarketplaceMetricRow> MarketplaceMetricReadings => Set<MarketplaceMetricRow>();
    public DbSet<ClosedModelRow> ClosedModelReadings => Set<ClosedModelRow>();
    public DbSet<CreativeAcademyLessonRow> CreativeAcademyLessons => Set<CreativeAcademyLessonRow>();
    public DbSet<CreativeAcademyDnaRow> CreativeAcademyDna => Set<CreativeAcademyDnaRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BlissDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
