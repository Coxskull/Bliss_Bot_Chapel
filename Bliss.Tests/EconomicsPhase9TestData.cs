using Bliss.Domain.Common;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests;

internal sealed record EconomicsPhase9Fixture(
    Guid CampaignId,
    Guid CampaignPlacementId,
    Guid QuoteOutcomeId,
    Guid QuoteLineItemId,
    Guid CompensationIllustrationId,
    Guid RateRecommendationId);

internal static class EconomicsPhase9TestData
{
    public static async Task<EconomicsPhase9Fixture> SeedAsync(
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

        var match = await db.BlissMatches.SingleAsync(
            x => x.Id == Phase1DataSeeder.MatchAId);
        match.Status = EntityStatuses.Approved;

        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            AdvertiserOpportunityId = Phase1DataSeeder.OpportunityAId,
            Name = $"TEST historical campaign {keyPrefix}",
            Status = EntityStatuses.Draft,
            CreatedAt = DateTime.UtcNow
        };
        var placement = new CampaignPlacement
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            ContentItemId = Phase1DataSeeder.ContentItem1Id,
            AdInventorySlotId =
                Guid.Parse("99999999-9999-9999-9999-999999999992"),
            BlissMatchId = Phase1DataSeeder.MatchAId,
            Status = "PLANNED"
        };
        db.Campaigns.Add(campaign);
        db.CampaignPlacements.Add(placement);
        await db.SaveChangesAsync();

        var recommendation = await new RateRecommendationService(db).GenerateAsync(new(
            Phase1DataSeeder.CreatorId,
            placement.AdInventorySlotId,
            EconomicsDataSeeder.ManilaId,
            "CPM",
            60,
            Phase1DataSeeder.OpportunityAId,
            Phase1DataSeeder.MatchAId,
            "WOMENS_FOOTWEAR",
            "Historical learning test",
            "PHASE9_TEST",
            $"{keyPrefix}-recommendation"));
        var quoteService = new QuoteService(db);
        var quote = await quoteService.CreateAsync(new(
            Phase1DataSeeder.OpportunityAId,
            "TEST_OPERATOR",
            "Historical learning TEST quote",
            [new(
                recommendation.Recommendation.Id,
                "TEST mid-roll inventory",
                1m,
                215m)],
            "PHASE9_TEST",
            $"{keyPrefix}-quote"));
        var versionId = quote.NewQuoteVersionId!.Value;
        await quoteService.DecideApprovalAsync(new(
            quote.Quote.Id,
            versionId,
            QuoteApprovalDecisions.Approved,
            "TEST_REVIEWER",
            "Approved for historical learning test",
            "PHASE9_TEST",
            $"{keyPrefix}-approval"));
        var outcomeResult = await quoteService.RecordOutcomeAsync(new(
            quote.Quote.Id,
            versionId,
            QuoteOutcomeResponses.Accepted,
            "TEST_ADVERTISER",
            "Accepted for historical learning test",
            null,
            "PHASE9_TEST",
            $"{keyPrefix}-outcome"));
        var outcomeId = outcomeResult.ActionId;
        var illustration = await new CompensationIllustrationService(db).GenerateAsync(new(
            quote.Quote.Id,
            versionId,
            EconomicsPhase6DataSeeder.RuleVersionId,
            "PHASE9_TEST",
            $"{keyPrefix}-illustration"));
        var lineId = await db.QuoteLineItems
            .Where(x => x.QuoteVersionId == versionId)
            .Select(x => x.Id)
            .SingleAsync();

        return new(
            campaign.Id,
            placement.Id,
            outcomeId,
            lineId,
            illustration.Illustration.Id,
            recommendation.Recommendation.Id);
    }
}
