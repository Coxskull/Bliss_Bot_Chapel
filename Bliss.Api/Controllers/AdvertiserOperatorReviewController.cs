using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage;
using Bliss.Domain.Entities;
using Npgsql;
using System.Security.Claims;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

// Human business review. The optional membership-backed profile lane consumes
// the approved membership directly; this controller never writes issuer claims.
[ApiController]
[Authorize]
[Route("api/advertisers/operator-review")]
public sealed class AdvertiserOperatorReviewController(
    BlissDbContext db, BlissAuthenticationOptions auth,
    ILogger<AdvertiserOperatorReviewController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Queue(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TryOperator(out _)) return StatusCode(403);
        var rows = await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
                          join r in db.AdvertiserRegistrationReceipts.AsNoTracking()
                              on new { m.Subject, m.AdvertiserId } equals new { r.Subject, r.AdvertiserId }
                          join b in db.Advertisers.AsNoTracking() on m.AdvertiserId equals b.Id
                          where m.Status == "PENDING_REVIEW"
                          orderby r.CreatedAt, r.Id
                          select new { receiptId = r.Id, advertiserId = b.Id, subject = m.Subject,
                              businessName = b.Name, countryCode = b.CountryCode, website = b.Website,
                              status = m.Status, createdAt = r.CreatedAt, processingConsent = r.ProcessingConsent,
                              termsAccepted = r.TermsAccepted, privacyNoticeVersion = r.PrivacyNoticeVersion,
                              termsVersion = r.TermsVersion })
            .Take(50).ToArrayAsync(cancellationToken);
        return Ok(rows);
    }

    // Receipt evidence is staff-only, even when generic authentication is disabled.
    // No browser-supplied data can mark a payment verified or authorize fulfillment.
    [HttpGet("starter-receipts")]
    public async Task<IActionResult> StarterReceipts(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TryOperator(out _)) return StatusCode(403);
        var rows = new List<object>();
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandTimeout = 10;
                cmd.CommandText = """
                    SELECT session_id, payment_intent_id, quote_reference, amount_cents, currency, received_at
                    FROM public.alpha_starter_stripe_receipts
                    ORDER BY received_at DESC, event_id DESC LIMIT 50
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    rows.Add(new { checkoutSessionId = reader.GetString(0), paymentIntentId = reader.GetString(1),
                        quoteReference = reader.IsDBNull(2) ? null : reader.GetString(2),
                        amountCents = reader.GetInt64(3), currency = reader.GetString(4),
                        receivedAt = reader.GetFieldValue<DateTime>(5),
                        status = "SIGNED_NOTIFICATION_PENDING_ORDER_RECONCILIATION" });
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        catch (Exception e) when (e is System.Data.Common.DbException or InvalidOperationException or TimeoutException)
        { return StatusCode(503, new { error = "Payment receipt storage unavailable." }); }
        return Ok(new { receipts = rows, fulfillmentAuthorized = false });
    }

    public const string StarterReconciliationAction = "STARTER_PAYMENT_RECONCILED";
    public static bool ExactQuoteReference(string? reference, Guid versionId) =>
        versionId != Guid.Empty && reference is not null
        && (Guid.TryParseExact(reference, "D", out var value) || Guid.TryParseExact(reference, "N", out value))
        && value == versionId;

    // One signed receipt can be reconciled to one current accepted quote.
    // The public client_reference_id is not payer authentication. The operator
    // must independently check the live Stripe merchant, payer/order and refunds.
    [HttpPost("starter-reconcile")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Reconcile([FromBody] StarterOrderReconciliation request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.StarterPaymentReconciliationRequested || !TryOperator(out var subject)) return StatusCode(403);
        if (request.QuoteVersionId == Guid.Empty || request.CampaignId == Guid.Empty
            || request.CheckoutSessionId is null || request.CheckoutSessionId.Length > 128
            || !request.CheckoutSessionId.StartsWith("cs_live_", StringComparison.Ordinal)
            || !request.CurrentMerchantPaymentVerified || !request.PayerAndOrderVerified
            || !request.NoRefundOrDisputeVerified || !request.PaidServiceScopeVerified)
            return BadRequest(new { error = "Exact accepted quote, campaign and current Stripe/order checks are required." });
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Serialize reconciliation on the actual signed notification row.
            // Reuse the existing receipt schema and append-only workspace audit.
            string? reference;
            string eventId, intentId;
            await using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.Transaction = tx.GetDbTransaction(); cmd.CommandTimeout = 10;
                cmd.CommandText = """
                    SELECT event_id, payment_intent_id, quote_reference
                    FROM public.alpha_starter_stripe_receipts
                    WHERE session_id = @session AND amount_cents = 14900 AND currency = 'usd'
                    FOR UPDATE
                    """;
                var param = cmd.CreateParameter(); param.ParameterName = "session"; param.DbType = DbType.String;
                param.Value = request.CheckoutSessionId; cmd.Parameters.Add(param);
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return NotFound(new { error = "Signed paid notification not found." });
                eventId = reader.GetString(0); intentId = reader.GetString(1);
                reference = reader.IsDBNull(2) ? null : reader.GetString(2);
            }
            if (!ExactQuoteReference(reference, request.QuoteVersionId))
                return Conflict(new { error = "Signed receipt does not identify this exact immutable quote version." });
            var quote = await (from v in db.QuoteVersions.AsNoTracking()
                join q in db.Quotes.AsNoTracking() on v.QuoteId equals q.Id
                join campaign in db.Campaigns.AsNoTracking() on q.AdvertiserOpportunityId equals (Guid?)campaign.AdvertiserOpportunityId
                where v.Id == request.QuoteVersionId && campaign.Id == request.CampaignId
                    && q.Status == "ACCEPTED" && q.CurrentVersionNumber == v.VersionNumber
                    && v.TotalAmount == 149m && v.CurrencyCode.ToUpper() == "USD"
                    && q.AdvertiserOpportunity != null
                select new { QuoteId = q.Id, VersionId = v.Id,
                    AdvertiserId = q.AdvertiserOpportunity!.AdvertiserProgram.AdvertiserId }).SingleOrDefaultAsync(ct);
            if (quote is null) return Conflict(new { error = "Current accepted USD 149 quote is not bound to the campaign." });
            var approval = await db.QuoteApprovalDecisions.AsNoTracking().Where(x => x.QuoteId == quote.QuoteId)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            var outcome = await db.QuoteOutcomes.AsNoTracking().Where(x => x.QuoteId == quote.QuoteId)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (approval?.QuoteVersionId != quote.VersionId || approval.Decision != "APPROVED"
                || outcome?.QuoteVersionId != quote.VersionId || outcome.Response != "ACCEPTED"
                || outcome.Amount != 149m || !string.Equals(outcome.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Latest approval and customer acceptance of the exact quote are required." });
            var workspace = await db.WeddingPlannerWorkspaces.AsNoTracking().Where(x => x.AdvertiserId == quote.AdvertiserId
                && x.IsPrimary && x.Status == "ACTIVE").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (workspace is null) return Conflict(new { error = "An active primary advertiser workspace is required." });
            var active = await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
                join receipt in db.AdvertiserRegistrationReceipts.AsNoTracking()
                    on new { m.Subject, m.AdvertiserId } equals new { receipt.Subject, receipt.AdvertiserId }
                where m.AdvertiserId == quote.AdvertiserId && m.Status == "ACTIVE"
                    && receipt.ProcessingConsent && receipt.TermsAccepted
                    && receipt.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                    && receipt.TermsVersion == AdvertiserRegistrationController.TermsVersion
                select m.Id).AnyAsync(ct);
            if (!active) return StatusCode(403);
            var receiptDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.CheckoutSessionId + "\n" + intentId))).ToLowerInvariant();
            var receiptKey = new Guid(Convert.FromHexString(receiptDigest[..32]));
            var prefix = "quote:" + quote.VersionId.ToString("N") + ":";
            var detail = prefix + request.CampaignId.ToString("N") + ":" + receiptDigest;
            var prior = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receiptKey, ct);
            if (prior is not null && (prior.AdvertiserId != quote.AdvertiserId || prior.WorkspaceId != workspace
                || prior.Action != StarterReconciliationAction || prior.Detail != detail || prior.Outcome != "RECONCILED"))
                return Conflict(new { error = "Payment receipt already belongs to another reconciliation." });
            if (prior is null)
            {
                if (await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == StarterReconciliationAction
                    && x.Detail != null && x.Detail.StartsWith(prefix), ct))
                    return Conflict(new { error = "Quote already has a reconciled payment. Investigate duplicate payment separately." });
                prior = new WeddingPlannerAuditEvent { Id = receiptKey, AdvertiserId = quote.AdvertiserId, WorkspaceId = workspace,
                    Action = StarterReconciliationAction, ActorType = "OPERATOR", ActorLabel = subject,
                    Outcome = "RECONCILED", Detail = detail, RequestId = eventId, OccurredAt = DateTime.UtcNow };
                db.WeddingPlannerAuditEvents.Add(prior);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new { reconciliationId = prior.Id, campaignId = request.CampaignId, quoteVersionId = quote.VersionId,
                status = "RECONCILED_BY_OPERATOR", amountCents = 14900, currency = "usd", recordedAt = prior.OccurredAt,
                fulfillmentAuthorized = false, delivery = "NOT_SENT", release = "HELD" });
        }
        catch (PostgresException pg) when (pg.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        { return Conflict(new { error = "Reconciliation raced another operation. Refresh before retrying." }); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && pg.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.UniqueViolation)
        { return Conflict(new { error = "Reconciliation raced another operation. Refresh before retrying." }); }
        catch (Exception ex) when (ex is System.Data.Common.DbException or TimeoutException)
        { return StatusCode(503, new { error = "Payment reconciliation storage unavailable." }); }
    }

    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Approve([FromBody] AdvertiserOperatorApproval request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TryOperator(out var operatorSubject)) return StatusCode(403);
        if (request.ReceiptId == Guid.Empty || request.AdvertiserId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.Subject) || request.Subject == operatorSubject
            || !request.BusinessIdentityReviewed || !request.ConsentReviewed)
            return BadRequest(new { error = "Exact receipt, business identity and human consent review required." });

        // Match all three identifiers and the original accepted notice versions.
        // Conditional SQL update is atomic: repeated or stale approvals cannot
        // activate another membership or rewrite an existing registration receipt.
        var count = await db.AdvertiserAccountMemberships
            .Where(m => m.Subject == request.Subject && m.AdvertiserId == request.AdvertiserId
                && m.Status == "PENDING_REVIEW"
                && db.AdvertiserRegistrationReceipts.Any(r => r.Id == request.ReceiptId
                    && r.Subject == m.Subject && r.AdvertiserId == m.AdvertiserId
                    && r.ProcessingConsent && r.TermsAccepted
                    && r.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                    && r.TermsVersion == AdvertiserRegistrationController.TermsVersion))
            .ExecuteUpdateAsync(change => change.SetProperty(m => m.Status, "ACTIVE"), cancellationToken);
        if (count != 1) return Conflict(new { error = "Matching pending registration with current consent not found." });
        logger.LogInformation("Advertiser membership review approved: ReceiptId={ReceiptId} AdvertiserId={AdvertiserId} OperatorSubject={OperatorSubject}",
            request.ReceiptId, request.AdvertiserId, operatorSubject);
        return Ok(new { receiptId = request.ReceiptId, advertiserId = request.AdvertiserId,
            status = "ACTIVE", identityProvisioning = auth.AdvertiserMembershipSelfServiceRequested
                && auth.AdvertiserSelfServiceRequested
                && !auth.ApprovedTestSubjects.Contains(request.Subject, StringComparer.Ordinal)
                && !auth.ApprovedCreatorSubjects.Contains(request.Subject, StringComparer.Ordinal)
                && !auth.ApprovedAdvertiserSubjects.Contains(request.Subject, StringComparer.Ordinal)
                && !auth.ApprovedRegistrationOperatorSubjects.Contains(request.Subject, StringComparer.Ordinal)
                && !auth.ApprovedPrivacyOperatorSubjects.Contains(request.Subject, StringComparer.Ordinal)
                ? "MEMBERSHIP_PROFILE_ENABLED" : "REQUIRED_SEPARATELY" });
    }

    private bool TryOperator(out string subject)
    {
        subject = string.Empty;
        if (!auth.Enabled || !auth.SupabaseEnabled || !auth.AdvertiserOperatorReviewRequested
            || User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(auth.Authority)
            || string.IsNullOrWhiteSpace(auth.RoleClaimType)
            || User.Claims.Any(c => string.Equals(c.Type, BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, BlissAuthenticationOptions.AdvertiserSelfServiceMarkerClaimType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, auth.AdvertiserIdClaimType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, auth.CreatorIdClaimType, StringComparison.OrdinalIgnoreCase))) return false;
        var subjects = User.Claims.Where(c => string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (subjects.Length != 1 || subjects[0].Type != "sub"
            || !auth.ApprovedRegistrationOperatorSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)
            || !auth.ApprovedTestSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)
            || auth.ApprovedAdvertiserSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)
            || auth.ApprovedCreatorSubjects.Contains(subjects[0].Value, StringComparer.Ordinal)) return false;
        var roles = User.Claims.Where(c => string.Equals(c.Type, auth.RoleClaimType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (roles.Length != 1 || roles[0].Type != auth.RoleClaimType
            || roles[0].Issuer != auth.Authority || roles[0].OriginalIssuer != auth.Authority
            || (roles[0].Value != auth.OperatorRole && roles[0].Value != auth.AdminRole)
            || User.Claims.Any(c => string.Equals(c.Type, auth.ReviewerWorkspaceClaimType, StringComparison.OrdinalIgnoreCase))) return false;
        subject = subjects[0].Value;
        return !string.IsNullOrWhiteSpace(subject);
    }
}

public sealed record AdvertiserOperatorApproval(Guid ReceiptId, Guid AdvertiserId, string Subject,
    bool BusinessIdentityReviewed, bool ConsentReviewed);

public sealed record StarterOrderReconciliation(Guid CampaignId, Guid QuoteVersionId, string CheckoutSessionId,
    bool CurrentMerchantPaymentVerified, bool PayerAndOrderVerified, bool NoRefundOrDisputeVerified, bool PaidServiceScopeVerified);
