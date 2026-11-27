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
    // Historical milestones only: no live Stripe status, payment authorization,
    // or commercial delivery authority is inferred from these audit records.
    public static string RecordedProgress(Accepted accepted, Guid campaignId,
        WeddingPlannerAuditEvent? instructions, IReadOnlyList<WeddingPlannerAuditEvent> reconciliations)
    {
        bool Owned(WeddingPlannerAuditEvent row) => row.AdvertiserId == accepted.AdvertiserId
            && row.WorkspaceId == accepted.WorkspaceId && row.ActorType == "OPERATOR";
        if (reconciliations.Count > 1) return "REVIEW_REQUIRED";
        if (reconciliations.Count == 1)
        {
            var row = reconciliations[0];
            var parts = row.Detail?.Split(':');
            if (!Owned(row) || row.Action != AdvertiserOperatorReviewController.StarterReconciliationAction
                || row.Outcome != "RECONCILED" || row.RequestId?.StartsWith("evt_", StringComparison.Ordinal) != true
                || parts is not { Length: 4 } || parts[0] != "quote"
                || parts[1] != accepted.VersionId.ToString("N") || parts[2] != campaignId.ToString("N")
                || parts[3].Length != 64 || !parts[3].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')
                || row.Id != new Guid(Convert.FromHexString(parts[3][..32]))) return "REVIEW_REQUIRED";
            return "RECONCILIATION_RECORDED";
        }
        if (instructions is null) return "AWAITING_PAYMENT_INSTRUCTIONS";
        return Owned(instructions) && instructions.Id == StarterArtworkDelivery.StableId(
                StaffCreativePreparationController.StarterCheckoutIssued, accepted.VersionId.ToString("N"))
            && instructions.Action == StaffCreativePreparationController.StarterCheckoutIssued
            && instructions.Outcome == "ISSUED"
            && instructions.Detail == $"{accepted.VersionId:N}:{campaignId:N}:{StarterStripeReceipt.PaymentLinkId}"
            ? "PAYMENT_INSTRUCTIONS_RECORDED" : "REVIEW_REQUIRED";
    }

    public static async Task<string> OrderProgress(BlissDbContext db, Accepted accepted, Guid campaignId, CancellationToken ct)
    {
        var instructionsId = StarterArtworkDelivery.StableId(
            StaffCreativePreparationController.StarterCheckoutIssued, accepted.VersionId.ToString("N"));
        var instructions = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == instructionsId && x.AdvertiserId == accepted.AdvertiserId && x.WorkspaceId == accepted.WorkspaceId, ct);
        var prefix = "quote:" + accepted.VersionId.ToString("N") + ":";
        var reconciliations = await db.WeddingPlannerAuditEvents.AsNoTracking().Where(x =>
            x.AdvertiserId == accepted.AdvertiserId && x.WorkspaceId == accepted.WorkspaceId
            && x.Action == AdvertiserOperatorReviewController.StarterReconciliationAction
            && x.Detail != null && x.Detail.StartsWith(prefix)).Take(2).ToArrayAsync(ct);
        return RecordedProgress(accepted, campaignId, instructions, reconciliations);
    }

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
