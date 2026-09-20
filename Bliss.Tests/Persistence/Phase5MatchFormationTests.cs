using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class Phase5MatchFormationTests
{
    [Fact]
    public async Task Controlled_formation_creates_and_evaluates_audited_match()
    {
        await using var db = await SeedAsync();
        var service = Service(db);
        var evaluationsBefore = await db.MatchEvaluationRuns.CountAsync();

        var result = await service.FormAsync(Command("formation-001"));

        Assert.False(result.IsReplay);
        Assert.Equal("CREATED", result.Outcome);
        Assert.NotEqual("CREATED", result.MatchStatus);
        Assert.Equal(1, await db.MatchFormationRuns.CountAsync());
        Assert.Equal(evaluationsBefore + 1, await db.MatchEvaluationRuns.CountAsync());
        Assert.True(await db.EligibilityChecks.AnyAsync(x => x.BlissMatchId == result.BlissMatchId));
        Assert.True(await db.MatchScoreComponents.AnyAsync(x => x.BlissMatchId == result.BlissMatchId));

        var run = await db.MatchFormationRuns.SingleAsync();
        Assert.Contains("\"evaluateOnCreate\":true", run.InputSnapshot);
        Assert.Equal(result.BlissMatchId, run.BlissMatchId);
    }

    [Fact]
    public async Task Replay_returns_original_and_writes_nothing()
    {
        await using var db = await SeedAsync();
        var service = Service(db);
        var command = Command("formation-002");
        var first = await service.FormAsync(command);
        var matches = await db.BlissMatches.CountAsync();
        var evaluations = await db.MatchEvaluationRuns.CountAsync();
        var checks = await db.EligibilityChecks.CountAsync();

        var replay = await service.FormAsync(command);

        Assert.True(replay.IsReplay);
        Assert.Equal(first.RunId, replay.RunId);
        Assert.Equal(first.BlissMatchId, replay.BlissMatchId);
        Assert.Equal(matches, await db.BlissMatches.CountAsync());
        Assert.Equal(evaluations, await db.MatchEvaluationRuns.CountAsync());
        Assert.Equal(checks, await db.EligibilityChecks.CountAsync());
        Assert.Equal(1, await db.MatchFormationRuns.CountAsync());
    }

    [Fact]
    public async Task Different_keys_allow_many_matches_for_one_creator()
    {
        await using var db = await SeedAsync();
        var service = Service(db);

        var first = await service.FormAsync(Command("formation-003") with { EvaluateOnCreate = false });
        var second = await service.FormAsync(Command("formation-004") with { EvaluateOnCreate = false });

        Assert.NotEqual(first.BlissMatchId, second.BlissMatchId);
        Assert.Equal("CREATED", first.MatchStatus);
        Assert.Equal(2, await db.MatchFormationRuns.CountAsync());
        Assert.Equal(2, await db.BlissMatches.CountAsync(x =>
            x.Id == first.BlissMatchId || x.Id == second.BlissMatchId));
    }

    [Fact]
    public async Task Invalid_reference_and_documentless_evaluation_persist_nothing()
    {
        await using var db = await SeedAsync();
        var service = Service(db);
        var matchesBefore = await db.BlissMatches.CountAsync();

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FormAsync(Command("formation-005") with { CreatorId = Guid.NewGuid() }));
        var noDocument = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.FormAsync(Command("formation-006") with { RuleVersionId = Phase1DataSeeder.RuleVersion1Id }));

        Assert.Contains("existing creator", missing.Message);
        Assert.Contains("rule document", noDocument.Message);
        Assert.Equal(matchesBefore, await db.BlissMatches.CountAsync());
        Assert.Equal(0, await db.MatchFormationRuns.CountAsync());
    }

    private static MatchFormationService Service(BlissDbContext db) =>
        new(db, new MatchRuleEvaluationService(db));

    private static MatchFormationCommand Command(string key) => new(
        SourceSystem: "ControlledFixture",
        IdempotencyKey: key,
        CreatorId: Phase1DataSeeder.CreatorId,
        AdvertiserOpportunityId: Phase2DataSeeder.OpportunityPhId,
        RuleVersionId: Phase2DataSeeder.RuleVersion2Id,
        EvaluateOnCreate: true);

    private static async Task<BlissDbContext> SeedAsync()
    {
        var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        return db;
    }
}
