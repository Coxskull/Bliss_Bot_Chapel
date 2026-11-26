using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

// Compact revision keys retain the entire campaign digest and exact review UUID
// within the existing 128-character key column. Original rows are never rewritten.
public static class CampaignRecipeRevision
{
    public static string OriginalKey(Guid campaignId,string digest) => "campaign:"+campaignId.ToString("N")+":"+digest;
    public static string Prefix(Guid campaignId,string digest) => "rev:"+campaignId.ToString("N")+":"
        +Convert.ToBase64String(Convert.FromHexString(digest)).TrimEnd('=').Replace('+','-').Replace('/','_')+":";
    public static string Key(Guid campaignId,string digest,Guid reviewId) => Prefix(campaignId,digest)+reviewId.ToString("N");
    public static bool Matches(string key,Guid campaignId,string digest) => key==OriginalKey(campaignId,digest)
        || (key.StartsWith(Prefix(campaignId,digest),StringComparison.Ordinal)
            && Guid.TryParseExact(key[Prefix(campaignId,digest).Length..],"N",out var review) && review!=Guid.Empty);
    public static bool TryCampaign(string key,out Guid campaignId)
    {
        campaignId=Guid.Empty;
        var parts=key.Split(':');
        return (parts.Length==3 && parts[0]=="campaign" || parts.Length==4 && parts[0]=="rev")
            && Guid.TryParseExact(parts[1],"N",out campaignId) && campaignId!=Guid.Empty;
    }
    public static IQueryable<AcademyProductionBrief> ForCampaign(IQueryable<AcademyProductionBrief> rows,Guid campaignId,string digest)
    {
        var original=OriginalKey(campaignId,digest);var prefix=Prefix(campaignId,digest);
        return rows.Where(x=>x.IdempotencyKey==original || x.IdempotencyKey.StartsWith(prefix))
            .OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id);
    }
}
