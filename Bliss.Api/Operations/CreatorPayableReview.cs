using System.Globalization;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// A full-allocation review for an externally fulfilled placement. This records
// human evidence, not an accounting liability, bank balance or payment command.
public static class CreatorPayableReview
{
    public const string Action = "CREATOR_PAYABLE_REVIEW_RECORDED";
    public static Guid Id(Guid placementId) => StarterArtworkDelivery.StableId(Action,placementId.ToString("N"));
    public static string Detail(CreatorSettlementEvidence.Snapshot value,string fulfillmentHash,string fundingHash,DateTimeOffset fulfilledAt) =>
        value.Allocation.HistoryId.ToString("N")+":"+CreatorSettlementEvidence.Digest(value)+":"+fulfillmentHash+":"+fundingHash+
        ":"+fulfilledAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    public static bool Matches(WeddingPlannerAuditEvent audit,CreatorSettlementEvidence.Snapshot value,out DateTimeOffset fulfilledAt)
    {
        fulfilledAt=default;
        if(audit.Id!=Id(value.Allocation.PlacementId) || audit.Action!=Action || audit.ActorType!="OPERATOR"
            || audit.Outcome!="REVIEW_RECORDED" || audit.AdvertiserId!=value.Allocation.AdvertiserId
            || audit.WorkspaceId!=value.Allocation.WorkspaceId || audit.RequestId!=value.Allocation.CreatorId.ToString("N")
            || audit.Detail is null || audit.Detail.Split(':') is not {Length:5} parts
            || !CreatorCompensationProposal.ValidDocumentHash(parts[2]) || !CreatorCompensationProposal.ValidDocumentHash(parts[3])
            || !long.TryParse(parts[4],NumberStyles.None,CultureInfo.InvariantCulture,out var unix) || unix<0 || unix>253402300799L)
            return false;
        fulfilledAt=DateTimeOffset.FromUnixTimeSeconds(unix);
        return fulfilledAt.UtcDateTime>=value.AgreementRecordedAt && fulfilledAt.UtcDateTime<=audit.OccurredAt
            && audit.Detail==Detail(value,parts[2],parts[3],fulfilledAt);
    }

    public static async Task<WeddingPlannerAuditEvent?> FindCurrent(BlissDbContext db,CreatorSettlementEvidence.Snapshot value,CancellationToken ct)
    {
        var id=Id(value.Allocation.PlacementId);
        var row=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
        return row is not null && Matches(row,value,out _) ? row : null;
    }
}
