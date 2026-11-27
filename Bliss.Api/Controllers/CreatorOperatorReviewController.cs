using System.Security.Claims;
using Bliss.Api.Operations;
using Bliss.Domain.Entities;
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

    // An unavailable record may be a new draft, a withdrawal, or older inventory.
    // No automatic restoration: the operator must review the exact current slot,
    // independently confirm current creator permission and commercial rights.
    [HttpGet("inventory")]
    public async Task<IActionResult> InventoryQueue(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorInventoryOperatorReviewRequested || !auth.CreatorInventoryReadbackRequested
            || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject)) return StatusCode(403);
        var rows = await (from s in db.AdInventorySlots.AsNoTracking()
            join m in db.CreatorAccountMemberships.AsNoTracking() on s.ContentItem.CreatorId equals m.CreatorId
            join r in db.CreatorRegistrationReceipts.AsNoTracking() on new { m.Subject, m.CreatorId } equals new { r.Subject, r.CreatorId }
            where !s.IsAvailable && m.Subject != subject && m.Status == "ACTIVE"
                && m.Creator.AuthorityStatus == "VERIFIED" && r.ProcessingConsent && r.TermsAccepted
                && r.PrivacyNoticeVersion == CreatorRegistrationController.PrivacyNoticeVersion
                && r.TermsVersion == CreatorRegistrationController.TermsVersion
            orderby s.Id
            select new { slotId = s.Id, contentItemId = s.ContentItemId, creatorId = m.CreatorId,
                receiptId = r.Id, creatorName = m.Creator.Name, channelUrl = m.SubmittedChannelUrl,
                contentTitle = s.ContentItem.Title, contentType = s.ContentItem.ContentType,
                s.SlotType, s.StartSecond, s.DurationSeconds, s.IsAvailable }).Take(50).ToArrayAsync(ct);
        return Ok(new { items = rows, bookingAuthorized = false, paymentAuthorized = false, deliveryAuthorized = false });
    }

    [HttpPost("inventory")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> ReviewInventory([FromBody] CreatorInventoryReviewRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorInventoryOperatorReviewRequested || !auth.CreatorInventoryReadbackRequested
            || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject)) return StatusCode(403);
        if (request.SlotId == Guid.Empty || request.CreatorId == Guid.Empty || request.ContentItemId == Guid.Empty
            || request.ReceiptId == Guid.Empty || !request.CreatorPermissionReviewed || !request.RightsReviewed
            || !request.InventoryFitReviewed || !request.CurrentConsentReviewed
            || request.StartSecond is null or < 0 or > 86400 || request.DurationSeconds is null or < 1 or > 3600
            || request.SlotType is not ("PRE_ROLL" or "MID_ROLL" or "POST_ROLL" or "LOWER_THIRD"
                or "PERIMETER_OVERLAY" or "CORNER_OVERLAY" or "ROTATING_OVERLAY" or "SPONSORED_SEGMENT")
            || string.IsNullOrWhiteSpace(request.ContentTitle) || request.ContentTitle.Length > 512
            || string.IsNullOrWhiteSpace(request.ChannelUrl) || request.ChannelUrl.Length > 1024)
            return BadRequest(new { error = "Exact slot details and independent human review required." });
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var slot = await db.AdInventorySlots.SingleOrDefaultAsync(s => s.Id == request.SlotId
                && !s.IsAvailable && s.ContentItemId == request.ContentItemId
                && s.ContentItem.CreatorId == request.CreatorId && s.ContentItem.Creator.AuthorityStatus == "VERIFIED"
                && s.ContentItem.Title == request.ContentTitle && s.SlotType == request.SlotType
                && s.StartSecond == request.StartSecond && s.DurationSeconds == request.DurationSeconds
                && db.CreatorAccountMemberships.Any(m => m.CreatorId == request.CreatorId
                    && m.Subject != subject && m.Status == "ACTIVE" && m.SubmittedChannelUrl == request.ChannelUrl
                    && db.CreatorRegistrationReceipts.Any(r => r.Id == request.ReceiptId
                        && r.CreatorId == m.CreatorId && r.Subject == m.Subject && r.ProcessingConsent && r.TermsAccepted
                        && r.PrivacyNoticeVersion == CreatorRegistrationController.PrivacyNoticeVersion
                        && r.TermsVersion == CreatorRegistrationController.TermsVersion)), ct);
            if (slot is null) return Conflict(new { error = "Slot or creator authority changed. Refresh and review again." });
            slot.IsAvailable = true;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            logger.LogInformation("Creator inventory availability reviewed: SlotId={SlotId} CreatorId={CreatorId} ReceiptId={ReceiptId} OperatorSubject={OperatorSubject}",
                request.SlotId, request.CreatorId, request.ReceiptId, subject);
            return Ok(new { slotId = slot.Id, isAvailable = true, existingPlacementsChanged = false,
                bookingAuthorized = false, paymentAuthorized = false, deliveryAuthorized = false });
        }
        catch (Npgsql.PostgresException pg) when (pg.SqlState == Npgsql.PostgresErrorCodes.SerializationFailure) { return Conflict(); }
        catch (DbUpdateException) { return Conflict(); }
    }

    [HttpGet("campaign-invitation")]
    public async Task<IActionResult> PreviewInvitation([FromQuery] Guid placementId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorCampaignInvitationRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out _))
            return StatusCode(403);
        if (placementId == Guid.Empty) return BadRequest();
        var row = await CreatorCampaignInvitation.Current(db, placementId, ct);
        if (row is null) return NotFound();
        return Ok(new { placement = row, digest = CreatorCampaignInvitation.Digest(row), bookingAuthorized = false,
            paymentAuthorized = false, deliveryAuthorized = false });
    }

    [HttpPost("campaign-invitation")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> IssueInvitation([FromBody] IssueCreatorInvitation request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorCampaignInvitationRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject))
            return StatusCode(403);
        if (request.PlacementId == Guid.Empty || request.Digest is not { Length: 64 }
            || !request.AdvertiserPermissionReviewed || !request.CreatorContactPermissionReviewed || !request.PlacementReviewed)
            return BadRequest();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var row = await CreatorCampaignInvitation.Current(db, request.PlacementId, ct);
            if (row is null || CreatorCampaignInvitation.Digest(row) != request.Digest) return Conflict();
            if (await db.CreatorAccountMemberships.AnyAsync(m => m.CreatorId == row.CreatorId && m.Subject == subject, ct)) return StatusCode(403);
            var id = CreatorCampaignInvitation.InvitationId(row.PlacementId);
            var prior = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, ct);
            if (prior is not null && !CreatorCampaignInvitation.Matches(prior,row)) return Conflict();
            if (prior is null)
            {
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent { Id = id, AdvertiserId = row.AdvertiserId,
                    WorkspaceId = row.WorkspaceId, Action = CreatorCampaignInvitation.Action, ActorType = "OPERATOR",
                    ActorLabel = subject, Outcome = "INVITED", RequestId = row.CreatorId.ToString("N"),
                    Detail = CreatorCampaignInvitation.Detail(row), OccurredAt = DateTime.UtcNow });
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return StatusCode(prior is null ? 201 : 200, new { invitationId = id, placementId = row.PlacementId,
                invitationRecorded = true, bookingAuthorized = false, paymentAuthorized = false, deliveryAuthorized = false });
        }
        catch (Npgsql.PostgresException pg) when (pg.SqlState == Npgsql.PostgresErrorCodes.SerializationFailure) { return Conflict(); }
        catch (DbUpdateException) { return Conflict(); }
    }

    [HttpGet("compensation-proposal")]
    public async Task<IActionResult> PreviewCompensation([FromQuery] Guid invitationId,[FromQuery] Guid illustrationId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorCampaignInvitationRequested || !auth.CreatorCompensationProposalRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out _)) return StatusCode(403);
        if(invitationId==Guid.Empty || illustrationId==Guid.Empty) return BadRequest();
        var row=await CreatorCompensationProposal.Current(db,invitationId,illustrationId,ct);
        return row is null ? NotFound() : Ok(new {proposal=row,digest=CreatorCompensationProposal.Digest(row),paymentAuthorized=false,deliveryAuthorized=false});
    }
    [HttpPost("compensation-proposal")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> PublishCompensation([FromBody] PublishCreatorCompensation request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorCampaignInvitationRequested || !auth.CreatorCompensationProposalRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject)) return StatusCode(403);
        if(request.InvitationId==Guid.Empty || request.IllustrationId==Guid.Empty || request.Digest is not {Length:64}
            || !request.AllocationReviewed || !request.PermissionToShareReviewed) return BadRequest();
        try {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await CreatorCompensationProposal.Current(db,request.InvitationId,request.IllustrationId,ct);
            if(row is null || CreatorCompensationProposal.Digest(row)!=request.Digest) return Conflict();
            if(await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==row.CreatorId && m.Subject==subject,ct))return StatusCode(403);
            var id=CreatorCompensationProposal.Id(row.InvitationId);
            var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
            if(prior is not null && !CreatorCompensationProposal.Matches(prior,row)) return Conflict();
            if(prior is null){
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent {Id=id,AdvertiserId=row.AdvertiserId,WorkspaceId=row.WorkspaceId,
                    Action=CreatorCompensationProposal.Action,ActorType="OPERATOR",ActorLabel=subject,Outcome="PROPOSED",
                    RequestId=row.CreatorId.ToString("N"),Detail=CreatorCompensationProposal.Detail(row),OccurredAt=DateTime.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new {proposalId=id,invitationId=row.InvitationId,proposalRecorded=true,paymentAuthorized=false,deliveryAuthorized=false});
        } catch(Npgsql.PostgresException pg) when(pg.SqlState==Npgsql.PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
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

public sealed record CreatorInventoryReviewRequest(Guid SlotId, Guid CreatorId, Guid ContentItemId, Guid ReceiptId,
    string ContentTitle, string ChannelUrl, string SlotType, int? StartSecond, int? DurationSeconds,
    bool CreatorPermissionReviewed, bool RightsReviewed, bool InventoryFitReviewed, bool CurrentConsentReviewed);

public sealed record IssueCreatorInvitation(Guid PlacementId, string Digest, bool AdvertiserPermissionReviewed, bool CreatorContactPermissionReviewed, bool PlacementReviewed);

public sealed record PublishCreatorCompensation(Guid InvitationId,Guid IllustrationId,string Digest,bool AllocationReviewed,bool PermissionToShareReviewed);
