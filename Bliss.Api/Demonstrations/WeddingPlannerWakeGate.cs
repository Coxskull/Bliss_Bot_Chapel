using Bliss.Domain.Demonstrations;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Demonstrations;

/// <summary>
/// Opens the existing Wedding Planner workspace only after an accepted
/// Economics result and an advertiser already on file.
/// </summary>
public sealed class WeddingPlannerWakeGate(
    BlissDbContext database,
    EconomicsAcceptedPriceReader economics,
    WeddingPlannerService planner)
{
    public const string SourceSystem = "acquisition";

    public async Task<WeddingPlannerWakeDecision> DecideAsync(
        DemonstrationRecord prospect,
        Guid? advertiserId,
        CancellationToken cancellationToken)
    {
        Guid? quoteId = Guid.TryParse(prospect.EconomicsQuoteId, out var parsed) ? parsed : null;
        var accepted = quoteId is Guid id
            ? await economics.ReadAcceptedAsync(id, cancellationToken)
            : null;
        var advertiserExists = advertiserId is Guid requested
            && await database.Advertisers.AsNoTracking().AnyAsync(item => item.Id == requested, cancellationToken);
        var decision = WeddingPlannerWake.Decide(
            prospect.BusinessName,
            prospect.Market,
            prospect.Suppressed,
            accepted,
            quoteId,
            advertiserExists ? advertiserId : null);
        if (decision.Status != WeddingPlannerWake.Awake || decision.AdvertiserId is null || decision.QuoteId is null)
        {
            return decision;
        }

        var workspace = await planner.OpenPrimaryWorkspaceAsync(
            decision.AdvertiserId.Value,
            SourceSystem,
            "wake:" + prospect.Slug,
            true,
            null,
            WeddingPlannerActorTypes.System,
            "SYSTEM",
            null,
            cancellationToken);
        var sessionKey = "wake:" + prospect.Slug + ":" + decision.QuoteId.Value.ToString("N");
        var session = await planner.CreateSessionAsync(
            workspace.WorkspaceId,
            SourceSystem,
            sessionKey,
            true,
            null,
            WeddingPlannerActorTypes.System,
            "SYSTEM",
            null,
            cancellationToken);
        await planner.AppendMessageAsync(
            session.SessionId,
            WeddingPlannerActorTypes.System,
            decision.Notice,
            SourceSystem,
            "wake-message:" + prospect.Slug + ":" + decision.QuoteId.Value.ToString("N"),
            true,
            null,
            WeddingPlannerActorTypes.System,
            "SYSTEM",
            null,
            cancellationToken);
        return decision with { WorkspaceId = workspace.WorkspaceId, SessionId = session.SessionId };
    }
}
