using Bliss.Domain.Economics;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase5QuoteTests
{
    [Fact]
    public async Task Quote_preserves_explicit_amount_and_idempotent_version_history()
    {
        await using var db = TestDb.CreateContext();
        var recommendationId = await SeedAndRecommendAsync(db, "phase5-quote-rec");
        var service = new QuoteService(db);
        var command = CreateCommand(recommendationId, "phase5-quote-create", 225m);

        var created = await service.CreateAsync(command);
        var replay = await service.CreateAsync(command);

        Assert.False(created.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.Equal(created.Quote.Id, replay.Quote.Id);
        Assert.Equal("DRAFT", created.Quote.Status);
        var version = Assert.Single(created.Quote.Versions);
        var line = Assert.Single(version.LineItems);
        Assert.Equal(225m, version.TotalAmount);
        Assert.Equal(225m, line.UnitAmount);
        Assert.Equal(242m, line.RecommendationTarget);
        Assert.NotEqual(line.RecommendationTarget, line.UnitAmount);
    }

    [Fact]
    public async Task Negotiation_creates_new_draft_that_requires_fresh_approval()
    {
        await using var db = TestDb.CreateContext();
        var recommendationId = await SeedAndRecommendAsync(db, "phase5-negotiate-rec");
        var service = new QuoteService(db);
        var createCommand = CreateCommand(
            recommendationId, "phase5-negotiate-create", 225m);
        var created = await service.CreateAsync(createCommand);
        var quoteId = created.Quote.Id;
        var version1 = created.NewQuoteVersionId!.Value;

        await service.DecideApprovalAsync(new(
            quoteId, version1, QuoteApprovalDecisions.Approved, "TEST_REVIEWER",
            "Version 1 approved", "TEST", "phase5-approve-v1"));
        var negotiated = await service.RecordOutcomeAsync(new(
            quoteId, version1, QuoteOutcomeResponses.Negotiated, "TEST_ADVERTISER",
            "Advertiser proposed 215 PHP",
            [Line(recommendationId, 215m)],
            "TEST", "phase5-negotiate"));

        Assert.Equal(QuoteStatuses.Draft, negotiated.Quote.Status);
        Assert.Equal(2, negotiated.Quote.CurrentVersionNumber);
        Assert.Equal(2, negotiated.Quote.Versions.Count);
        Assert.Single(negotiated.Quote.ApprovalDecisions);
        Assert.Single(negotiated.Quote.Outcomes);
        Assert.Equal(215m, negotiated.Quote.Versions
            .Single(x => x.VersionNumber == 2).TotalAmount);

        var version2 = negotiated.NewQuoteVersionId!.Value;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RecordOutcomeAsync(new(
                quoteId, version2, QuoteOutcomeResponses.Accepted, "TEST_ADVERTISER",
                "Attempt before approval", null, "TEST", "phase5-early-accept")));
        Assert.Contains("APPROVED", error.Message);

        await service.DecideApprovalAsync(new(
            quoteId, version2, QuoteApprovalDecisions.Approved, "TEST_REVIEWER",
            "Negotiated version approved", "TEST", "phase5-approve-v2"));
        var accepted = await service.RecordOutcomeAsync(new(
            quoteId, version2, QuoteOutcomeResponses.Accepted, "TEST_ADVERTISER",
            "Advertiser accepted version 2", null, "TEST", "phase5-accept-v2"));

        Assert.Equal(QuoteStatuses.Accepted, accepted.Quote.Status);
        Assert.Equal(2, accepted.Quote.ApprovalDecisions.Count);
        Assert.Equal(2, accepted.Quote.Outcomes.Count);
        Assert.Equal(215m, accepted.Quote.Outcomes
            .Single(x => x.Response == QuoteOutcomeResponses.Accepted).Amount);

        var createReplay = await service.CreateAsync(createCommand);
        Assert.True(createReplay.IsReplay);
        Assert.Equal(version1, createReplay.NewQuoteVersionId);
    }

    [Fact]
    public async Task Quote_line_keeps_recommendation_snapshot_if_source_row_changes()
    {
        await using var db = TestDb.CreateContext();
        var recommendationId = await SeedAndRecommendAsync(db, "phase5-history-rec");
        var service = new QuoteService(db);
        var created = await service.CreateAsync(
            CreateCommand(recommendationId, "phase5-history-create", 225m));

        var recommendation = await db.RateRecommendations.FindAsync(recommendationId);
        recommendation!.RangeTarget = 9_999m;
        await db.SaveChangesAsync();

        var quote = await service.QuoteGraph()
            .SingleAsync(x => x.Id == created.Quote.Id);
        Assert.Equal(242m, quote.Versions.Single().LineItems.Single().RecommendationTarget);
        Assert.Equal(225m, quote.Versions.Single().TotalAmount);
    }

    private static CreateQuoteCommand CreateCommand(
        Guid recommendationId, string key, decimal amount) =>
        new(
            Phase1DataSeeder.OpportunityAId,
            "TEST_OPERATOR",
            "Initial TEST offer",
            [Line(recommendationId, amount)],
            "TEST",
            key);

    private static QuoteLineCommand Line(Guid recommendationId, decimal amount) =>
        new(recommendationId, "TEST mid-roll inventory", 1m, amount);

    private static async Task<Guid> SeedAndRecommendAsync(
        BlissDbContext db, string key)
    {
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();
        await new EconomicsPhase4DataSeeder(db).SeedAsync();
        var recommendation = await new RateRecommendationService(db).GenerateAsync(new(
            Phase1DataSeeder.CreatorId,
            Guid.Parse("99999999-9999-9999-9999-999999999992"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            60,
            Phase1DataSeeder.OpportunityAId,
            Phase1DataSeeder.MatchAId,
            "WOMENS_FOOTWEAR",
            "Reach women 18-34 in Manila",
            "TEST",
            key));
        return recommendation.Recommendation.Id;
    }
}
