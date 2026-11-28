using System.Globalization;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// Append-only hold on an original settlement attestation, even when its
// agreement or historical allocation is no longer current. Never reopens
// a payable, initiates a refund, or permits a second transfer.
public static class CreatorSettlementException
{
    public const string Action = "CREATOR_SETTLEMENT_EXCEPTION";
    public sealed record Snapshot(Guid EvidenceId, Guid HistoryId, Guid PlacementId, Guid CreatorId,
        Guid AdvertiserId, Guid WorkspaceId, string OriginalDigest, decimal Amount, string CurrencyCode, DateTimeOffset TransferAt);
    public static Guid Id(Guid evidenceId) => StarterArtworkDelivery.StableId(Action,evidenceId.ToString("N"));
    public static bool ValidReason(string? value) => value is "FULL_REVERSAL_REPORTED" or "DISPUTE_REPORTED" or "RECORD_CORRECTION_REQUIRED";
    public static string Detail(Snapshot value,string documentHash,DateTimeOffset observedAt) =>
        value.EvidenceId.ToString("N")+":"+value.OriginalDigest+":"+documentHash+":"+observedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    public static async Task<Snapshot?> Original(BlissDbContext db,Guid evidenceId,CancellationToken ct)
    {
        var audit=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==evidenceId,ct);
        if(audit is null || audit.Action!=CreatorSettlementEvidence.Action || audit.ActorType!="OPERATOR"
            || audit.Outcome!="EVIDENCE_RECORDED" || !audit.WorkspaceId.HasValue
            || audit.Detail is null || audit.Detail.Split(':') is not {Length:6} parts
            || !Guid.TryParseExact(parts[0],"N",out var historyId) || !Guid.TryParseExact(parts[2],"N",out _)
            || !CreatorCompensationProposal.ValidDocumentHash(parts[1]) || !CreatorCompensationProposal.ValidDocumentHash(parts[3])
            || !CreatorCompensationProposal.ValidDocumentHash(parts[4]) || audit.RequestId!=parts[3]
            || !long.TryParse(parts[5],NumberStyles.None,CultureInfo.InvariantCulture,out var unix) || unix<0 || unix>253402300799L)return null;
        var h=await db.HistoricalPlacementEconomics.AsNoTracking().Where(x=>x.Id==historyId)
            .Select(x=>new {x.CampaignPlacementId,x.CreatorCompensationAmount,x.CurrencyCode,
                CreatorId=x.CampaignPlacement.ContentItem.CreatorId,
                AdvertiserId=x.CampaignPlacement.Campaign.AdvertiserOpportunity==null ? (Guid?)null :
                    x.CampaignPlacement.Campaign.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId})
            .SingleOrDefaultAsync(ct);
        if(h is null || h.AdvertiserId!=audit.AdvertiserId || h.CreatorCompensationAmount is null or <=0
            || h.CurrencyCode.Length!=3 || !h.CurrencyCode.All(c=>c is >= 'A' and <= 'Z')
            || CreatorSettlementEvidence.Id(h.CampaignPlacementId)!=audit.Id
            || !await db.WeddingPlannerWorkspaces.AnyAsync(w=>w.Id==audit.WorkspaceId && w.AdvertiserId==audit.AdvertiserId,ct))return null;
        var transferAt=DateTimeOffset.FromUnixTimeSeconds(unix);
        if(transferAt.UtcDateTime>audit.OccurredAt)return null;
        return new Snapshot(audit.Id,historyId,h.CampaignPlacementId,h.CreatorId,audit.AdvertiserId,audit.WorkspaceId.Value,
            ControlledAiService.Hash(audit.Detail),h.CreatorCompensationAmount.Value,h.CurrencyCode,transferAt);
    }
    public static bool Matches(WeddingPlannerAuditEvent audit,Snapshot value)
    {
        if(audit.Id!=Id(value.EvidenceId) || audit.Action!=Action || audit.ActorType!="OPERATOR"
            || !ValidReason(audit.Outcome) || audit.AdvertiserId!=value.AdvertiserId || audit.WorkspaceId!=value.WorkspaceId
            || audit.RequestId!=value.CreatorId.ToString("N") || audit.Detail is null || audit.Detail.Split(':') is not {Length:4} parts
            || !CreatorCompensationProposal.ValidDocumentHash(parts[2])
            || !long.TryParse(parts[3],NumberStyles.None,CultureInfo.InvariantCulture,out var unix) || unix<0 || unix>253402300799L)return false;
        var observed=DateTimeOffset.FromUnixTimeSeconds(unix);
        return observed>=value.TransferAt && observed.UtcDateTime<=audit.OccurredAt
            && audit.Detail==Detail(value,parts[2],observed);
    }
}
