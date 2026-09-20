using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bliss.Tests.Api;

public sealed class Phase6HumanReviewApiTests : IClassFixture<BlissApiFactory>
{
    private readonly BlissApiFactory _factory;

    public Phase6HumanReviewApiTests(BlissApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Queue_lists_review_required_and_approve_then_replay_are_idempotent()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var queue = await client.GetFromJsonAsync<List<MatchReviewQueueItemDto>>("/api/match-reviews/queue");
        Assert.NotNull(queue);
        Assert.Contains(queue!, item => item.BlissMatchId == Phase2DataSeeder.MatchUnknownReviewId);

        var request = Request($"api-review-{Guid.NewGuid():N}", Phase2DataSeeder.MatchUnknownReviewId, "APPROVE");
        var created = await client.PostAsJsonAsync("/api/match-review-decisions", request);
        var first = await created.Content.ReadFromJsonAsync<MatchReviewResultDto>();
        var replayed = await client.PostAsJsonAsync("/api/match-review-decisions", request);
        var replay = await replayed.Content.ReadFromJsonAsync<MatchReviewResultDto>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayed.StatusCode);
        Assert.False(first!.IsReplay);
        Assert.True(replay!.IsReplay);
        Assert.Equal(first.DecisionId, replay.DecisionId);
        Assert.Equal("APPROVED", first.ResultingMatchStatus);

        var queueAfter = await client.GetFromJsonAsync<List<MatchReviewQueueItemDto>>("/api/match-reviews/queue");
        Assert.DoesNotContain(queueAfter!, item => item.BlissMatchId == Phase2DataSeeder.MatchUnknownReviewId);

        var detail = await client.GetAsync($"/api/match-review-decisions/{first.DecisionId}");
        var byMatch = await client.GetStringAsync($"/api/bliss/matches/{first.BlissMatchId}/review-decisions");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Contains(first.DecisionId.ToString(), byMatch);
        Assert.Contains("\"rationale\"", await detail.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Created_match_cannot_be_approved()
    {
        await SeedAsync();
        var client = _factory.CreateClient();
        var request = Request($"api-invalid-{Guid.NewGuid():N}", Phase1DataSeeder.MatchAId, "APPROVE");

        var response = await client.PostAsJsonAsync("/api/match-review-decisions", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("REVIEW_REQUIRED", await response.Content.ReadAsStringAsync());
    }

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BlissDbContext>();
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new Phase3DataSeeder(db, scope.ServiceProvider.GetRequiredService<MatchRuleEvaluationService>()).SeedAsync();
    }

    private static MatchReviewRequest Request(string key, Guid matchId, string decision) => new(
        SourceSystem: "DashboardFixture",
        IdempotencyKey: key,
        BlissMatchId: matchId,
        ReviewerLabel: "TEST_OPERATOR",
        Decision: decision,
        Rationale: "Controlled API review.");
}
