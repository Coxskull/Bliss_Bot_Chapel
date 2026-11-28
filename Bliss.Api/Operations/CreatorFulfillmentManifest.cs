using System.Security.Cryptography;
using System.Text.Json;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// A local preparation manifest only. No dispatch, payment or production activation.
public static class CreatorFulfillmentManifest
{
    public static async Task<object?> Build(BlissDbContext db,Guid handoffId,Guid illustrationId,CancellationToken ct)
    {
        var handoff=await db.CreativeCampaignHandoffs.AsNoTracking().SingleOrDefaultAsync(h=>h.Id==handoffId,ct);
        if(handoff is null || handoff.Status!="PLANNED" || handoff.Delivery!="NOT_SENT"
            || handoff.SnapshotJson.Length is <2 or >32768)return null;
        var placement=await CreatorCampaignInvitation.Current(db,handoff.CampaignPlacementId,ct);
        if(placement is null || placement.WorkspaceId!=handoff.WorkspaceId)return null;
        var invitationId=CreatorCampaignInvitation.InvitationId(placement.PlacementId);
        var proposal=await CreatorCompensationProposal.Current(db,invitationId,illustrationId,ct);
        if(proposal is null || proposal.QuoteLineId!=handoff.QuoteLineItemId || proposal.PlacementId!=placement.PlacementId
            || !await CreatorCompensationProposal.HasCurrentAcknowledgment(db,proposal,ct))return null;
        var proposalId=CreatorCompensationProposal.Id(invitationId);
        var published=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==proposalId,ct);
        if(published is null || !CreatorCompensationProposal.Matches(published,proposal))return null;
        var agreementId=CreatorCompensationProposal.AgreementId(proposalId);
        var agreement=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==agreementId,ct);
        if(agreement is null || !CreatorCompensationProposal.MatchesAgreement(agreement,proposal))return null;
        var run=await db.CampaignPlacementRuns.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==handoff.CampaignPlacementRunId,ct);
        if(run is null || run.CampaignPlacementId!=placement.PlacementId || run.CampaignId!=placement.CampaignId
            || run.CreatorId!=placement.CreatorId || run.AdInventorySlotId!=placement.SlotId
            || run.SourceSystem!="CREATIVE_HANDOFF_V1" || run.Outcome!="PLANNED" || run.Status!="COMPLETED")return null;
        var candidate=await db.AcademyProductionCandidates.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==handoff.CandidateId,ct);
        if(candidate is null || candidate.ImageBytes.Length is <24 or >16000000
            || !CreatorCompensationProposal.ValidDocumentHash(candidate.ImageSha256)
            || Convert.ToHexString(SHA256.HashData(candidate.ImageBytes)).ToLowerInvariant()!=candidate.ImageSha256)return null;
        var brief=await db.AcademyProductionBriefs.AsNoTracking().SingleOrDefaultAsync(b=>b.Id==candidate.BriefId && b.WorkspaceId==handoff.WorkspaceId,ct);
        if(brief is null || await db.AcademyProductionCandidates.AnyAsync(c=>c.BriefId==brief.Id && c.Version>candidate.Version,ct))return null;
        var brand=await db.WeddingPlannerBrandDna.AsNoTracking().Where(b=>b.WorkspaceId==handoff.WorkspaceId).OrderByDescending(b=>b.Version).FirstOrDefaultAsync(ct);
        if(brand is null || brand.Id!=brief.BrandDnaId || brand.Status!="CONFIRMED")return null;
        var review=await db.AcademyProductionReviews.AsNoTracking().Where(r=>r.CandidateId==candidate.Id).OrderByDescending(r=>r.Version).FirstOrDefaultAsync(ct);
        if(review is null || review.Id!=handoff.ReviewId || review.Decision!="PASS" || review.CriticalDefect
            || new[]{review.Customization,review.QualityParity,review.Originality,review.GeographicAuthenticity}.Any(x=>x!="PASS"))return null;
        var scopes=new[]{"ADVERTISER","ALPHA_QA",$"FIT:{placement.SlotId}"};
        var expected=new[]{handoff.AdvertiserApprovalId,handoff.AlphaQaApprovalId,handoff.InventoryFitId};
        var approvals=new List<Bliss.Domain.Entities.CandidateApprovalDecision>();
        for(var i=0;i<scopes.Length;i++){
            var scope=scopes[i];
            var latest=await db.CandidateApprovalDecisions.AsNoTracking().Where(a=>a.CandidateId==candidate.Id && a.ScopeKey==scope)
                .OrderByDescending(a=>a.Version).FirstOrDefaultAsync(ct);
            if(latest is null || latest.Id!=expected[i] || latest.Decision!="APPROVE" || latest.ReviewId!=review.Id)return null;
            approvals.Add(latest);
        }
        var fit=approvals[2];
        if(approvals[0].ActorSubject==approvals[1].ActorSubject || fit.AdInventorySlotId!=placement.SlotId
            || !fit.DimensionsVerified || !fit.FormatVerified || !fit.CreatorSafeAreaVerified || fit.ImageWidth<=1 || fit.ImageHeight<=1)return null;
        try{
            using var doc=JsonDocument.Parse(handoff.SnapshotJson);var frozen=doc.RootElement;
            if(frozen.GetProperty("imageSha256").GetString()!=candidate.ImageSha256
                || frozen.GetProperty("campaignId").GetGuid()!=placement.CampaignId
                || frozen.GetProperty("slotId").GetGuid()!=placement.SlotId
                || frozen.GetProperty("matchId").GetGuid()!=run.BlissMatchId
                || frozen.GetProperty("contentItemId").GetGuid()!=run.ContentItemId
                || frozen.GetProperty("quoteLineId").GetGuid()!=proposal.QuoteLineId
                || frozen.GetProperty("quoteVersionId").GetGuid()!=proposal.QuoteVersionId
                || frozen.GetProperty("currencyCode").GetString()!=proposal.CurrencyCode)return null;
        }catch(Exception e) when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){return null;}
        return new {schema="alpha-creator-fulfillment-preparation/v1",handoffId,placementId=placement.PlacementId,
            campaignId=placement.CampaignId,creatorId=placement.CreatorId,slotId=placement.SlotId,slotType=placement.SlotType,
            startSecond=placement.StartSecond,durationSeconds=placement.DurationSeconds,candidateId=candidate.Id,
            imageSha256=candidate.ImageSha256,imageWidth=fit.ImageWidth,imageHeight=fit.ImageHeight,reviewId=review.Id,
            advertiserApprovalId=expected[0],alphaQaApprovalId=expected[1],inventoryFitId=expected[2],
            quoteVersionId=proposal.QuoteVersionId,quoteLineId=proposal.QuoteLineId,agreementEvidenceId=agreement.Id,
            handoffSha256=ControlledAiService.Hash(handoff.SnapshotJson),agreementEvidenceSha256=ControlledAiService.Hash(agreement.Detail!),
            status="PREPARATION_ONLY",delivery="NOT_SENT",fundingVerified=false,bookingAuthorized=false,paymentAuthorized=false,
            deliveryAuthorized=false,factoryDispatchAuthorized=false};
    }
}
