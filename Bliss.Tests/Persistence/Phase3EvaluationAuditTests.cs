using Bliss.Domain.Common;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class Phase3EvaluationAuditTests
{
    [Fact]
    public async Task Phase3_seed_is_additive_and_records_two_brazil_runs()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var matchA = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId);
        Assert.Equal("CREATED", matchA.Status);
        Assert.Equal(Phase1DataSeeder.RuleVersion1Id, matchA.RuleVersionId);
        Assert.Equal(0, await db.MatchEvaluationRuns.CountAsync(x => x.BlissMatchId == Phase1DataSeeder.MatchAId));

        Assert.Equal(5, await db.MatchEvaluationRuns.CountAsync());
        Assert.Equal(2, await db.MatchEvaluationRuns.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchBrazilApprovedId));
        Assert.Equal(1, await db.MatchEvaluationRuns.CountAsync(x => x.Id == Phase3DataSeeder.RunBrazilFirstId));
        Assert.Equal(1, await db.MatchEvaluationRuns.CountAsync(x => x.Id == Phase3DataSeeder.RunBrazilSecondId));

        var brazil = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId);
        Assert.Equal(EntityStatuses.Approved, brazil.Status);
        Assert.Equal(1.0m, brazil.OverallScore);
    }

    [Fact]
    public async Task Re_evaluation_appends_a_run_and_does_not_delete_prior_runs()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var beforeCount = await db.MatchEvaluationRuns.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchBrazilApprovedId);
        var firstRun = await db.MatchEvaluationRuns.AsNoTracking().SingleAsync(x => x.Id == Phase3DataSeeder.RunBrazilFirstId);
        var firstInput = firstRun.InputSnapshot;
        var firstOutput = firstRun.OutputSnapshot;

        var extraId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee9");
        var evaluated = await new MatchRuleEvaluationService(db).EvaluateAsync(Phase2DataSeeder.MatchBrazilApprovedId, extraId, CancellationToken.None);
        Assert.NotNull(evaluated);

        Assert.Equal(beforeCount + 1, await db.MatchEvaluationRuns.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchBrazilApprovedId));
        Assert.True(await db.MatchEvaluationRuns.AnyAsync(x => x.Id == Phase3DataSeeder.RunBrazilFirstId));
        Assert.True(await db.MatchEvaluationRuns.AnyAsync(x => x.Id == Phase3DataSeeder.RunBrazilSecondId));
        Assert.True(await db.MatchEvaluationRuns.AnyAsync(x => x.Id == extraId));

        var unchangedFirst = await db.MatchEvaluationRuns.AsNoTracking().SingleAsync(x => x.Id == Phase3DataSeeder.RunBrazilFirstId);
        Assert.Equal(firstInput, unchangedFirst.InputSnapshot);
        Assert.Equal(firstOutput, unchangedFirst.OutputSnapshot);
        Assert.Contains("audienceSize", firstInput);
        Assert.Contains("GEOGRAPHY", firstOutput);
    }

    [Fact]
    public async Task Re_evaluating_one_match_does_not_change_another_match_or_its_runs()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var beforeBrazil = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId);
        var beforeBrazilRuns = await db.MatchEvaluationRuns.AsNoTracking()
            .Where(x => x.BlissMatchId == Phase2DataSeeder.MatchBrazilApprovedId)
            .Select(x => x.Id)
            .ToListAsync();

        await new MatchRuleEvaluationService(db).EvaluateAsync(Phase2DataSeeder.MatchGeoFailId);

        var afterBrazil = await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase2DataSeeder.MatchBrazilApprovedId);
        Assert.Equal(beforeBrazil.Status, afterBrazil.Status);
        Assert.Equal(beforeBrazil.OverallScore, afterBrazil.OverallScore);

        var afterBrazilRuns = await db.MatchEvaluationRuns.AsNoTracking()
            .Where(x => x.BlissMatchId == Phase2DataSeeder.MatchBrazilApprovedId)
            .Select(x => x.Id)
            .ToListAsync();
        Assert.Equal(beforeBrazilRuns.OrderBy(x => x), afterBrazilRuns.OrderBy(x => x));
        Assert.Equal(Phase1DataSeeder.RuleVersion1Id, (await db.BlissMatches.AsNoTracking().SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId)).RuleVersionId);
    }

    [Fact]
    public async Task Live_checks_are_replaced_while_historical_runs_remain()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var checkCountBefore = await db.EligibilityChecks.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchGeoFailId);
        var runCountBefore = await db.MatchEvaluationRuns.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchGeoFailId);
        var checkIdsBefore = await db.EligibilityChecks.Where(x => x.BlissMatchId == Phase2DataSeeder.MatchGeoFailId).Select(x => x.Id).ToListAsync();

        await new MatchRuleEvaluationService(db).EvaluateAsync(Phase2DataSeeder.MatchGeoFailId);

        var checkCountAfter = await db.EligibilityChecks.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchGeoFailId);
        Assert.Equal(checkCountBefore, checkCountAfter);
        Assert.Equal(runCountBefore + 1, await db.MatchEvaluationRuns.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchGeoFailId));

        var checkIdsAfter = await db.EligibilityChecks.Where(x => x.BlissMatchId == Phase2DataSeeder.MatchGeoFailId).Select(x => x.Id).ToListAsync();
        Assert.Empty(checkIdsBefore.Intersect(checkIdsAfter));
        Assert.Contains(
            await db.EligibilityChecks.Where(x => x.BlissMatchId == Phase2DataSeeder.MatchGeoFailId).Select(x => x.ReasonCode).ToListAsync(),
            x => x == EligibilityReasonCodes.GeoNotEligible);
    }

    [Fact]
    public async Task Unknown_demographics_are_still_not_zero_after_phase3_seed()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db).SeedAsync();

        var unknown = await db.Creators.AsNoTracking().SingleAsync(x => x.Id == Guid.Parse("11111111-1111-1111-1111-111111111112"));
        Assert.Null(unknown.AudienceSize);

        var run = await db.MatchEvaluationRuns.AsNoTracking().SingleAsync(x => x.Id == Phase3DataSeeder.RunUnknownId);
        Assert.Equal(EntityStatuses.ReviewRequired, run.MatchStatus);
        Assert.DoesNotContain("\"audienceSize\":0", run.InputSnapshot);
        Assert.Contains("UNKNOWN", run.OutputSnapshot);
    }

    [Fact]
    public async Task Second_phase3_seed_is_idempotent()
    {
        await using var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        var seeder = new Phase3DataSeeder(db);
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        Assert.Equal(5, await db.MatchEvaluationRuns.CountAsync());
        Assert.Equal(2, await db.MatchEvaluationRuns.CountAsync(x => x.BlissMatchId == Phase2DataSeeder.MatchBrazilApprovedId));
    }
}
