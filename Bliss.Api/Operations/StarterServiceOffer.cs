using System.Text.Json;
using Bliss.Api.Controllers;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// The fixed-price direct service is distinct from creator-inventory rate quotes.
// Append-only offer and acceptance records reuse the existing workspace audit store.
public static class StarterServiceOffer
{
    public const string OfferAction = "STARTER_SERVICE_OFFER";
    public const string AcceptAction = "STARTER_SERVICE_ACCEPTED";
    public sealed record Terms(Guid CampaignId,string CampaignDigest,string Scope,string TermsText,int AmountCents,string Currency);
    public sealed record Accepted(Guid AdvertiserId,Guid VersionId,Guid WorkspaceId);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Serialize(Terms terms) => JsonSerializer.Serialize(terms,Json);
    public static Terms? Parse(string? detail)
    {
        try {
            if (detail is not { Length: > 0 and <= 2000 }) return null;
            var t=JsonSerializer.Deserialize<Terms>(detail,Json);
            return t is not null && t.CampaignId!=Guid.Empty && t.CampaignDigest is {Length:64}
                && t.AmountCents==14900 && t.Currency=="usd" && !string.IsNullOrWhiteSpace(t.Scope)
                && !string.IsNullOrWhiteSpace(t.TermsText) ? t : null;
        } catch(JsonException){return null;}
    }
    public static Guid OfferId(Guid campaignId,string digest) =>
        StarterArtworkDelivery.StableId(OfferAction,campaignId.ToString("N")+":"+digest);
    public static Guid AcceptanceId(Guid offerId) => StarterArtworkDelivery.StableId(AcceptAction,offerId.ToString("N"));
    public static async Task<bool> ActiveMember(BlissDbContext db,Guid advertiser,string? subject,CancellationToken ct) =>
        await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
            join receipt in db.AdvertiserRegistrationReceipts.AsNoTracking()
                on new {m.Subject,m.AdvertiserId} equals new {receipt.Subject,receipt.AdvertiserId}
            where m.AdvertiserId==advertiser && m.Status=="ACTIVE" && (subject==null || m.Subject==subject)
                && receipt.ProcessingConsent && receipt.TermsAccepted
                && receipt.PrivacyNoticeVersion==AdvertiserRegistrationController.PrivacyNoticeVersion
                && receipt.TermsVersion==AdvertiserRegistrationController.TermsVersion
            select m.Id).AnyAsync(ct);
    public static async Task<Accepted?> CurrentAccepted(BlissDbContext db,Guid campaignId,Guid offerId,CancellationToken ct)
    {
        var offer=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==offerId
            && x.Action==OfferAction && x.Outcome=="OFFERED" && x.ActorType=="OPERATOR",ct);
        var terms=Parse(offer?.Detail);
        if(offer is null || terms is null || terms.CampaignId!=campaignId || offer.WorkspaceId is null
            || offerId!=OfferId(campaignId,terms.CampaignDigest))return null;
        var campaign=await db.Campaigns.AsNoTracking().Where(x=>x.Id==campaignId && x.Status=="DRAFT"
            && x.AuthorityStatus=="UNVERIFIED" && x.AdvertiserOpportunity!=null
            && x.AdvertiserOpportunity.Status=="DRAFT" && x.AdvertiserOpportunity.AuthorityStatus=="UNVERIFIED"
            && x.AdvertiserOpportunity.AdvertiserProgram.Status=="DRAFT"
            && x.AdvertiserOpportunity.AdvertiserProgram.ExternalProgramId=="alpha:customer-campaign-draft:v1"
            && x.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId==offer.AdvertiserId)
            .Select(x=>new{x.Name,Brief=x.AdvertiserOpportunity!.Description}).SingleOrDefaultAsync(ct);
        if(campaign?.Brief is null || StaffCreativePreparationController.CampaignDigest(campaignId,campaign.Name,campaign.Brief)!=terms.CampaignDigest
            || !await db.WeddingPlannerWorkspaces.AnyAsync(x=>x.Id==offer.WorkspaceId && x.AdvertiserId==offer.AdvertiserId && x.IsPrimary && x.Status=="ACTIVE",ct))return null;
        var digest=ControlledAiService.Hash(offer.Detail!);
        var acceptance=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==AcceptanceId(offerId)
            && x.AdvertiserId==offer.AdvertiserId && x.WorkspaceId==offer.WorkspaceId && x.Action==AcceptAction
            && x.ActorType=="ADVERTISER" && x.Outcome=="ACCEPTED" && x.Detail==digest,ct);
        if(acceptance is null || acceptance.ActorLabel==offer.ActorLabel || !await ActiveMember(db,offer.AdvertiserId,acceptance.ActorLabel,ct))return null;
        return new Accepted(offer.AdvertiserId,offerId,offer.WorkspaceId.Value);
    }
}
