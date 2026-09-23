using Bliss.Domain.Economics;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests.Persistence;

public sealed class EconomicsPhase6CompensationTests
{
    [Fact]
    public async Task Illustration_is_replayable_and_allocations_equal_accepted_amount()
    {
        await using var db = TestDb.CreateContext();
        var accepted = await SeedAcceptedQuoteAsync(db, "phase6-main");
        var service = new CompensationIllustrationService(db);
        var command = new GenerateCompensationIllustrationCommand(
            accepted.QuoteId,
            accepted.VersionId,
            EconomicsPhase6DataSeeder.RuleVersionId,
            "TEST",
            "phase6-illustration");

        var created = await service.GenerateAsync(command);
        var replay = await service.GenerateAsync(command);

        Assert.False(created.IsReplay);
        Assert.True(replay.IsReplay);
        Assert.Equal(created.Illustration.Id, replay.Illustration.Id);
        Assert.Equal(215m, created.Illustration.GrossAmount);
        Assert.Equal(215m, created.Illustration.Lines.Sum(x => x.Amount));
        Assert.Collection(
            created.Illustration.Lines.OrderBy(x => x.SortOrder),
            alpha =>
            {
                Assert.Equal(CompensationParticipantRoles.Alpha, alpha.ParticipantRole);
                Assert.Equal(18m, alpha.Percentage);
                Assert.Equal(38.70m, alpha.Amount);
            },
            creator =>
            {
                Assert.Equal(CompensationParticipantRoles.Creator, creator.ParticipantRole);
                Assert.Equal(72m, creator.Percentage);
                Assert.Equal(154.80m, creator.Amount);
            },
            other =>
            {
                Assert.Equal(CompensationParticipantRoles.OtherAuthorized, other.ParticipantRole);
                Assert.Equal(10m, other.Percentage);
                Assert.Equal(21.50m, other.Amount);
            });
    }

    [Fact]
    public async Task Unaccepted_quote_and_invalid_policy_are_rejected()
    {
        await using var db = TestDb.CreateContext();
        var accepted = await SeedAcceptedQuoteAsync(db, "phase6-validation");
        var service = new CompensationIllustrationService(db);

        var quote = await db.Quotes.SingleAsync(x => x.Id == accepted.QuoteId);
        quote.Status = QuoteStatuses.Draft;
        await db.SaveChangesAsync();
        var quoteError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(new(
                accepted.QuoteId, accepted.VersionId,
                EconomicsPhase6DataSeeder.RuleVersionId,
                "TEST", "phase6-unaccepted")));
        Assert.Contains("ACCEPTED", quoteError.Message);

        quote.Status = QuoteStatuses.Accepted;
        var allocation = await db.CompensationRuleAllocations
            .OrderBy(x => x.SortOrder).LastAsync();
        allocation.Percentage = 9m;
        await db.SaveChangesAsync();
        var policyError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(new(
                accepted.QuoteId, accepted.VersionId,
                EconomicsPhase6DataSeeder.RuleVersionId,
                "TEST", "phase6-invalid-policy")));
        Assert.Contains("100%", policyError.Message);
    }

    [Fact]
    public async Task Historical_illustration_does_not_change_with_policy_rows()
    {
        await using var db = TestDb.CreateContext();
        var accepted = await SeedAcceptedQuoteAsync(db, "phase6-history");
        var service = new CompensationIllustrationService(db);
        var generated = await service.GenerateAsync(new(
            accepted.QuoteId, accepted.VersionId,
            EconomicsPhase6DataSeeder.RuleVersionId,
            "TEST", "phase6-history-illustration"));

        var policyLine = await db.CompensationRuleAllocations
            .OrderBy(x => x.SortOrder).FirstAsync();
        policyLine.Percentage = 1m;
        await db.SaveChangesAsync();

        var historical = await db.CompensationIllustrations
            .Include(x => x.Lines)
            .SingleAsync(x => x.Id == generated.Illustration.Id);
        Assert.Equal(18m, historical.Lines.OrderBy(x => x.SortOrder).First().Percentage);
        Assert.Equal(38.70m, historical.Lines.OrderBy(x => x.SortOrder).First().Amount);
        Assert.Equal(215m, historical.Lines.Sum(x => x.Amount));
    }

    private static async Task<AcceptedQuote> SeedAcceptedQuoteAsync(
        BlissDbContext db,
        string keyPrefix)
    {
        await new Phase1DataSeeder(db).SeedAsync();
        await new Phase2DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync();
        await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync();
        await new EconomicsPhase4DataSeeder(db).SeedAsync();
        await new EconomicsPhase6DataSeeder(db).SeedAsync();

        var recommendation = await new RateRecommendationService(db).GenerateAsync(new(
            Phase1DataSeeder.CreatorId,
            Guid.Parse("99999999-9999-9999-9999-999999999992"),
            EconomicsDataSeeder.ManilaId,
            "CPM",
            60,
            Phase1DataSeeder.OpportunityAId,
            Phase1DataSeeder.MatchAId,
            "WOMENS_FOOTWEAR",
            "Compensation test",
            "TEST",
            $"{keyPrefix}-recommendation"));
        var quoteService = new QuoteService(db);
        var quote = await quoteService.CreateAsync(new(
            Phase1DataSeeder.OpportunityAId,
            "TEST_OPERATOR",
            "Compensation TEST quote",
            [new(
                recommendation.Recommendation.Id,
                "TEST inventory",
                1m,
                215m)],
            "TEST",
            $"{keyPrefix}-quote"));
        var versionId = quote.NewQuoteVersionId!.Value;
        await quoteService.DecideApprovalAsync(new(
            quote.Quote.Id,
            versionId,
            QuoteApprovalDecisions.Approved,
            "TEST_REVIEWER",
            "Approved for illustration test",
            "TEST",
            $"{keyPrefix}-approval"));
        await quoteService.RecordOutcomeAsync(new(
            quote.Quote.Id,
            versionId,
            QuoteOutcomeResponses.Accepted,
            "TEST_ADVERTISER",
            "Accepted for illustration test",
            null,
            "TEST",
            $"{keyPrefix}-outcome"));
        return new(quote.Quote.Id, versionId);
    }

    private sealed record AcceptedQuote(Guid QuoteId, Guid VersionId);
}
