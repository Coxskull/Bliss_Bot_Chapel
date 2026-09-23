using Bliss.Domain.Common;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase8HandshakeTests
{
    [Fact]
    public async Task Planner_handshake_calls_economics_and_replays_same_result()
    {
        await using var db = TestDb.CreateContext();
        await SeedEconomicsAsync(db);
        var sessionId = await OpenSessionAsync(db, Phase1DataSeeder.AdvertiserId, "owned");
        var match = await db.BlissMatches.SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId);
        match.Status = EntityStatuses.Approved;
        await db.SaveChangesAsync();
        var service = Service(db);
        var command = Command("phase8-handshake");

        var created = await service.RequestAsync(
            sessionId, command, true, null, "OPERATOR", "TEST_OPERATOR", "request-1");
        var replay = await service.RequestAsync(
            sessionId, command, true, null, "OPERATOR", "TEST_OPERATOR", "request-2");

        Assert.False(created.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.Equal(created.Request.Id, replay.Request.Id);
        Assert.Equal(
            created.Request.RateRecommendationId,
            replay.Request.RateRecommendationId);
        Assert.Equal(
            WeddingPlannerStatuses.RecommendationReady,
            created.Request.Status);
        Assert.Equal(198m, created.Request.RateRecommendation.RangeLow);
        Assert.Equal(242m, created.Request.RateRecommendation.RangeTarget);
        Assert.Equal(286m, created.Request.RateRecommendation.RangeHigh);
        Assert.Equal("CPM", created.Request.RateRecommendation.PricingModel.Code);
        Assert.Equal("MEDIUM", created.Request.RateRecommendation.ConfidenceLevel);
        Assert.Equal(5, created.Request.RateRecommendation.Factors.Count);
        Assert.Single(created.Request.RateRecommendation.Sources);
        Assert.Single(await db.WeddingPlannerEconomicsRequests.ToListAsync());
        Assert.Single(await db.RateRecommendations.Where(
            x => x.SourceSystem == "WEDDING_PLANNER_TEST"
                && x.IdempotencyKey == "phase8-handshake").ToListAsync());
        Assert.Contains(await db.WeddingPlannerAuditEvents.ToListAsync(),
            x => x.Action
                == WeddingPlannerAuditActions.EconomicsRecommendationRequested);
    }

    [Fact]
    public async Task Planner_handshake_requires_approved_owned_compatible_available_inventory()
    {
        await using var db = TestDb.CreateContext();
        await SeedEconomicsAsync(db);
        await new WeddingPlannerDataSeeder(db).SeedAsync();
        var ownedSession = await OpenSessionAsync(
            db, Phase1DataSeeder.AdvertiserId, "owned-validation");
        var otherSession = await OpenSessionAsync(
            db, WeddingPlannerDataSeeder.DentalManilaId, "other-validation");
        var service = Service(db);

        var unapproved = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestAsync(
                ownedSession,
                Command("unapproved"),
                true,
                null,
                "OPERATOR",
                "TEST_OPERATOR",
                null));
        Assert.Contains("APPROVED", unapproved.Message);

        var match = await db.BlissMatches.SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId);
        match.Status = EntityStatuses.Approved;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<WeddingPlannerNotFoundException>(() =>
            service.RequestAsync(
                otherSession,
                Command("cross-advertiser"),
                true,
                null,
                "OPERATOR",
                "TEST_OPERATOR",
                null));

        var slot = await db.AdInventorySlots.SingleAsync(
            x => x.Id == Guid.Parse("99999999-9999-9999-9999-999999999992"));
        slot.IsAvailable = false;
        await db.SaveChangesAsync();
        var unavailable = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestAsync(
                ownedSession,
                Command("unavailable"),
                true,
                null,
                "OPERATOR",
                "TEST_OPERATOR",
                null));
        Assert.Contains("unavailable", unavailable.Message);
    }

    [Fact]
    public async Task Planner_handshake_creates_no_quote_placement_or_compensation()
    {
        await using var db = TestDb.CreateContext();
        await SeedEconomicsAsync(db);
        var sessionId = await OpenSessionAsync(
            db, Phase1DataSeeder.AdvertiserId, "boundary");
        var match = await db.BlissMatches.SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId);
        match.Status = EntityStatuses.Approved;
        await db.SaveChangesAsync();

        await Service(db).RequestAsync(
            sessionId,
            Command("boundary"),
            true,
            null,
            "OPERATOR",
            "TEST_OPERATOR",
            null);

        Assert.Empty(await db.Quotes.ToListAsync());
        Assert.Empty(await db.CompensationIllustrations.ToListAsync());
        Assert.Empty(await db.CampaignPlacementRuns.ToListAsync());
    }

    private static WeddingPlannerEconomicsHandshakeService Service(BlissDbContext db) =>
        new(db, new RateRecommendationService(db));

    private static RequestWeddingPlannerRecommendationCommand Command(string key) =>
        new(
            Phase1DataSeeder.MatchAId,
            Guid.Parse("99999999-9999-9999-9999-999999999992"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            60,
            "WELLNESS",
            "Reach women 18-34 in Manila",
            "TEST_OPERATOR",
            "WEDDING_PLANNER_TEST",
            key);

    private static async Task<Guid> OpenSessionAsync(
        BlissDbContext db,
        Guid advertiserId,
        string suffix)
    {
        var planner = new WeddingPlannerService(db);
        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            advertiserId,
            "WEDDING_PLANNER_TEST",
            $"workspace-{suffix}",
            true,
            null,
            "OPERATOR",
            "TEST_OPERATOR",
            null);
        var session = await planner.CreateSessionAsync(
            workspace.WorkspaceId,
            "WEDDING_PLANNER_TEST",
            $"session-{suffix}",
            true,
            null,
            "OPERATOR",
            "TEST_OPERATOR",
            null);
        return session.SessionId;
    }

    private static async Task SeedEconomicsAsync(BlissDbContext db)
    {
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();
        await new EconomicsPhase4DataSeeder(db).SeedAsync();
    }
}
