using System.Text.Json;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// Exact existing Economics allocation; no invented percentages, payables or payouts.
public static class CreatorCompensationProposal
{
    public const string Action = "CREATOR_COMPENSATION_PROPOSED";
    public const string ResponseAction = "CREATOR_COMPENSATION_RESPONSE";
    public const string AgreementAction = "CREATOR_AGREEMENT_EVIDENCE";
    public static Guid AgreementId(Guid proposalId) => StarterArtworkDelivery.StableId(AgreementAction,proposalId.ToString("N"));
    public static bool ValidDocumentHash(string? value) => value is {Length:64} && value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static string AgreementDetail(Snapshot value,string documentHash) =>
        Id(value.InvitationId).ToString("N")+":"+Digest(value)+":"+documentHash;
    public static bool MatchesAgreement(WeddingPlannerAuditEvent audit,Snapshot value) =>
        audit.Id==AgreementId(Id(value.InvitationId)) && audit.Action==AgreementAction && audit.ActorType=="OPERATOR"
        && audit.Outcome=="EVIDENCE_RECORDED" && audit.AdvertiserId==value.AdvertiserId && audit.WorkspaceId==value.WorkspaceId
        && audit.RequestId==value.CreatorId.ToString("N")
        && audit.Detail is not null && audit.Detail.Split(':') is {Length:3} parts
        && ValidDocumentHash(parts[2]) && audit.Detail==AgreementDetail(value,parts[2]);
    public static async Task<bool> HasCurrentAcknowledgment(BlissDbContext db,Snapshot value,CancellationToken ct)
    {
        var id=ResponseId(Id(value.InvitationId));
        var response=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
        if(response is null || response.Outcome!="ALLOCATION_ACKNOWLEDGED" || !MatchesResponse(response,value,response.ActorLabel))return false;
        return await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==value.CreatorId && m.Subject==response.ActorLabel
            && m.Status=="ACTIVE",ct);
    }
    public static Guid ResponseId(Guid proposalId) => StarterArtworkDelivery.StableId(ResponseAction,proposalId.ToString("N"));
    public static bool MatchesResponse(WeddingPlannerAuditEvent audit, Snapshot value, string subject) =>
        audit.Id==ResponseId(Id(value.InvitationId)) && audit.Action==ResponseAction && audit.ActorType=="CREATOR"
        && audit.ActorLabel==subject && audit.AdvertiserId==value.AdvertiserId && audit.WorkspaceId==value.WorkspaceId
        && audit.Detail==Id(value.InvitationId).ToString("N")+":"+Digest(value)
        && audit.Outcome is "ALLOCATION_ACKNOWLEDGED" or "ALLOCATION_DECLINED";
    public sealed record Snapshot(Guid InvitationId, Guid PlacementId, Guid CreatorId, Guid AdvertiserId,
        Guid WorkspaceId, Guid IllustrationId, Guid QuoteVersionId, Guid QuoteOutcomeId, Guid QuoteLineId,
        decimal CreatorAmount, string CurrencyCode, string PlacementDigest);
    public static Guid Id(Guid invitationId) => StarterArtworkDelivery.StableId(Action,invitationId.ToString("N"));
    public static string Digest(Snapshot value) => ControlledAiService.Hash(JsonSerializer.Serialize(value));
    public static string Detail(Snapshot value) => value.IllustrationId.ToString("N")+":"+Digest(value);
    public static async Task<Snapshot?> Current(BlissDbContext db, Guid invitationId, Guid illustrationId, CancellationToken ct)
    {
        var invite = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a => a.Id == invitationId,ct);
        if (invite?.Detail is null || !Guid.TryParseExact(invite.Detail.Split(':')[0],"N",out var placementId)) return null;
        var placement = await CreatorCampaignInvitation.Current(db,placementId,ct);
        if (placement is null || !CreatorCampaignInvitation.Matches(invite,placement)) return null;
        var responseId = CreatorCampaignInvitation.ResponseId(invitationId);
        var response = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==responseId,ct);
        if(response is null || response.Action!=CreatorCampaignInvitation.ResponseAction || response.ActorType!="CREATOR"
            || response.Outcome!="INTERESTED" || response.AdvertiserId!=placement.AdvertiserId || response.WorkspaceId!=placement.WorkspaceId
            || response.Detail!=invitationId.ToString("N")
            || !await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==placement.CreatorId && m.Subject==response.ActorLabel && m.Status=="ACTIVE",ct)) return null;
        var source = await db.CampaignPlacements.AsNoTracking().Where(p=>p.Id==placementId)
            .Select(p=>new {p.BlissMatchId,p.ContentItemId,p.AdInventorySlotId,p.Campaign.AdvertiserOpportunityId}).SingleAsync(ct);
        var illustration = await db.CompensationIllustrations.AsNoTracking().Include(x=>x.Lines)
            .Include(x=>x.QuoteVersion).ThenInclude(x=>x.LineItems).ThenInclude(x=>x.RateRecommendation)
            .Include(x=>x.Quote).Include(x=>x.QuoteOutcome).Include(x=>x.CompensationRuleVersion)
            .SingleOrDefaultAsync(x=>x.Id==illustrationId,ct);
        if(illustration is null || illustration.Quote.Status!="ACCEPTED"
            || illustration.Quote.AdvertiserOpportunityId!=source.AdvertiserOpportunityId
            || illustration.QuoteVersion.QuoteId!=illustration.QuoteId
            || illustration.QuoteVersion.VersionNumber!=illustration.Quote.CurrentVersionNumber
            || illustration.QuoteOutcome.QuoteId!=illustration.QuoteId || illustration.QuoteOutcome.QuoteVersionId!=illustration.QuoteVersionId
            || illustration.QuoteOutcome.Response!="ACCEPTED" || illustration.QuoteOutcome.Amount!=illustration.GrossAmount
            || illustration.QuoteVersion.TotalAmount!=illustration.GrossAmount || illustration.GrossAmount<=0
            || illustration.CurrencyCode!=illustration.QuoteOutcome.CurrencyCode || illustration.CurrencyCode!=illustration.QuoteVersion.CurrencyCode
            || !illustration.CompensationRuleVersion.IsActive || illustration.CompensationRuleVersion.EffectiveAt>DateTime.UtcNow
            || illustration.QuoteVersion.LineItems.Count!=1) return null;
        var line=illustration.QuoteVersion.LineItems.Single();
        var rate=line.RateRecommendation;
        if(line.Quantity!=1 || line.LineAmount!=illustration.GrossAmount || line.CurrencyCode!=illustration.CurrencyCode
            || rate.CreatorId!=placement.CreatorId || rate.AdInventorySlotId!=source.AdInventorySlotId
            || rate.BlissMatchId!=source.BlissMatchId || rate.AdvertiserOpportunityId!=source.AdvertiserOpportunityId
            || (rate.ContentItemId.HasValue && rate.ContentItemId!=source.ContentItemId)) return null;
        var latestApproval=await db.QuoteApprovalDecisions.AsNoTracking().Where(a=>a.QuoteId==illustration.QuoteId)
            .OrderByDescending(a=>a.CreatedAt).ThenByDescending(a=>a.Id).FirstOrDefaultAsync(ct);
        var latestOutcome=await db.QuoteOutcomes.AsNoTracking().Where(a=>a.QuoteId==illustration.QuoteId)
            .OrderByDescending(a=>a.CreatedAt).ThenByDescending(a=>a.Id).FirstOrDefaultAsync(ct);
        if(latestApproval?.QuoteVersionId!=illustration.QuoteVersionId || latestApproval.Decision!="APPROVED"
            || latestOutcome?.Id!=illustration.QuoteOutcomeId) return null;
        var creatorLines=illustration.Lines.Where(l=>l.ParticipantRole=="CREATOR").ToArray();
        if(creatorLines.Length!=1 || creatorLines[0].Amount<=0 || illustration.Lines.Any(l=>l.Amount<0)
            || illustration.Lines.Sum(l=>l.Amount)!=illustration.GrossAmount) return null;
        return new Snapshot(invitationId,placementId,placement.CreatorId,placement.AdvertiserId,placement.WorkspaceId,
            illustrationId,illustration.QuoteVersionId,illustration.QuoteOutcomeId,line.Id,creatorLines[0].Amount,
            illustration.CurrencyCode,CreatorCampaignInvitation.Digest(placement));
    }
    public static bool Matches(WeddingPlannerAuditEvent audit, Snapshot value) =>
        audit.Id==Id(value.InvitationId) && audit.Action==Action && audit.ActorType=="OPERATOR" && audit.Outcome=="PROPOSED"
        && audit.AdvertiserId==value.AdvertiserId && audit.WorkspaceId==value.WorkspaceId
        && audit.RequestId==value.CreatorId.ToString("N") && audit.Detail==Detail(value);
}
