using System.Text.Json;
using Bliss.Domain.Entities;

namespace Bliss.Api.Operations;

// Immutable human review of one held preview, never a release or payment.
public static class CreatorFactoryVisualReview
{
    public const string Action="CREATOR_FACTORY_VISUAL_REVIEW";
    public sealed record Preview(string SpecSha256,string CompositionSha256,string PreviewSha256,string QaSha256,string ReceiptSha256,string TechnicalStatus);
    public static Guid Id(Guid placementId,string specSha256)=>StarterArtworkDelivery.StableId(Action,placementId.ToString("N")+":"+specSha256);
    public static bool ValidDecision(string? decision)=>decision is "VISUAL_ACCEPTED" or "REVISION_REQUESTED";
    public static Preview? Select(JsonElement result,string spec)
    {
        var matches=result.GetProperty("results").EnumerateArray().Where(r=>r.GetProperty("specSha256").GetString()==spec).ToArray();
        if(matches.Length!=1)return null;
        var row=matches[0];var status=row.GetProperty("status").GetString();
        if(status is not ("PRIVATE_PREVIEW_READY" or "REVIEW_REQUIRED"))return null;
        var hashes=new[]{"compositionSha256","previewSha256","qaSha256","receiptSha256"}.Select(k=>row.GetProperty(k).GetString()).ToArray();
        if(!CreatorCompensationProposal.ValidDocumentHash(spec)||hashes.Any(x=>!CreatorCompensationProposal.ValidDocumentHash(x)))return null;
        return new(spec,hashes[0]!,hashes[1]!,hashes[2]!,hashes[3]!,status);
    }
    public static string Detail(Guid placementId,string manifest,Preview p,string reviewDocumentHash)=>
        string.Join(":",placementId.ToString("N"),manifest,p.SpecSha256,p.CompositionSha256,p.PreviewSha256,p.QaSha256,p.ReceiptSha256,p.TechnicalStatus,reviewDocumentHash);
    public static bool Matches(WeddingPlannerAuditEvent audit,CreatorCampaignInvitation.Snapshot placement,string manifest,Preview p)
    {
        if(audit.Id!=Id(placement.PlacementId,p.SpecSha256)||audit.Action!=Action||audit.ActorType!="OPERATOR"||
            !ValidDecision(audit.Outcome)||audit.AdvertiserId!=placement.AdvertiserId||audit.WorkspaceId!=placement.WorkspaceId||
            audit.RequestId!=placement.CreatorId.ToString("N")||audit.Detail?.Split(':') is not {Length:9} fields||
            !CreatorCompensationProposal.ValidDocumentHash(fields[8]))return false;
        return audit.Detail==Detail(placement.PlacementId,manifest,p,fields[8])
            && (audit.Outcome!="VISUAL_ACCEPTED"||p.TechnicalStatus=="PRIVATE_PREVIEW_READY");
    }
}
