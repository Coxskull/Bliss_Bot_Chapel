using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class Phase6HumanReviewTests
{
    [Fact]
    public async Task Approve_moves_review_required_match_and_writes_immutable_decision()
    {
        await using var db = await SeedAsync();
        var service = new MatchReviewService(db);
        var before = await db.MatchReviewDecisions.CountAsync();

        var result = await service.DecideAsync(Command("review-001", Phase2DataSeeder.MatchUnknownReviewId, MatchReviewService.Approve));

        Assert.False(result.IsReplay);
        Assert.Equal(MatchReviewService.Approve, result.Decision);
        Assert.Equal("APPROVED", result.ResultingMatchStatus);
        Assert.Equal(before + 1, await db.MatchReviewDecisions.CountAsync());
        Assert.Equal("APPROVED", (await db.BlissMatches.SingleAsync(x => x.Id == Phase2DataSeeder.MatchUnknownReviewId)).Status);
        Assert.NotNull(result.MatchEvaluationRunId);
        Assert.Contains("\"decision\":\"APPROVE\"", (await db.MatchReviewDecisions.SingleAsync()).InputSnapshot);
    }

    [Fact]
    public async Task Replay_returns_original_and_writes_nothing()
    {
        await using var db = await SeedAsync();
        var service = new MatchReviewService(db);
        var command = Command("review-002", Phase2DataSeeder.MatchUnknownReviewId, MatchReviewService.Reject);
        var first = await service.DecideAsync(command);
        var decisions = await db.MatchReviewDecisions.CountAsync();

        var replay = await service.DecideAsync(command);

        Assert.True(replay.IsReplay);
        Assert.Equal(first.DecisionId, replay.DecisionId);
        Assert.Equal("INELIGIBLE", replay.ResultingMatchStatus);
        Assert.Equal(decisions, await db.MatchReviewDecisions.CountAsync());
        Assert.Equal("INELIGIBLE", (await db.BlissMatches.SingleAsync(x => x.Id == Phase2DataSeeder.MatchUnknownReviewId)).Status);
    }

    [Fact]
    public async Task Created_match_without_review_status_is_rejected_and_persists_nothing()
    {
        await using var db = await SeedAsync();
        var service = new MatchReviewService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecideAsync(Command("review-003", Phase1DataSeeder.MatchAId, MatchReviewService.Approve)));

        Assert.Contains("REVIEW_REQUIRED", error.Message);
        Assert.Equal(0, await db.MatchReviewDecisions.CountAsync());
        Assert.Equal("CREATED", (await db.BlissMatches.SingleAsync(x => x.Id == Phase1DataSeeder.MatchAId)).Status);
    }

    [Fact]
    public async Task Unknown_decision_persists_nothing()
    {
        await using var db = await SeedAsync();
        var service = new MatchReviewService(db);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecideAsync(Command("review-004", Phase2DataSeeder.MatchUnknownReviewId, "AUTO_APPROVE")));

        Assert.Contains("APPROVE, REJECT, or HOLD", error.Message);
        Assert.Equal(0, await db.MatchReviewDecisions.CountAsync());
        Assert.Equal("REVIEW_REQUIRED", (await db.BlissMatches.SingleAsync(x => x.Id == Phase2DataSeeder.MatchUnknownReviewId)).Status);
    }

    private static MatchReviewCommand Command(string key, Guid matchId, string decision) => new(
        SourceSystem: "ControlledFixture",
        IdempotencyKey: key,
        BlissMatchId: matchId,
        ReviewerLabel: "TEST_OPERATOR",
        Decision: decision,
        Rationale: "Controlled Phase 6 review decision.");

    private static async Task<BlissDbContext> SeedAsync()
    {
        var db = TestDb.CreateContext();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db, new MatchRuleEvaluationService(db)).SeedAsync();
        return db;
    }
}
