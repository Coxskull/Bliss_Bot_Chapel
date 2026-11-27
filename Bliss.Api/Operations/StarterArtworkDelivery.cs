using Bliss.Api.Controllers;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Bliss.Api.Operations;

public static class StarterArtworkDelivery
{
    public const string ReleaseAction = "STARTER_ARTWORK_RELEASED";
    public const string DownloadAction = "STARTER_ARTWORK_DOWNLOAD_REQUESTED";
    public static string Suffix(Guid campaignId, Guid candidateId, Guid reviewId, Guid approvalId, string hash) =>
        $":{campaignId:N}:{candidateId:N}:{reviewId:N}:{approvalId:N}:{hash}";
    public static Guid StableId(string action, string value) =>
        new Guid(Convert.FromHexString(ControlledAiService.Hash(action + "\n" + value)[..32]));
    public static Guid DownloadRequestId(Guid releaseId, string subject) =>
        StableId(DownloadAction, releaseId.ToString("N") + ":" + subject);

    // A request is not proof of completed transfer or saving the PNG.
    public static bool MatchesDownloadRequest(WeddingPlannerAuditEvent row, Guid advertiserId,
        Guid workspaceId, Guid releaseId, string subject) =>
        row.Id == DownloadRequestId(releaseId, subject) && row.AdvertiserId == advertiserId
        && row.WorkspaceId == workspaceId && row.Action == DownloadAction
        && row.ActorType == "ADVERTISER" && row.ActorLabel == subject
        && row.Outcome == "REQUESTED" && row.Detail == releaseId.ToString("N");

    public const string AcknowledgeAction = "STARTER_ARTWORK_RECEIPT_ACKNOWLEDGED";
    public static Guid AcknowledgmentId(Guid releaseId, string subject) =>
        StableId(AcknowledgeAction, releaseId.ToString("N") + ":" + subject);

    public static bool MatchesAcknowledgment(WeddingPlannerAuditEvent row, Guid advertiserId,
        Guid workspaceId, Guid releaseId, string subject, string imageSha256) =>
        row.Id == AcknowledgmentId(releaseId, subject) && row.AdvertiserId == advertiserId
        && row.WorkspaceId == workspaceId && row.Action == AcknowledgeAction
        && row.ActorType == "ADVERTISER" && row.ActorLabel == subject
        && row.Outcome == "ACKNOWLEDGED" && row.Detail == $"{releaseId:N}:{imageSha256}";

    // Historical signed notification evidence is necessary but is not a live
    // Stripe refund/dispute check or independent fulfillment authorization.
    public static bool MatchesSignedReceipt(WeddingPlannerAuditEvent audit, Guid versionId,
        Guid campaignId, StarterStripeReceipt receipt)
    {
        if (versionId == Guid.Empty || campaignId == Guid.Empty
            || audit.Action != AdvertiserOperatorReviewController.StarterReconciliationAction
            || audit.ActorType != "OPERATOR" || audit.Outcome != "RECONCILED"
            || audit.RequestId != receipt.EventId || !receipt.EventId.StartsWith("evt_", StringComparison.Ordinal)
            || !receipt.SessionId.StartsWith("cs_live_", StringComparison.Ordinal)
            || !receipt.PaymentIntentId.StartsWith("pi_", StringComparison.Ordinal)
            || receipt.AmountCents != 14900 || receipt.Currency != "usd"
            || receipt.EventType is not ("checkout.session.completed" or "checkout.session.async_payment_succeeded")
            || !AdvertiserOperatorReviewController.ExactQuoteReference(receipt.QuoteReference, versionId))
            return false;
        var digest = ControlledAiService.Hash(receipt.SessionId + "\n" + receipt.PaymentIntentId);
        var key = new Guid(Convert.FromHexString(digest[..32]));
        return audit.Id == key && audit.Detail == $"quote:{versionId:N}:{campaignId:N}:{digest}";
    }

    private static async Task<StarterStripeReceipt?> FindSignedReceipt(BlissDbContext db, string eventId, CancellationToken ct)
    {
        // Both release and download use a single database transaction. Do not
        // fall back to a second connection or an unbound historical read.
        var tx = db.Database.CurrentTransaction;
        if (tx is null) return null;
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = tx.GetDbTransaction();
        cmd.CommandTimeout = 10;
        cmd.CommandText = """
            SELECT event_id, session_id, payment_intent_id, quote_reference, amount_cents, currency, event_type
            FROM public.alpha_starter_stripe_receipts WHERE event_id = @event
            """;
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = "event";
        parameter.DbType = DbType.String;
        parameter.Value = eventId;
        cmd.Parameters.Add(parameter);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new StarterStripeReceipt(reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetInt64(4), reader.GetString(5), reader.GetString(6));
    }

    public static async Task<WeddingPlannerAuditEvent?> Reconciliation(BlissDbContext db, Guid advertiserId,
        Guid workspaceId, Guid campaignId, Guid reconciliationId, CancellationToken ct)
    {
        var row = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reconciliationId
            && x.AdvertiserId == advertiserId && x.WorkspaceId == workspaceId
            && x.Action == AdvertiserOperatorReviewController.StarterReconciliationAction
            && x.ActorType == "OPERATOR" && x.Outcome == "RECONCILED", ct);
        var parts = row?.Detail?.Split(':');
        if (row is null || string.IsNullOrWhiteSpace(row.RequestId) || parts is not { Length: 4 }
            || parts[0] != "quote" || !Guid.TryParseExact(parts[1], "N", out var versionId)
            || parts[2] != campaignId.ToString("N") || parts[3].Length != 64) return null;
        var receipt = await FindSignedReceipt(db, row.RequestId, ct);
        if (receipt is null || !MatchesSignedReceipt(row, versionId, campaignId, receipt)) return null;
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
