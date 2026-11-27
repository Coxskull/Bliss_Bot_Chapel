using System.Text.Json;
using Bliss.Api.Controllers;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// Historical allocation evidence is analytical, not a payable, cash balance,
// proof of delivery, or settlement. Explicit operator publication controls disclosure.
public static class CreatorAllocationHistory
{
    public const string Action = "CREATOR_ALLOCATION_HISTORY_SHARED";
    public sealed record Snapshot(Guid HistoryId, Guid PlacementId, Guid CreatorId, Guid AdvertiserId,
        Guid WorkspaceId, Guid QuoteLineId, Guid IllustrationId, string CampaignName,
        string CurrencyCode, decimal ContractedAmount, decimal CreatorAllocation,
        decimal AlphaAllocation, decimal OtherAllocation, DateTime MeasurementAsOf, DateTime RecordedAt);
    public static Guid Id(Guid historyId) => StarterArtworkDelivery.StableId(Action,historyId.ToString("N"));
    public static string Digest(Snapshot value) => ControlledAiService.Hash(JsonSerializer.Serialize(value));
    public static string Detail(Snapshot value) => value.HistoryId.ToString("N")+":"+Digest(value);

    public static async Task<Snapshot?> Current(BlissDbContext db,Guid historyId,CancellationToken ct)
    {
        var h=await db.HistoricalPlacementEconomics.AsNoTracking()
            .Include(x=>x.CampaignPlacement).ThenInclude(x=>x.ContentItem).ThenInclude(x=>x.Creator)
            .Include(x=>x.CampaignPlacement).ThenInclude(x=>x.AdInventorySlot)
            .Include(x=>x.CampaignPlacement).ThenInclude(x=>x.Campaign).ThenInclude(x=>x.AdvertiserOpportunity).ThenInclude(x=>x!.AdvertiserProgram)
            .Include(x=>x.RateRecommendation).Include(x=>x.QuoteVersion).ThenInclude(x=>x.LineItems)
            .Include(x=>x.QuoteOutcome).ThenInclude(x=>x.Quote).Include(x=>x.QuoteLineItem)
            .Include(x=>x.CompensationIllustration).ThenInclude(x=>x!.Lines)
            .SingleOrDefaultAsync(x=>x.Id==historyId,ct);
        if(h is null || h.CompensationIllustration is null || !h.CompensationIllustrationId.HasValue
            || h.CreatorCompensationAmount is null or <=0 || h.AlphaCompensationAmount is null or <0
            || h.OtherCompensationAmount is null or <0 || h.ContractedAmount<=0
            || h.CurrencyCode.Length!=3 || !h.CurrencyCode.All(c=>c is >= 'A' and <= 'Z'))return null;
        var placement=h.CampaignPlacement;
        var opportunity=placement.Campaign.AdvertiserOpportunity;
        var creator=placement.ContentItem.CreatorId;
        var allocation=h.CompensationIllustration;
        if(opportunity is null || opportunity.AuthorityStatus!="VERIFIED" || placement.Campaign.AuthorityStatus!="VERIFIED"
            || placement.ContentItem.Creator.AuthorityStatus!="VERIFIED" || placement.AdInventorySlot.ContentItemId!=placement.ContentItemId
            || placement.BlissMatchId is null || h.RateRecommendation.BlissMatchId!=placement.BlissMatchId
            || h.RateRecommendation.AdInventorySlotId!=placement.AdInventorySlotId || h.RateRecommendation.CreatorId!=creator
            || h.RateRecommendation.AdvertiserOpportunityId!=opportunity.Id
            || h.QuoteLineItem.RateRecommendationId!=h.RateRecommendationId || h.QuoteLineItem.QuoteVersionId!=h.QuoteVersionId
            || h.QuoteVersion.LineItems.Count!=1 || h.QuoteLineItem.Quantity!=1
            || h.QuoteOutcome.QuoteVersionId!=h.QuoteVersionId || h.QuoteOutcome.QuoteId!=h.QuoteVersion.QuoteId
            || h.QuoteOutcome.Quote.AdvertiserOpportunityId!=opportunity.Id
            || h.QuoteOutcome.Response!="ACCEPTED" || h.QuoteOutcome.Amount!=h.ContractedAmount
            || h.QuoteOutcome.CurrencyCode!=h.CurrencyCode || h.QuoteLineItem.CurrencyCode!=h.CurrencyCode
            || h.QuoteVersion.CurrencyCode!=h.CurrencyCode || h.QuoteVersion.TotalAmount!=h.ContractedAmount
            || allocation.QuoteId!=h.QuoteVersion.QuoteId || allocation.QuoteVersionId!=h.QuoteVersionId
            || allocation.QuoteOutcomeId!=h.QuoteOutcomeId || allocation.CurrencyCode!=h.CurrencyCode || allocation.GrossAmount!=h.ContractedAmount
            || allocation.Lines.Any(l=>l.Amount<0) || allocation.Lines.Count(l=>l.ParticipantRole=="CREATOR")!=1
            || allocation.Lines.Any(l=>l.ParticipantRole!="CREATOR" && l.ParticipantRole!="ALPHA" && l.ParticipantRole!="OTHER_AUTHORIZED")
            || allocation.Lines.Sum(l=>l.Amount)!=h.ContractedAmount
            || allocation.Lines.Where(l=>l.ParticipantRole=="CREATOR").Sum(l=>l.Amount)!=h.CreatorCompensationAmount
            || allocation.Lines.Where(l=>l.ParticipantRole=="ALPHA").Sum(l=>l.Amount)!=h.AlphaCompensationAmount
            || allocation.Lines.Where(l=>l.ParticipantRole=="OTHER_AUTHORIZED").Sum(l=>l.Amount)!=h.OtherCompensationAmount)return null;
        // Fail closed on superseded records and ambiguous current heads. No double counting.
        var heads=await db.HistoricalPlacementEconomics.AsNoTracking()
            .Where(x=>x.CampaignPlacementId==placement.Id && !db.HistoricalPlacementEconomics.Any(y=>y.SupersedesHistoricalPlacementEconomicsId==x.Id))
            .Select(x=>x.Id).Take(2).ToArrayAsync(ct);
        if(heads.Length!=1 || heads[0]!=h.Id)return null;
        if(!await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==creator && m.Status=="ACTIVE"
            && db.CreatorRegistrationReceipts.Any(r=>r.Subject==m.Subject && r.CreatorId==m.CreatorId
                && r.ProcessingConsent && r.TermsAccepted && r.PrivacyNoticeVersion==CreatorRegistrationController.PrivacyNoticeVersion
                && r.TermsVersion==CreatorRegistrationController.TermsVersion),ct))return null;
        var advertiser=opportunity.AdvertiserProgram.AdvertiserId;
        var workspace=await db.WeddingPlannerWorkspaces.AsNoTracking().Where(w=>w.AdvertiserId==advertiser && w.IsPrimary && w.Status=="ACTIVE")
            .Select(w=>(Guid?)w.Id).SingleOrDefaultAsync(ct);
        if(workspace is null)return null;
        return new Snapshot(h.Id,placement.Id,creator,advertiser,workspace.Value,h.QuoteLineItemId,allocation.Id,placement.Campaign.Name,
            h.CurrencyCode,h.ContractedAmount,h.CreatorCompensationAmount.Value,h.AlphaCompensationAmount.Value,h.OtherCompensationAmount.Value,
            h.MeasurementAsOf,h.RecordedAt);
    }
    public static bool Matches(WeddingPlannerAuditEvent audit,Snapshot value) =>
        audit.Id==Id(value.HistoryId) && audit.Action==Action && audit.ActorType=="OPERATOR" && audit.Outcome=="SHARED"
        && audit.AdvertiserId==value.AdvertiserId && audit.WorkspaceId==value.WorkspaceId
        && audit.RequestId==value.CreatorId.ToString("N") && audit.Detail==Detail(value);
}
