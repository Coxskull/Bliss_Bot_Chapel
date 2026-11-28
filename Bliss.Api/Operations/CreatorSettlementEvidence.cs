using System.Globalization;
using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// An operator attestation about an already completed external transfer.
// Never a transfer instruction, payable calculation, or provider verification.
public static class CreatorSettlementEvidence
{
    public const string Action = "CREATOR_SETTLEMENT_EVIDENCE";
    public sealed record Snapshot(CreatorAllocationHistory.Snapshot Allocation, Guid AgreementEvidenceId,
        string AgreementDigest, DateTime AgreementRecordedAt);
    public static Guid Id(Guid placementId) => StarterArtworkDelivery.StableId(Action,placementId.ToString("N"));
    public static string Digest(Snapshot value) => ControlledAiService.Hash(JsonSerializer.Serialize(value));
    public static string Detail(Snapshot value,string transferHash,string documentHash,DateTimeOffset completedAt) =>
        value.Allocation.HistoryId.ToString("N")+":"+Digest(value)+":"+value.AgreementEvidenceId.ToString("N")+":"+
        transferHash+":"+documentHash+":"+completedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    public static async Task<Snapshot?> Current(BlissDbContext db,Guid historyId,CancellationToken ct)
    {
        var allocation=await CreatorAllocationHistory.Current(db,historyId,ct);
        if(allocation is null)return null;
        var publicationId=CreatorAllocationHistory.Id(historyId);
        var publication=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==publicationId,ct);
        if(publication is null || !CreatorAllocationHistory.Matches(publication,allocation))return null;
        var invitationId=CreatorCampaignInvitation.InvitationId(allocation.PlacementId);
        var proposalId=CreatorCompensationProposal.Id(invitationId);
        // Reuse the exact previously reviewed proposal and recorded agreement.
        // Current planning guards remain intact: later-state placements require
        // a separate historical agreement verifier before reconciliation.
        var proposal=await CreatorCompensationProposal.Current(db,invitationId,allocation.IllustrationId,ct);
        if(proposal is null || proposal.CreatorId!=allocation.CreatorId || proposal.AdvertiserId!=allocation.AdvertiserId
            || proposal.WorkspaceId!=allocation.WorkspaceId || proposal.PlacementId!=allocation.PlacementId
            || proposal.QuoteLineId!=allocation.QuoteLineId || proposal.CreatorAmount!=allocation.CreatorAllocation
            || proposal.CurrencyCode!=allocation.CurrencyCode || !await CreatorCompensationProposal.HasCurrentAcknowledgment(db,proposal,ct))return null;
        var published=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==proposalId,ct);
        if(published is null || !CreatorCompensationProposal.Matches(published,proposal))return null;
        var agreementId=CreatorCompensationProposal.AgreementId(proposalId);
        var agreement=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==agreementId,ct);
        if(agreement is null || !CreatorCompensationProposal.MatchesAgreement(agreement,proposal))return null;
        return new Snapshot(allocation,agreement.Id,ControlledAiService.Hash(agreement.Detail!),agreement.OccurredAt);
    }
    public static bool Matches(WeddingPlannerAuditEvent audit,Snapshot value,out DateTimeOffset completedAt)
    {
        completedAt=default;
        if(audit.Id!=Id(value.Allocation.PlacementId) || audit.Action!=Action || audit.ActorType!="OPERATOR"
            || audit.Outcome!="EVIDENCE_RECORDED" || audit.AdvertiserId!=value.Allocation.AdvertiserId
            || audit.WorkspaceId!=value.Allocation.WorkspaceId || audit.Detail is null
            || audit.Detail.Split(':') is not {Length:6} parts
            || !CreatorCompensationProposal.ValidDocumentHash(parts[3]) || !CreatorCompensationProposal.ValidDocumentHash(parts[4])
            || audit.RequestId!=parts[3] || !long.TryParse(parts[5],NumberStyles.None,CultureInfo.InvariantCulture,out var unix)
            || unix<0 || unix>253402300799L)return false;
        completedAt=DateTimeOffset.FromUnixTimeSeconds(unix);
        return completedAt.UtcDateTime>=value.AgreementRecordedAt && completedAt.UtcDateTime<=audit.OccurredAt
            && audit.Detail==Detail(value,parts[3],parts[4],completedAt);
    }
}
