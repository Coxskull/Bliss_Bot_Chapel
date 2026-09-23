using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Architecture;

public sealed class EfModelConstraintTests
{
    [Fact]
    public void Bliss_match_creator_id_is_not_unique()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(BlissMatch));
        Assert.NotNull(entity);

        var uniqueCreatorIndexes = entity!.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(BlissMatch.CreatorId));

        Assert.Empty(uniqueCreatorIndexes);
    }

    [Fact]
    public void Campaign_placement_content_item_id_is_not_unique()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(CampaignPlacement));
        Assert.NotNull(entity);

        var uniqueContentIndexes = entity!.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(CampaignPlacement.ContentItemId));

        Assert.Empty(uniqueContentIndexes);
    }

    [Fact]
    public void Major_relationships_use_restrict_delete_behavior()
    {
        using var db = TestDb.CreateContext();
        var restrictKeys = db.Model.GetEntityTypes()
            .SelectMany(e => e.GetForeignKeys())
            .Where(fk => fk.DeleteBehavior == DeleteBehavior.Restrict)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}.{fk.Properties[0].Name}")
            .ToHashSet();

        Assert.Contains("BlissMatch.CreatorId", restrictKeys);
        Assert.Contains("BlissMatch.AdvertiserOpportunityId", restrictKeys);
        Assert.Contains("BlissMatch.RuleVersionId", restrictKeys);
        Assert.Contains("ContentItem.CreatorId", restrictKeys);
        Assert.Contains("CampaignPlacement.ContentItemId", restrictKeys);
        Assert.Contains("CampaignPlacement.CampaignId", restrictKeys);
        Assert.Contains("NetworkAccess.AdvertiserId", restrictKeys);
        Assert.Contains("ProgramAccess.AdvertiserProgramId", restrictKeys);
        Assert.Contains("MatchEvaluationRun.BlissMatchId", restrictKeys);
        Assert.Contains("MatchEvaluationRun.CreatorId", restrictKeys);
        Assert.Contains("MatchEvaluationRun.RuleVersionId", restrictKeys);
        Assert.Contains("CreatorIngestionRun.CreatorId", restrictKeys);
        Assert.Contains("CreatorIngestionRun.CreatorPlatformId", restrictKeys);
        Assert.Contains("MatchFormationRun.BlissMatchId", restrictKeys);
        Assert.Contains("MatchFormationRun.CreatorId", restrictKeys);
        Assert.Contains("MatchFormationRun.AdvertiserOpportunityId", restrictKeys);
        Assert.Contains("MatchFormationRun.RuleVersionId", restrictKeys);
        Assert.Contains("MatchReviewDecision.BlissMatchId", restrictKeys);
        Assert.Contains("MatchReviewDecision.CreatorId", restrictKeys);
        Assert.Contains("MatchReviewDecision.MatchEvaluationRunId", restrictKeys);
        Assert.Contains("Campaign.AdvertiserOpportunityId", restrictKeys);
        Assert.Contains("CampaignPlacement.BlissMatchId", restrictKeys);
        Assert.Contains("CampaignPlacementRun.CampaignPlacementId", restrictKeys);
        Assert.Contains("CampaignPlacementRun.BlissMatchId", restrictKeys);
        Assert.Contains("CampaignPlacementRun.CampaignId", restrictKeys);
        Assert.Contains("CampaignPlacementRun.CreatorId", restrictKeys);
        Assert.Contains("CampaignPlacementRun.AdvertiserOpportunityId", restrictKeys);
        Assert.Contains("CampaignPlacementRun.ContentItemId", restrictKeys);
        Assert.Contains("CampaignPlacementRun.AdInventorySlotId", restrictKeys);
        Assert.Contains("WeddingPlannerWorkspace.AdvertiserId", restrictKeys);
        Assert.Contains("WeddingPlannerPlanningSession.WorkspaceId", restrictKeys);
        Assert.Contains("WeddingPlannerPlanningSession.AdvertiserId", restrictKeys);
        Assert.Contains("WeddingPlannerConversationMessage.SessionId", restrictKeys);
        Assert.Contains("WeddingPlannerConversationMessage.WorkspaceId", restrictKeys);
        Assert.Contains("WeddingPlannerConversationMessage.AdvertiserId", restrictKeys);
        Assert.Contains("WeddingPlannerAuditEvent.AdvertiserId", restrictKeys);
        Assert.Contains("MarketBenchmarkObservation.GeographicMarketId", restrictKeys);
        Assert.Contains("MarketBenchmarkObservation.ResearchSourceId", restrictKeys);
        Assert.Contains("CreatorAudienceSnapshot.CreatorId", restrictKeys);
        Assert.Contains("CreatorAudienceSnapshot.GeographicMarketId", restrictKeys);
        Assert.Contains("CreatorAudienceSnapshot.ResearchSourceId", restrictKeys);
        Assert.Contains("CreatorPerformanceSnapshot.CreatorId", restrictKeys);
        Assert.Contains("CreatorPerformanceSnapshot.ContentItemId", restrictKeys);
        Assert.Contains("CreatorPerformanceSnapshot.ResearchSourceId", restrictKeys);
        Assert.Contains("MarketEconomicProfile.GeographicMarketId", restrictKeys);
        Assert.Contains("MarketEconomicProfile.ResearchSourceId", restrictKeys);
        Assert.Contains("IndustryEconomicProfile.GeographicMarketId", restrictKeys);
        Assert.Contains("IndustryEconomicProfile.ResearchSourceId", restrictKeys);
        Assert.Contains("InventoryRateBenchmark.GeographicMarketId", restrictKeys);
        Assert.Contains("InventoryRateBenchmark.PricingModelId", restrictKeys);
        Assert.Contains("InventoryRateBenchmark.ResearchSourceId", restrictKeys);
        Assert.Contains("ExchangeRateObservation.ResearchSourceId", restrictKeys);
        Assert.Contains("RateRecommendation.CreatorId", restrictKeys);
        Assert.Contains("RateRecommendation.AdInventorySlotId", restrictKeys);
        Assert.Contains("RateRecommendation.GeographicMarketId", restrictKeys);
        Assert.Contains("RateRecommendation.PricingModelId", restrictKeys);
        Assert.Contains("RateRecommendation.PricingRuleVersionId", restrictKeys);
        Assert.Contains("RateRecommendationFactor.RateRecommendationId", restrictKeys);
        Assert.Contains("RateRecommendationSource.RateRecommendationId", restrictKeys);
        Assert.Contains("RateRecommendationSource.ResearchSourceId", restrictKeys);
        Assert.Contains("Quote.AdvertiserOpportunityId", restrictKeys);
        Assert.Contains("QuoteVersion.QuoteId", restrictKeys);
        Assert.Contains("QuoteVersion.ParentVersionId", restrictKeys);
        Assert.Contains("QuoteLineItem.QuoteVersionId", restrictKeys);
        Assert.Contains("QuoteLineItem.RateRecommendationId", restrictKeys);
        Assert.Contains("QuoteApprovalDecision.QuoteId", restrictKeys);
        Assert.Contains("QuoteApprovalDecision.QuoteVersionId", restrictKeys);
        Assert.Contains("QuoteOutcome.QuoteId", restrictKeys);
        Assert.Contains("QuoteOutcome.QuoteVersionId", restrictKeys);
        Assert.Contains("QuoteOutcome.NewQuoteVersionId", restrictKeys);
    }

    [Fact]
    public void Economics_catalog_natural_keys_are_unique()
    {
        using var db = TestDb.CreateContext();

        var marketCode = db.Model.FindEntityType(typeof(GeographicMarket))!.GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(GeographicMarket.MarketCode) }));
        var pricingCode = db.Model.FindEntityType(typeof(PricingModel))!.GetIndexes()
            .Single(x => x.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(PricingModel.Code) }));

        Assert.True(marketCode.IsUnique);
        Assert.True(pricingCode.IsUnique);
    }

    [Fact]
    public void Match_evaluation_run_bliss_match_id_is_not_unique()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(MatchEvaluationRun));
        Assert.NotNull(entity);

        var uniqueMatchIndexes = entity!.GetIndexes()
            .Where(i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(MatchEvaluationRun.BlissMatchId));

        Assert.Empty(uniqueMatchIndexes);
    }

    [Fact]
    public void Creator_platform_identity_key_is_unique_when_present()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(CreatorPlatform));
        Assert.NotNull(entity);

        var identityIndex = entity!.GetIndexes()
            .Single(i => i.Properties.Count == 1 && i.Properties[0].Name == nameof(CreatorPlatform.IdentityKey));

        Assert.True(identityIndex.IsUnique);
        Assert.Equal("\"IdentityKey\" IS NOT NULL", identityIndex.GetFilter());
    }

    [Fact]
    public void Creator_ingestion_source_and_idempotency_key_are_unique_together()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(CreatorIngestionRun));
        Assert.NotNull(entity);

        var idempotencyIndex = entity!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(CreatorIngestionRun.SourceSystem),
                nameof(CreatorIngestionRun.IdempotencyKey)
            }));

        Assert.True(idempotencyIndex.IsUnique);
    }

    [Fact]
    public void Match_formation_source_and_idempotency_key_are_unique_together()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(MatchFormationRun));
        Assert.NotNull(entity);

        var idempotencyIndex = entity!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(MatchFormationRun.SourceSystem),
                nameof(MatchFormationRun.IdempotencyKey)
            }));

        Assert.True(idempotencyIndex.IsUnique);
        Assert.DoesNotContain(entity.GetIndexes(), i =>
            i.IsUnique && i.Properties.Count == 1
            && i.Properties[0].Name == nameof(MatchFormationRun.CreatorId));
    }

    [Fact]
    public void Match_review_source_and_idempotency_key_are_unique_together()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(MatchReviewDecision));
        Assert.NotNull(entity);

        var idempotencyIndex = entity!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(MatchReviewDecision.SourceSystem),
                nameof(MatchReviewDecision.IdempotencyKey)
            }));

        Assert.True(idempotencyIndex.IsUnique);
    }

    [Fact]
    public void Campaign_placement_run_idempotency_is_unique_but_business_keys_are_not()
    {
        using var db = TestDb.CreateContext();
        var run = db.Model.FindEntityType(typeof(CampaignPlacementRun));
        Assert.NotNull(run);

        var idempotencyIndex = run!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(CampaignPlacementRun.SourceSystem),
                nameof(CampaignPlacementRun.IdempotencyKey)
            }));
        Assert.True(idempotencyIndex.IsUnique);

        var placement = db.Model.FindEntityType(typeof(CampaignPlacement))!;
        Assert.DoesNotContain(placement.GetIndexes(), i =>
            i.IsUnique && i.Properties.Any(p =>
                p.Name is nameof(CampaignPlacement.ContentItemId)
                    or nameof(CampaignPlacement.AdInventorySlotId)
                    or nameof(CampaignPlacement.BlissMatchId)));
    }

    [Fact]
    public void Wedding_planner_workspace_advertiser_id_is_unique()
    {
        using var db = TestDb.CreateContext();
        var entity = db.Model.FindEntityType(typeof(WeddingPlannerWorkspace));
        Assert.NotNull(entity);

        var uniqueAdvertiser = entity!.GetIndexes().Single(i =>
            i.IsUnique && i.Properties.Count == 1
            && i.Properties[0].Name == nameof(WeddingPlannerWorkspace.AdvertiserId));
        Assert.True(uniqueAdvertiser.IsUnique);
    }

    [Fact]
    public void Female_percentage_is_nullable()
    {
        using var db = TestDb.CreateContext();
        var property = db.Model.FindEntityType(typeof(Creator))!
            .FindProperty(nameof(Creator.FemalePercentage));

        Assert.True(property!.IsNullable);
    }

    [Fact]
    public void There_is_no_direct_creator_to_advertiser_foreign_key()
    {
        using var db = TestDb.CreateContext();
        var creator = db.Model.FindEntityType(typeof(Creator))!;
        var advertiserFks = creator.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Advertiser));
        var content = db.Model.FindEntityType(typeof(ContentItem))!;
        var contentAdvertiserFks = content.GetForeignKeys()
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Advertiser));

        Assert.Empty(advertiserFks);
        Assert.Empty(contentAdvertiserFks);
    }

    [Fact]
    public void Required_foreign_key_indexes_exist()
    {
        using var db = TestDb.CreateContext();

        AssertIndex(db, typeof(CreatorPlatform), nameof(CreatorPlatform.CreatorId));
        AssertIndex(db, typeof(ContentItem), nameof(ContentItem.CreatorId));
        AssertIndex(db, typeof(AdInventorySlot), nameof(AdInventorySlot.ContentItemId));
        AssertIndex(db, typeof(AdvertiserProgram), nameof(AdvertiserProgram.AdvertiserId));
        AssertIndex(db, typeof(AdvertiserOpportunity), nameof(AdvertiserOpportunity.AdvertiserProgramId));
        AssertIndex(db, typeof(NetworkAccess), nameof(NetworkAccess.AdvertiserId));
        AssertIndex(db, typeof(NetworkAccess), nameof(NetworkAccess.AffiliateNetworkId));
        AssertIndex(db, typeof(ProgramAccess), nameof(ProgramAccess.AdvertiserProgramId));
        AssertIndex(db, typeof(BlissMatch), nameof(BlissMatch.CreatorId));
        AssertIndex(db, typeof(BlissMatch), nameof(BlissMatch.AdvertiserOpportunityId));
        AssertIndex(db, typeof(BlissMatch), nameof(BlissMatch.RuleVersionId));
        AssertIndex(db, typeof(MatchScoreComponent), nameof(MatchScoreComponent.BlissMatchId));
        AssertIndex(db, typeof(EligibilityCheck), nameof(EligibilityCheck.BlissMatchId));
        AssertIndex(db, typeof(CampaignPlacement), nameof(CampaignPlacement.CampaignId));
        AssertIndex(db, typeof(CampaignPlacement), nameof(CampaignPlacement.ContentItemId));
        AssertIndex(db, typeof(CampaignPlacement), nameof(CampaignPlacement.AdInventorySlotId));
        AssertIndex(db, typeof(MatchEvaluationRun), nameof(MatchEvaluationRun.BlissMatchId));
        AssertIndex(db, typeof(MatchEvaluationRun), nameof(MatchEvaluationRun.CreatorId));
        AssertIndex(db, typeof(MatchEvaluationRun), nameof(MatchEvaluationRun.RuleVersionId));
        AssertIndex(db, typeof(CreatorIngestionRun), nameof(CreatorIngestionRun.CreatorId));
        AssertIndex(db, typeof(CreatorIngestionRun), nameof(CreatorIngestionRun.CreatorPlatformId));
        AssertIndex(db, typeof(CreatorIngestionRun), nameof(CreatorIngestionRun.IdentityKey));
        AssertIndex(db, typeof(MatchFormationRun), nameof(MatchFormationRun.BlissMatchId));
        AssertIndex(db, typeof(MatchFormationRun), nameof(MatchFormationRun.CreatorId));
        AssertIndex(db, typeof(MatchFormationRun), nameof(MatchFormationRun.AdvertiserOpportunityId));
        AssertIndex(db, typeof(MatchFormationRun), nameof(MatchFormationRun.RuleVersionId));
        AssertIndex(db, typeof(MatchReviewDecision), nameof(MatchReviewDecision.BlissMatchId));
        AssertIndex(db, typeof(MatchReviewDecision), nameof(MatchReviewDecision.CreatorId));
        AssertIndex(db, typeof(Campaign), nameof(Campaign.AdvertiserOpportunityId));
        AssertIndex(db, typeof(CampaignPlacement), nameof(CampaignPlacement.BlissMatchId));
        AssertIndex(db, typeof(CampaignPlacementRun), nameof(CampaignPlacementRun.CampaignPlacementId));
        AssertIndex(db, typeof(CampaignPlacementRun), nameof(CampaignPlacementRun.BlissMatchId));
        AssertIndex(db, typeof(CampaignPlacementRun), nameof(CampaignPlacementRun.CampaignId));
        AssertIndex(db, typeof(CampaignPlacementRun), nameof(CampaignPlacementRun.CreatorId));
        AssertIndex(db, typeof(CampaignPlacementRun), nameof(CampaignPlacementRun.AdvertiserOpportunityId));
        AssertIndex(db, typeof(CampaignPlacementRun), nameof(CampaignPlacementRun.ContentItemId));
        AssertIndex(db, typeof(CampaignPlacementRun), nameof(CampaignPlacementRun.AdInventorySlotId));
        AssertIndex(db, typeof(WeddingPlannerWorkspace), nameof(WeddingPlannerWorkspace.AdvertiserId));
        AssertIndex(db, typeof(WeddingPlannerPlanningSession), nameof(WeddingPlannerPlanningSession.WorkspaceId));
        AssertIndex(db, typeof(WeddingPlannerConversationMessage), nameof(WeddingPlannerConversationMessage.SessionId));
        AssertIndex(db, typeof(WeddingPlannerAuditEvent), nameof(WeddingPlannerAuditEvent.AdvertiserId));
        AssertIndex(db, typeof(RateRecommendation), nameof(RateRecommendation.CreatorId));
        AssertIndex(db, typeof(RateRecommendation), nameof(RateRecommendation.AdInventorySlotId));
        AssertIndex(db, typeof(RateRecommendation), nameof(RateRecommendation.GeographicMarketId));
        AssertIndex(db, typeof(RateRecommendation), nameof(RateRecommendation.PricingModelId));
        AssertIndex(db, typeof(RateRecommendation), nameof(RateRecommendation.PricingRuleVersionId));
        AssertIndex(db, typeof(RateRecommendationFactor), nameof(RateRecommendationFactor.RateRecommendationId));
        AssertIndex(db, typeof(RateRecommendationSource), nameof(RateRecommendationSource.RateRecommendationId));
        AssertIndex(db, typeof(Quote), nameof(Quote.AdvertiserOpportunityId));
        AssertIndex(db, typeof(QuoteVersion), nameof(QuoteVersion.QuoteId));
        AssertIndex(db, typeof(QuoteLineItem), nameof(QuoteLineItem.QuoteVersionId));
        AssertIndex(db, typeof(QuoteLineItem), nameof(QuoteLineItem.RateRecommendationId));
        AssertIndex(db, typeof(QuoteApprovalDecision), nameof(QuoteApprovalDecision.QuoteVersionId));
        AssertIndex(db, typeof(QuoteOutcome), nameof(QuoteOutcome.QuoteVersionId));
    }

    private static void AssertIndex(BlissDbContext db, Type type, string propertyName)
    {
        var entity = db.Model.FindEntityType(type)!;
        var hasIndex = entity.GetIndexes().Any(i => i.Properties.Any(p => p.Name == propertyName));
        Assert.True(hasIndex, $"{type.Name}.{propertyName} should be indexed.");
    }
}
