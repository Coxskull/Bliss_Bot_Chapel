using Bliss.Domain.Common;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class Phase2SeedAndEvaluationTests
{
    [Fact]
    public async Task Phase2_seed_is_additive_and_does_not_overwrite_phase1_matches()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        var matchAOpportunity = await db.BlissMatches
            .AsNoTracking()
            .Where(x => x.Id == Phase1DataSeeder.MatchAId)
            .Select(x => x.AdvertiserOpportunityId)
            .SingleAsync();

        await new Phase2DataSeeder(db).SeedAsync();

        var persistedA = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId);
        Assert.Equal(matchAOpportunity, persistedA.AdvertiserOpportunityId);
        Assert.Equal(Phase1DataSeeder.RuleVersion1Id, persistedA.RuleVersionId);
        Assert.Equal("CREATED", persistedA.Status);

        Assert.Equal(2, await db.Advertisers.CountAsync());
        Assert.True(await db.BlissMatches.CountAsync(x => x.CreatorId == Phase1DataSeeder.CreatorId) >= 5);
        Assert.Equal(4, await db.AdInventorySlots.CountAsync(x => x.ContentItemId == Phase2DataSeeder.BrazilContentId));
        Assert.Contains(
            await db.AdInventorySlots.Where(x => x.ContentItemId == Phase2DataSeeder.BrazilContentId).Select(x => x.SlotType).ToListAsync(),
            x => x == InventorySlotTypes.PerimeterOverlay);
    }

    [Fact]
    public async Task Phase2_brazil_match_is_approved_and_ph_to_br_is_ineligible()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();

        var brazil = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId);
        Assert.Equal(EntityStatuses.Approved, brazil.Status);
        Assert.Equal(1.0m, brazil.OverallScore);

        var geoFail = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchGeoFailId);
        Assert.Equal(EntityStatuses.Ineligible, geoFail.Status);
        Assert.Contains(
            await db.EligibilityChecks.Where(x => x.BlissMatchId == geoFail.Id).Select(x => x.ReasonCode).ToListAsync(),
            x => x == EligibilityReasonCodes.GeoNotEligible);
    }

    [Fact]
    public async Task Re_evaluating_one_match_does_not_change_another_match()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();

        var beforeB = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId);
        var service = new MatchRuleEvaluationService(db);
        var evaluated = await service.EvaluateAsync(Phase2DataSeeder.MatchGeoFailId);
        Assert.NotNull(evaluated);

        var afterB = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId);
        Assert.Equal(beforeB.Status, afterB.Status);
        Assert.Equal(beforeB.OverallScore, afterB.OverallScore);
        Assert.Equal(Phase2DataSeeder.RuleVersion2Id, evaluated!.RuleVersionId);
        Assert.Equal(Phase1DataSeeder.RuleVersion1Id, (await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId)).RuleVersionId);
    }

    [Fact]
    public async Task Unknown_demographics_creator_review_does_not_use_zero_audience()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();

        var unknown = await db.Creators.AsNoTracking().SingleAsync(x => x.Id == Guid.Parse("11111111-1111-1111-1111-111111111112"));
        Assert.Null(unknown.AudienceSize);
        Assert.Null(unknown.FemalePercentage);

        var match = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchUnknownReviewId);
        Assert.Equal(EntityStatuses.ReviewRequired, match.Status);
        Assert.Contains(
            await db.EligibilityChecks.Where(x => x.BlissMatchId == match.Id).Select(x => x.ReasonCode).ToListAsync(),
            x => x == EligibilityReasonCodes.MinimumAudienceUnknown || x == EligibilityReasonCodes.GeoUnknown || x == EligibilityReasonCodes.LanguageUnknown);
    }

    [Fact]
    public async Task Second_seed_is_idempotent()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        var seeder = new Phase2DataSeeder(db);
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        Assert.Equal(1, await db.Advertisers.CountAsync(x => x.Id == Phase2DataSeeder.Advertiser2Id));
        Assert.Equal(1, await db.BlissMatches.CountAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId));
    }
}
