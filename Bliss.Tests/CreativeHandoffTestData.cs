using Bliss.Api.Operations;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Tests;

internal sealed record CreativeHandoffFixture(WeddingPlannerWorkspaceResult Workspace, Guid Brief, Guid Candidate, Guid Review,
    Guid Campaign, Guid QuoteLine, Guid Quote, decimal Amount)
{
    public static Guid Slot => Guid.Parse("99999999-9999-9999-9999-999999999995");
}
internal static class CreativeHandoffTestData
{
    public const string Png = "iVBORw0KGgoAAAANSUhEUgAAACAAAAAQCAYAAAB3AH1ZAAAAI0lEQVR4nGMI0Oj5P5CYYdQBow4YdcCoA0YdMOqAUQcMtAMAujoGPVlqwssAAAAASUVORK5CYII=";
    public static async Task<CreativeHandoffFixture> Seed(BlissDbContext db, IWebHostEnvironment environment)
    {
        await new Phase1DataSeeder(db).SeedAsync(); await new Phase2DataSeeder(db).SeedAsync();
        await new EconomicsDataSeeder(db).SeedAsync(); await new EconomicsPhase2DataSeeder(db).SeedAsync();
        await new EconomicsPhase3DataSeeder(db).SeedAsync(); await new EconomicsPhase4DataSeeder(db).SeedAsync();
        var workspace = await new WeddingPlannerService(db).OpenPrimaryWorkspaceAsync(Phase2DataSeeder.Advertiser2Id,
            "HANDOFF_TEST", "fixture-workspace", true, null, "OPERATOR", "Fixture operator", "fixture");
        var brandService = new WeddingPlannerBrandDnaService(db);
        await brandService.AppendAsync(workspace, new BrandDnaFields("TEST Harbor Creative", "pharmacy", "São Paulo, Brazil", "Portuguese",
            "Burgundy", "Montserrat", "Fictional test awareness", "Test fixture only"), 0, "fixture-brand", false, "ADVERTISER", "Fixture owner", "fixture", default);
        var brand = (await brandService.AppendAsync(workspace, null, 1, "fixture-confirm", true, "ADVERTISER", "Fixture owner", "fixture", default)).Revision;
        var production = new AcademyProductionService(db, new BlueprintPhaseService(db, environment));
        var brief = (await production.CreateAsync(workspace, new CreateProductionBrief(brand.Id, "pharmacy", "TEST local care", "TEST visit", "fixture-brief"),
            "ADVERTISER", "Fixture owner", "fixture", default)).Row;
        var candidate = (await production.SubmitAsync(workspace, brief.Id, new SubmitProductionCandidate(0, "TEST logical workflow candidate", "Synthetic PNG; not real visual acceptance", Png, "fixture-candidate"),
            "ADVERTISER", "Fixture owner", "fixture", default)).Row;
        var review = (await production.ReviewAsync(workspace, brief.Id, candidate.Id,
            new RecordProductionReview(0, "PASS", "PASS", "PASS", "PASS", "PASS", false, "Synthetic instructor decision for authority tests", "", "", "fixture-review"),
            "OPERATOR", "Fixture QA", "fixture", default)).Row;
        var campaign = new Campaign { Id = Guid.NewGuid(), AdvertiserOpportunityId = Phase2DataSeeder.OpportunityBrId, Name = "TEST handoff campaign", AuthorityStatus = Bliss.Domain.Common.RecordAuthorityStatuses.SyntheticFixture, CreatedAt = DateTime.UtcNow };
        db.Add(campaign); await db.SaveChangesAsync();
        // Explicitly fictional overlay benchmark; never modify or invent production Economics data.
        var baseline = await db.InventoryRateBenchmarks.AsNoTracking().FirstAsync(x => x.GeographicMarketId == EconomicsDataSeeder.ManilaId
            && x.InventorySlotType == "MID_ROLL");
        db.Add(new InventoryRateBenchmark { Id = Guid.NewGuid(), GeographicMarketId = baseline.GeographicMarketId,
            PricingModelId = baseline.PricingModelId, ResearchSourceId = baseline.ResearchSourceId, InventorySlotType = "PERIMETER_OVERLAY",
            DurationSecondsLow = 1, DurationSecondsHigh = 300, RangeLow = baseline.RangeLow, RangeHigh = baseline.RangeHigh,
            CurrencyCode = baseline.CurrencyCode, ConfidenceLevel = "TEST", VerificationStatus = "TEST_ONLY",
            EffectiveAt = DateTime.UtcNow.AddDays(-1), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var recommendation = (await new RateRecommendationService(db).GenerateAsync(new(Phase2DataSeeder.BrazilCreatorId, CreativeHandoffFixture.Slot,
            EconomicsDataSeeder.ManilaId, "CPM", 60, Phase2DataSeeder.OpportunityBrId, Phase2DataSeeder.MatchBrazilApprovedId,
            "CREATOR_TOOLS", "TEST handoff", "HANDOFF_TEST", "fixture-rate"))).Recommendation;
        var quoteService = new QuoteService(db);
        var quote = await quoteService.CreateAsync(new(Phase2DataSeeder.OpportunityBrId, "Fixture operator", "Adopt fictional Economics recommendation",
            [new QuoteLineCommand(recommendation.Id, "TEST overlay placement", 1, recommendation.RangeTarget)], "HANDOFF_TEST", "fixture-quote"));
        var version = quote.NewQuoteVersionId!.Value;
        await quoteService.DecideApprovalAsync(new(quote.Quote.Id, version, QuoteApprovalDecisions.Approved, "Fixture quote reviewer", "Fictional approval", "HANDOFF_TEST", "fixture-quote-approval"));
        await quoteService.RecordOutcomeAsync(new(quote.Quote.Id, version, QuoteOutcomeResponses.Accepted, "Fixture owner", "Fictional acceptance", null, "HANDOFF_TEST", "fixture-quote-accept"));
        var line = quote.Quote.Versions.Single(x => x.Id == version).LineItems.Single();
        return new(workspace, brief.Id, candidate.Id, review.Id, campaign.Id, line.Id, quote.Quote.Id, line.LineAmount);
    }
    public static DecideCandidateApproval Approval(CreativeHandoffFixture f, string stage, string key, int version = 0, string decision = "APPROVE") =>
        new(stage, decision, f.Review, stage == "INVENTORY_FIT" ? CreativeHandoffFixture.Slot : null,
            version, "Fictional human decision for workflow validation", stage == "INVENTORY_FIT", stage == "INVENTORY_FIT", stage == "INVENTORY_FIT", key);
    public static async Task<PlanCreativeHandoff> Approve(BlissDbContext db, IWebHostEnvironment environment, CreativeHandoffFixture f)
    {
        var service = new CreativeHandoffService(db, new AcademyProductionService(db, new BlueprintPhaseService(db, environment)), new CampaignPlacementService(db));
        var advertiser = (await service.Decide(f.Workspace, f.Candidate, Approval(f, "ADVERTISER", "fixture-client-approval"), "fixture-client", "Fixture client", true, default)).Row;
        var qa = (await service.Decide(f.Workspace, f.Candidate, Approval(f, "ALPHA_QA", "fixture-qa-approval"), "fixture-qa", "Fixture QA", true, default)).Row;
        var fit = (await service.Decide(f.Workspace, f.Candidate, Approval(f, "INVENTORY_FIT", "fixture-fit-approval"), "fixture-qa", "Fixture QA", true, default)).Row;
        return new(f.Candidate, f.Review, advertiser.Id, qa.Id, fit.Id, Phase2DataSeeder.MatchBrazilApprovedId, f.Campaign, CreativeHandoffFixture.Slot, f.QuoteLine, "fixture-plan");
    }
}
