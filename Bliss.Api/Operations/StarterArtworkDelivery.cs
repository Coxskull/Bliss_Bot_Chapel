using Bliss.Api.Controllers;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Operations;

public static class StarterArtworkDelivery
{
    public const string ReleaseAction = "STARTER_ARTWORK_RELEASED";
    public const string DownloadAction = "STARTER_ARTWORK_DOWNLOAD_REQUESTED";
    public static string Suffix(Guid campaignId, Guid candidateId, Guid reviewId, Guid approvalId, string hash) =>
        $":{campaignId:N}:{candidateId:N}:{reviewId:N}:{approvalId:N}:{hash}";
    public static Guid StableId(string action, string value) =>
        new Guid(Convert.FromHexString(ControlledAiService.Hash(action + "\n" + value)[..32]));
    public static async Task<WeddingPlannerAuditEvent?> Reconciliation(BlissDbContext db, Guid advertiserId,
        Guid workspaceId, Guid campaignId, Guid reconciliationId, CancellationToken ct)
    {
        var row = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reconciliationId
            && x.AdvertiserId == advertiserId && x.WorkspaceId == workspaceId
            && x.Action == AdvertiserOperatorReviewController.StarterReconciliationAction
            && x.ActorType == "OPERATOR" && x.Outcome == "RECONCILED", ct);
        var parts = row?.Detail?.Split(':');
        if (parts is not { Length: 4 } || parts[0] != "quote" || !Guid.TryParseExact(parts[1], "N", out var versionId)
            || parts[2] != campaignId.ToString("N") || parts[3].Length != 64) return null;
        var quote = await (from v in db.QuoteVersions.AsNoTracking()
            join q in db.Quotes.AsNoTracking() on v.QuoteId equals q.Id
            join campaign in db.Campaigns.AsNoTracking() on q.AdvertiserOpportunityId equals campaign.AdvertiserOpportunityId
            where v.Id == versionId && campaign.Id == campaignId
                && q.Status == "ACCEPTED" && q.CurrentVersionNumber == v.VersionNumber
                && v.TotalAmount == 149m && v.CurrencyCode.ToUpper() == "USD"
                && q.AdvertiserOpportunity != null
                && q.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == advertiserId
            select q.Id).SingleOrDefaultAsync(ct);
        if (quote == Guid.Empty){
            var direct=await StarterServiceOffer.CurrentAccepted(db,campaignId,versionId,ct);
            return direct is not null && direct.AdvertiserId==advertiserId && direct.WorkspaceId==workspaceId ? row : null;
        }
        var approval = await db.QuoteApprovalDecisions.AsNoTracking().Where(x => x.QuoteId == quote)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var outcome = await db.QuoteOutcomes.AsNoTracking().Where(x => x.QuoteId == quote)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        return approval?.QuoteVersionId == versionId && approval.Decision == "APPROVED"
            && outcome?.QuoteVersionId == versionId && outcome.Response == "ACCEPTED"
            && outcome.Amount == 149m && string.Equals(outcome.CurrencyCode,"USD",StringComparison.OrdinalIgnoreCase) ? row : null;
    }
}
