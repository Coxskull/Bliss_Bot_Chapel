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
    }

    private static void AssertIndex(BlissDbContext db, Type type, string propertyName)
    {
        var entity = db.Model.FindEntityType(type)!;
        var hasIndex = entity.GetIndexes().Any(i => i.Properties.Any(p => p.Name == propertyName));
        Assert.True(hasIndex, $"{type.Name}.{propertyName} should be indexed.");
    }
}
