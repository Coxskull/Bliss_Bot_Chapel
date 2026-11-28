using Bliss.Api.Operations;
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
        var rows = new List<(string Session, string Intent, string? Reference, long Amount, string Currency, DateTime Received, string EventId)>();
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandTimeout = 10;
                cmd.CommandText = """
                    SELECT session_id, payment_intent_id, quote_reference, amount_cents, currency, received_at, event_id
                    FROM public.alpha_starter_stripe_receipts
                    ORDER BY received_at DESC, event_id DESC LIMIT 50
                    """;
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    rows.Add((reader.GetString(0), reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetInt64(3),
                        reader.GetString(4), reader.GetFieldValue<DateTime>(5), reader.GetString(6)));
            }
            finally { await db.Database.CloseConnectionAsync(); }

            // Read the existing append-only reconciliation audit in one bounded query.
            var keys = rows.Select(row => StarterReceiptKey(StarterReceiptDigest(row.Session, row.Intent))).ToArray();
            var audits = keys.Length == 0 ? new Dictionary<Guid, WeddingPlannerAuditEvent>()
                : await db.WeddingPlannerAuditEvents.AsNoTracking().Where(x => keys.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, ct);
            return Ok(new { receipts = rows.Select(row =>
            {
                var digest = StarterReceiptDigest(row.Session, row.Intent);
                audits.TryGetValue(StarterReceiptKey(digest), out var audit);
                return new { checkoutSessionId = row.Session, paymentIntentId = row.Intent,
                    quoteReference = row.Reference, amountCents = row.Amount, currency = row.Currency,
                    receivedAt = row.Received, status = StarterReceiptStatus(row.Reference, row.EventId, digest, audit) };
            }).ToArray(), fulfillmentAuthorized = false });
        }
        catch (Exception e) when (e is System.Data.Common.DbException or InvalidOperationException or TimeoutException)
        { return StatusCode(503, new { error = "Payment receipt storage unavailable." }); }
    }

    public const string StarterReconciliationAction = "STARTER_PAYMENT_RECONCILED";
    public static string StarterReceiptDigest(string session, string intent) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(session + "\n" + intent))).ToLowerInvariant();
    public static Guid StarterReceiptKey(string digest) => new(Convert.FromHexString(digest[..32]));

    public static string StarterReceiptStatus(string? reference, string eventId, string digest, WeddingPlannerAuditEvent? audit)
    {
        if (audit is null) return "SIGNED_NOTIFICATION_PENDING_ORDER_RECONCILIATION";
        var parts = audit.Detail?.Split(':');
        if (audit.Id == StarterReceiptKey(digest) && audit.Action == StarterReconciliationAction
            && audit.Outcome == "RECONCILED" && audit.RequestId == eventId
            && parts is { Length: 4 } && parts[0] == "quote"
            && Guid.TryParseExact(parts[1], "N", out var version) && ExactQuoteReference(reference, version)
            && Guid.TryParseExact(parts[2], "N", out var campaign) && campaign != Guid.Empty
            && parts[3] == digest)
            return "RECONCILED_BY_OPERATOR";
        return "RECONCILIATION_REVIEW_REQUIRED";
    }
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
            var direct=quote is null && auth.StarterServiceOfferRequested
                ? await StarterServiceOffer.CurrentAccepted(db,request.CampaignId,request.QuoteVersionId,ct) : null;
            if(quote is null && direct is null)return Conflict(new{error="Current accepted USD 149 quote or service offer is not bound to the campaign."});
            if(quote is not null){
            var approval = await db.QuoteApprovalDecisions.AsNoTracking().Where(x => x.QuoteId == quote.QuoteId)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            var outcome = await db.QuoteOutcomes.AsNoTracking().Where(x => x.QuoteId == quote.QuoteId)
                .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (approval?.QuoteVersionId != quote.VersionId || approval.Decision != "APPROVED"
                || outcome?.QuoteVersionId != quote.VersionId || outcome.Response != "ACCEPTED"
                || outcome.Amount != 149m || !string.Equals(outcome.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { error = "Latest approval and customer acceptance of the exact quote are required." });
            }
            var advertiserId=quote?.AdvertiserId ?? direct!.AdvertiserId;
            var versionId=quote?.VersionId ?? direct!.VersionId;
            var workspace = await db.WeddingPlannerWorkspaces.AsNoTracking().Where(x => x.AdvertiserId == advertiserId
                && x.IsPrimary && x.Status == "ACTIVE").Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (workspace is null) return Conflict(new { error = "An active primary advertiser workspace is required." });
            var active = await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
                join receipt in db.AdvertiserRegistrationReceipts.AsNoTracking()
                    on new { m.Subject, m.AdvertiserId } equals new { receipt.Subject, receipt.AdvertiserId }
                where m.AdvertiserId == advertiserId && m.Status == "ACTIVE"
                    && receipt.ProcessingConsent && receipt.TermsAccepted
                    && receipt.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                    && receipt.TermsVersion == AdvertiserRegistrationController.TermsVersion
                select m.Id).AnyAsync(ct);
            if (!active) return StatusCode(403);
            var receiptDigest = StarterReceiptDigest(request.CheckoutSessionId, intentId);
            var receiptKey = StarterReceiptKey(receiptDigest);
            var prefix = "quote:" + versionId.ToString("N") + ":";
            var detail = prefix + request.CampaignId.ToString("N") + ":" + receiptDigest;
            var prior = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receiptKey, ct);
            if (prior is not null && (prior.AdvertiserId != advertiserId || prior.WorkspaceId != workspace
                || prior.Action != StarterReconciliationAction || prior.Detail != detail || prior.Outcome != "RECONCILED"))
                return Conflict(new { error = "Payment receipt already belongs to another reconciliation." });
            if (prior is null)
            {
                if (await db.WeddingPlannerAuditEvents.AnyAsync(x => x.Action == StarterReconciliationAction
                    && x.Detail != null && x.Detail.StartsWith(prefix), ct))
                    return Conflict(new { error = "Quote already has a reconciled payment. Investigate duplicate payment separately." });
                prior = new WeddingPlannerAuditEvent { Id = receiptKey, AdvertiserId = advertiserId, WorkspaceId = workspace,
                    Action = StarterReconciliationAction, ActorType = "OPERATOR", ActorLabel = subject,
                    Outcome = "RECONCILED", Detail = detail, RequestId = eventId, OccurredAt = DateTime.UtcNow };
                db.WeddingPlannerAuditEvents.Add(prior);
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new { reconciliationId = prior.Id, campaignId = request.CampaignId, quoteVersionId = versionId,
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
