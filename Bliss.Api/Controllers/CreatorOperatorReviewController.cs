using System.Security.Claims;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

// Registration approval is distinct from operator-observed channel control.
// Neither operation grants issuer roles, campaign or financial permissions.
[ApiController]
[Authorize]
[Route("api/creators/operator-review")]
public sealed class CreatorOperatorReviewController(
    BlissDbContext db, BlissAuthenticationOptions auth,
    ILogger<CreatorOperatorReviewController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Queue(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TryOperator(out _)) return StatusCode(403);
        var rows = await (from m in db.CreatorAccountMemberships.AsNoTracking()
                          join r in db.CreatorRegistrationReceipts.AsNoTracking()
                              on new { m.Subject, m.CreatorId } equals new { r.Subject, r.CreatorId }
                          join c in db.Creators.AsNoTracking() on m.CreatorId equals c.Id
                          where m.Status == "PENDING_REVIEW" || (auth.CreatorAuthorityVerificationRequested
                              && m.Status == "ACTIVE" && c.AuthorityStatus == "UNVERIFIED")
                          orderby r.CreatedAt, r.Id
                          select new { receiptId = r.Id, creatorId = c.Id,
                              displayName = c.Name, countryCode = c.CountryCode,
                              channelUrl = m.SubmittedChannelUrl, status = m.Status,
                              authorityStatus = c.AuthorityStatus, createdAt = r.CreatedAt,
                              processingConsent = r.ProcessingConsent, termsAccepted = r.TermsAccepted,
                              privacyNoticeVersion = r.PrivacyNoticeVersion, termsVersion = r.TermsVersion })
            .Take(50).ToArrayAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Approve([FromBody] CreatorOperatorApproval request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!TryOperator(out var operatorSubject)) return StatusCode(403);
        if (request.ReceiptId == Guid.Empty || request.CreatorId == Guid.Empty
            || !request.ProfileReviewed || !request.ConsentReviewed)
            return BadRequest(new { error = "Exact receipt, profile and human consent review required." });
        var count = await db.CreatorAccountMemberships
            .Where(m => m.CreatorId == request.CreatorId && m.Subject != operatorSubject
                && m.Status == "PENDING_REVIEW" && m.Creator.AuthorityStatus == "UNVERIFIED"
                && db.CreatorRegistrationReceipts.Any(r => r.Id == request.ReceiptId
                    && r.Subject == m.Subject && r.CreatorId == m.CreatorId
                    && r.ProcessingConsent && r.TermsAccepted
                    && r.PrivacyNoticeVersion == CreatorRegistrationController.PrivacyNoticeVersion
                    && r.TermsVersion == CreatorRegistrationController.TermsVersion))
            .ExecuteUpdateAsync(change => change.SetProperty(m => m.Status, "ACTIVE"), cancellationToken);
        if (count != 1) return Conflict(new { error = "Matching pending creator registration with current consent not found." });
        logger.LogInformation("Creator registration review approved: ReceiptId={ReceiptId} CreatorId={CreatorId} OperatorSubject={OperatorSubject}",
            request.ReceiptId, request.CreatorId, operatorSubject);
        return Ok(new { receiptId = request.ReceiptId, creatorId = request.CreatorId,
            status = "ACTIVE", authorityStatus = "UNVERIFIED", workspaceAuthorized = false });
    }

    [HttpPost("verify-channel")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> VerifyChannel([FromBody] CreatorChannelVerification request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorAuthorityVerificationRequested || !TryOperator(out var operatorSubject)) return StatusCode(403);
        if (request.ReceiptId == Guid.Empty || request.CreatorId == Guid.Empty
            || !request.ChannelControlReviewed || !request.ConsentReviewed
            || string.IsNullOrWhiteSpace(request.ObservedCode) || request.ObservedCode.Length != 38)
            return BadRequest(new { error = "Independent channel control observation, exact proof and consent review required." });

        // Never fetch or follow an applicant-supplied URL from the server.
        // The approved human operator independently observes the proof on the
        // real submitted channel, not a screenshot or a supplied third-party link.
        var candidate = await (from m in db.CreatorAccountMemberships.AsNoTracking()
                              join r in db.CreatorRegistrationReceipts.AsNoTracking()
                                  on new { m.Subject, m.CreatorId } equals new { r.Subject, r.CreatorId }
                              where m.CreatorId == request.CreatorId && r.Id == request.ReceiptId
                                  && m.Subject != operatorSubject && m.Status == "ACTIVE"
                                  && m.Creator.AuthorityStatus == "UNVERIFIED"
                                  && r.ProcessingConsent && r.TermsAccepted
                                  && r.PrivacyNoticeVersion == CreatorRegistrationController.PrivacyNoticeVersion
                                  && r.TermsVersion == CreatorRegistrationController.TermsVersion
                              select new { m.Subject, m.SubmittedChannelUrl, r.RequestKey })
            .SingleOrDefaultAsync(cancellationToken);
        var now = DateTime.UtcNow;
        if (candidate is null || !CreatorChannelProof.Matches(request.ObservedCode, request.ReceiptId,
                candidate.RequestKey, request.CreatorId, candidate.SubmittedChannelUrl, now))
            return Conflict(new { error = "Matching approved application and current channel proof not found." });

        // Keep the existing receipt immutable. Recheck membership, exact channel
        // and receipt at the write boundary; no issuer claims are created.
        var count = await db.Creators.Where(c => c.Id == request.CreatorId && c.AuthorityStatus == "UNVERIFIED"
            && db.CreatorAccountMemberships.Any(m => m.CreatorId == c.Id && m.Subject == candidate.Subject
                && m.Subject != operatorSubject && m.Status == "ACTIVE" && m.SubmittedChannelUrl == candidate.SubmittedChannelUrl
                && db.CreatorRegistrationReceipts.Any(r => r.Id == request.ReceiptId && r.Subject == m.Subject
                    && r.CreatorId == c.Id && r.RequestKey == candidate.RequestKey && r.ProcessingConsent && r.TermsAccepted
                    && r.PrivacyNoticeVersion == CreatorRegistrationController.PrivacyNoticeVersion
                    && r.TermsVersion == CreatorRegistrationController.TermsVersion)))
            .ExecuteUpdateAsync(change => change.SetProperty(c => c.AuthorityStatus, "VERIFIED")
                .SetProperty(c => c.UpdatedAt, now), cancellationToken);
        if (count != 1) return Conflict(new { error = "Application state changed; refresh and review again." });
        logger.LogInformation("Creator channel control verified: ReceiptId={ReceiptId} CreatorId={CreatorId} OperatorSubject={OperatorSubject}",
            request.ReceiptId, request.CreatorId, operatorSubject);
        return Ok(new { receiptId = request.ReceiptId, creatorId = request.CreatorId, status = "ACTIVE",
            authorityStatus = "VERIFIED", workspaceAuthorized = true, commercialAuthorized = false });
    }

    private bool TryOperator(out string subject)
    {
        subject = string.Empty;
        if (!auth.Enabled || !auth.SupabaseEnabled || !auth.CreatorOperatorReviewRequested
            || User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(auth.Authority)
            || string.IsNullOrWhiteSpace(auth.RoleClaimType)
            || User.Claims.Any(c => string.Equals(c.Type, BlissAuthenticationOptions.CreatorRegistrationPendingMarkerClaimType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType, StringComparison.OrdinalIgnoreCase)
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

public sealed record CreatorOperatorApproval(Guid ReceiptId, Guid CreatorId, bool ProfileReviewed, bool ConsentReviewed);

public sealed record CreatorChannelVerification(Guid ReceiptId, Guid CreatorId, string ObservedCode, bool ChannelControlReviewed, bool ConsentReviewed);
