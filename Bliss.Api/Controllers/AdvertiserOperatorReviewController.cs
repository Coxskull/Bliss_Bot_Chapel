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
