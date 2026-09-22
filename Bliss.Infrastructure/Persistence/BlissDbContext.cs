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
    public DbSet<WeddingPlannerAgentRun> WeddingPlannerAgentRuns => Set<WeddingPlannerAgentRun>();
    public DbSet<WeddingPlannerBrandDnaVersion> WeddingPlannerBrandDnaVersions => Set<WeddingPlannerBrandDnaVersion>();
    public DbSet<WeddingPlannerBrandDnaDecision> WeddingPlannerBrandDnaDecisions => Set<WeddingPlannerBrandDnaDecision>();
    public DbSet<WeddingPlannerColorProfileVersion> WeddingPlannerColorProfileVersions => Set<WeddingPlannerColorProfileVersion>();
    public DbSet<WeddingPlannerColorProfileDecision> WeddingPlannerColorProfileDecisions => Set<WeddingPlannerColorProfileDecision>();
    public DbSet<WeddingPlannerResearchJob> WeddingPlannerResearchJobs => Set<WeddingPlannerResearchJob>();
    public DbSet<WeddingPlannerResearchReportVersion> WeddingPlannerResearchReportVersions => Set<WeddingPlannerResearchReportVersion>();
    public DbSet<WeddingPlannerResearchRoleContribution> WeddingPlannerResearchRoleContributions => Set<WeddingPlannerResearchRoleContribution>();
    public DbSet<WeddingPlannerResearchReportDecision> WeddingPlannerResearchReportDecisions => Set<WeddingPlannerResearchReportDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BlissDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
