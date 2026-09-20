using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class Phase7CampaignPlacementTests
{
    private static readonly Guid BrazilSlotId =
        Guid.Parse("99999999-9999-9999-9999-999999999997");

    [Fact]
    public async Task Approved_match_binds_to_creator_inventory_without_delivery_mutation()
    {
        await using var db = await SeedAsync();
        var service = new CampaignPlacementService(db);
        var placementsBefore = await db.CampaignPlacements.CountAsync();

        var result = await service.BindAsync(Command("binding-001"));

        Assert.False(result.IsReplay);
        Assert.Equal("PLANNED", result.Outcome);
        Assert.Equal(placementsBefore + 1, await db.CampaignPlacements.CountAsync());
        Assert.Equal(1, await db.CampaignPlacementRuns.CountAsync());

        var placement = await db.CampaignPlacements.SingleAsync(x => x.Id == result.CampaignPlacementId);
        Assert.Equal(Phase2DataSeeder.MatchBrazilApprovedId, placement.BlissMatchId);
        Assert.Equal("PLANNED", placement.Status);
        Assert.Null(placement.StartAt);
        Assert.Null(placement.EndAt);
        Assert.True((await db.AdInventorySlots.SingleAsync(x => x.Id == BrazilSlotId)).IsAvailable);
        Assert.Equal(
            Phase2DataSeeder.OpportunityBrId,
            (await db.Campaigns.SingleAsync(x => x.Id == Phase2DataSeeder.Campaign2Id)).AdvertiserOpportunityId);
        Assert.Equal(
            "APPROVED",
            (await db.BlissMatches.SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId)).Status);
    }

    [Fact]
    public async Task Replay_returns_original_and_does_not_duplicate()
    {
        await using var db = await SeedAsync();
        var service = new CampaignPlacementService(db);
        var command = Command("binding-002");
        var first = await service.BindAsync(command);
        var placements = await db.CampaignPlacements.CountAsync();

        var replay = await service.BindAsync(command);

        Assert.True(replay.IsReplay);
        Assert.Equal(first.RunId, replay.RunId);
        Assert.Equal(first.CampaignPlacementId, replay.CampaignPlacementId);
        Assert.Equal(placements, await db.CampaignPlacements.CountAsync());
        Assert.Equal(1, await db.CampaignPlacementRuns.CountAsync());
    }

    [Fact]
    public async Task Different_keys_allow_multiple_placements_for_same_match_and_content()
    {
        await using var db = await SeedAsync();
        var service = new CampaignPlacementService(db);

        var first = await service.BindAsync(Command("binding-003"));
        var second = await service.BindAsync(Command("binding-004"));

        Assert.NotEqual(first.CampaignPlacementId, second.CampaignPlacementId);
        Assert.Equal(2, await db.CampaignPlacementRuns.CountAsync());
    }

    [Fact]
    public async Task Nonapproved_match_and_graph_mismatches_persist_nothing()
    {
        await using var db = await SeedAsync();
        var service = new CampaignPlacementService(db);
        var placementsBefore = await db.CampaignPlacements.CountAsync();
        var otherSlot = await db.AdInventorySlots
            .AsNoTracking()
            .FirstAsync(x => x.ContentItemId == Phase1DataSeeder.ContentItem1Id);

        var notApproved = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BindAsync(Command("binding-005") with
            {
                BlissMatchId = Phase2DataSeeder.MatchUnknownReviewId
            }));
        var wrongCreator = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BindAsync(Command("binding-006") with
            {
                ContentItemId = Phase1DataSeeder.ContentItem1Id,
                AdInventorySlotId = otherSlot.Id
            }));
        var wrongSlot = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.BindAsync(Command("binding-007") with
            {
                AdInventorySlotId = otherSlot.Id
            }));

        Assert.Contains("APPROVED", notApproved.Message);
        Assert.Contains("match creator", wrongCreator.Message);
        Assert.Contains("supplied content", wrongSlot.Message);
        Assert.Equal(placementsBefore, await db.CampaignPlacements.CountAsync());
        Assert.Equal(0, await db.CampaignPlacementRuns.CountAsync());
        Assert.Null((await db.Campaigns.SingleAsync(x => x.Id == Phase2DataSeeder.Campaign2Id)).AdvertiserOpportunityId);
    }

    private static CampaignPlacementCommand Command(string key) => new(
        SourceSystem: "ControlledFixture",
        IdempotencyKey: key,
        OperatorLabel: "TEST_OPERATOR",
        BlissMatchId: Phase2DataSeeder.MatchBrazilApprovedId,
        CampaignId: Phase2DataSeeder.Campaign2Id,
        ContentItemId: Phase2DataSeeder.BrazilContentId,
        AdInventorySlotId: BrazilSlotId);

    private static async Task<BlissDbContext> SeedAsync()
    {
        var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        return db;
    }
}
