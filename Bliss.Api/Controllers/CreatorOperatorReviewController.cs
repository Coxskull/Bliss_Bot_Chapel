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
        if(row is null)return NotFound();
        var proposalId=CreatorCompensationProposal.Id(invitationId);
        var published=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==proposalId,ct);
        var acknowledged=published is not null && CreatorCompensationProposal.Matches(published,row)
            && await CreatorCompensationProposal.HasCurrentAcknowledgment(db,row,ct);
        return Ok(new {proposal=row,digest=CreatorCompensationProposal.Digest(row),paymentAuthorized=false,deliveryAuthorized=false,
            allocationAcknowledged=acknowledged,canRecordAgreementEvidence=auth.CreatorAgreementEvidenceRequested && acknowledged});
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
    [HttpPost("agreement-evidence")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> RecordAgreementEvidence([FromBody] RecordCreatorAgreementEvidence request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorCampaignInvitationRequested || !auth.CreatorCompensationProposalRequested
            || !auth.CreatorAgreementEvidenceRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject))return StatusCode(403);
        if(request.InvitationId==Guid.Empty || request.IllustrationId==Guid.Empty || !CreatorCompensationProposal.ValidDocumentHash(request.Digest)
            || !CreatorCompensationProposal.ValidDocumentHash(request.DocumentSha256)
            || !request.SignedAgreementReviewed || !request.PartiesAndScopeReviewed || !request.CompensationAndPaymentTermsReviewed
            || !request.CancellationAndRightsReviewed)return BadRequest();
        try {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await CreatorCompensationProposal.Current(db,request.InvitationId,request.IllustrationId,ct);
            if(row is null || CreatorCompensationProposal.Digest(row)!=request.Digest
                || !await CreatorCompensationProposal.HasCurrentAcknowledgment(db,row,ct))return Conflict();
            if(await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==row.CreatorId && m.Subject==subject,ct))return StatusCode(403);
            var proposalId=CreatorCompensationProposal.Id(row.InvitationId);
            var proposal=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==proposalId,ct);
            if(proposal is null || !CreatorCompensationProposal.Matches(proposal,row))return Conflict();
            var id=CreatorCompensationProposal.AgreementId(proposalId);
            var detail=CreatorCompensationProposal.AgreementDetail(row,request.DocumentSha256);
            var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
            if(prior is not null && (!CreatorCompensationProposal.MatchesAgreement(prior,row) || prior.Detail!=detail))return Conflict();
            if(prior is null){
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent {Id=id,AdvertiserId=row.AdvertiserId,WorkspaceId=row.WorkspaceId,
                    Action=CreatorCompensationProposal.AgreementAction,ActorType="OPERATOR",ActorLabel=subject,Outcome="EVIDENCE_RECORDED",
                    RequestId=row.CreatorId.ToString("N"),Detail=detail,OccurredAt=DateTime.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new {agreementEvidenceId=id,proposalId,agreementEvidenceRecorded=true,signatureVerifiedBySystem=false,
                payable=false,bookingAuthorized=false,paymentAuthorized=false,deliveryAuthorized=false});
        }catch(Npgsql.PostgresException pg) when(pg.SqlState==Npgsql.PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
    }
    [HttpGet("allocation-history")]
    public async Task<IActionResult> PreviewAllocationHistory([FromQuery] Guid historyId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorAllocationHistoryRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out _))return StatusCode(403);
        if(historyId==Guid.Empty)return BadRequest();
        var row=await CreatorAllocationHistory.Current(db,historyId,ct);
        if(row is null)return NotFound();
        return Ok(new {allocation=row,digest=CreatorAllocationHistory.Digest(row),payable=false,settlementVerified=false});
    }
    [HttpPost("allocation-history")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> ShareAllocationHistory([FromBody] ShareCreatorAllocationHistory request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorAllocationHistoryRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject))return StatusCode(403);
        if(request.HistoryId==Guid.Empty || !CreatorCompensationProposal.ValidDocumentHash(request.Digest)
            || !request.AllocationReviewed || !request.PermissionToShareReviewed)return BadRequest();
        try {
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await CreatorAllocationHistory.Current(db,request.HistoryId,ct);
            if(row is null || CreatorAllocationHistory.Digest(row)!=request.Digest)return Conflict();
            if(await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==row.CreatorId && m.Subject==subject,ct))return StatusCode(403);
            var id=CreatorAllocationHistory.Id(row.HistoryId);
            var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
            if(prior is not null && !CreatorAllocationHistory.Matches(prior,row))return Conflict();
            if(prior is null){
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent {Id=id,AdvertiserId=row.AdvertiserId,WorkspaceId=row.WorkspaceId,
                    Action=CreatorAllocationHistory.Action,ActorType="OPERATOR",ActorLabel=subject,Outcome="SHARED",
                    RequestId=row.CreatorId.ToString("N"),Detail=CreatorAllocationHistory.Detail(row),OccurredAt=DateTime.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new {publicationId=id,historyId=row.HistoryId,shared=true,payable=false,settlementVerified=false});
        }catch(Npgsql.PostgresException pg) when(pg.SqlState==Npgsql.PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
    }
    [HttpGet("settlement-evidence")]
    public async Task<IActionResult> PreviewSettlementEvidence([FromQuery] Guid historyId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorSettlementEvidenceRequested || !auth.CreatorAllocationHistoryRequested || !auth.CreatorAgreementEvidenceRequested
            || !auth.CreatorCompensationProposalRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out _))return StatusCode(403);
        if(historyId==Guid.Empty)return BadRequest();
        var row=await CreatorSettlementEvidence.Current(db,historyId,ct);
        if(row is null)return NotFound();
        var id=CreatorSettlementEvidence.Id(row.Allocation.PlacementId);
        var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
        var review=auth.CreatorPayableReviewRequested?await CreatorPayableReview.FindCurrent(db,row,ct):null;
        var reviewId=CreatorPayableReview.Id(row.Allocation.PlacementId);
        var reviewExists=await db.WeddingPlannerAuditEvents.AnyAsync(a=>a.Id==reviewId,ct);
        return Ok(new {settlement=row,digest=CreatorSettlementEvidence.Digest(row),existingEvidence=prior is not null,
            settlementEvidenceId=prior?.Id,settlementExceptionEnabled=auth.CreatorSettlementExceptionRequested,
            payableReviewEnabled=auth.CreatorPayableReviewRequested,payableReviewRecorded=review is not null,payableReviewExists=reviewExists,
            transferAuthorized=false,providerVerified=false,payable=false});
    }

    [HttpPost("settlement-evidence")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> RecordSettlementEvidence([FromBody] RecordCreatorSettlementEvidence request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorSettlementEvidenceRequested || !auth.CreatorAllocationHistoryRequested || !auth.CreatorAgreementEvidenceRequested
            || !auth.CreatorCompensationProposalRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject))return StatusCode(403);
        if(request.HistoryId==Guid.Empty || !CreatorCompensationProposal.ValidDocumentHash(request.Digest)
            || !CreatorCompensationProposal.ValidDocumentHash(request.TransferReferenceSha256)
            || !CreatorCompensationProposal.ValidDocumentHash(request.EvidenceDocumentSha256)
            || request.Amount<=0 || request.CurrencyCode is not {Length:3}
            || !request.FulfillmentReviewed || !request.RecipientReviewed || !request.CompletedTransferReviewed
            || !request.NoReversalReviewed || !request.NoDuplicateReviewed)return BadRequest();
        // Canonical UTC whole-second evidence timestamp.
        var completedAt=DateTimeOffset.FromUnixTimeSeconds(request.CompletedAt.ToUnixTimeSeconds());
        if(completedAt>DateTimeOffset.UtcNow || completedAt.Year<2020)return BadRequest();
        try{
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await CreatorSettlementEvidence.Current(db,request.HistoryId,ct);
            if(row is null || CreatorSettlementEvidence.Digest(row)!=request.Digest
                || row.Allocation.CreatorAllocation!=request.Amount || row.Allocation.CurrencyCode!=request.CurrencyCode
                || completedAt.UtcDateTime<row.AgreementRecordedAt)return Conflict();
            if(await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==row.Allocation.CreatorId && m.Subject==subject,ct))return StatusCode(403);
            if(auth.CreatorPayableReviewRequested){
                var review=await CreatorPayableReview.FindCurrent(db,row,ct);
                if(review is null || !CreatorPayableReview.Matches(review,row,out var fulfilledAt) || completedAt<fulfilledAt)
                    return Conflict(new {error="Current fulfillment and payable review evidence required."});
            }
            var id=CreatorSettlementEvidence.Id(row.Allocation.PlacementId);
            var exceptionId=CreatorSettlementException.Id(id);
            if(await db.WeddingPlannerAuditEvents.AnyAsync(a=>a.Id==exceptionId,ct))return Conflict(new {error="Settlement exception recorded. No repeat settlement permitted."});
            var detail=CreatorSettlementEvidence.Detail(row,request.TransferReferenceSha256,request.EvidenceDocumentSha256,completedAt);
            var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
            if(prior is not null && (!CreatorSettlementEvidence.Matches(prior,row,out _) || prior.Detail!=detail))return Conflict();
            if(await db.WeddingPlannerAuditEvents.AnyAsync(a=>a.Action==CreatorSettlementEvidence.Action
                && a.RequestId==request.TransferReferenceSha256 && a.Id!=id,ct))return Conflict();
            if(prior is null){
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent {Id=id,AdvertiserId=row.Allocation.AdvertiserId,
                    WorkspaceId=row.Allocation.WorkspaceId,Action=CreatorSettlementEvidence.Action,ActorType="OPERATOR",ActorLabel=subject,
                    Outcome="EVIDENCE_RECORDED",RequestId=request.TransferReferenceSha256,Detail=detail,OccurredAt=DateTime.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new {settlementEvidenceId=id,historyId=request.HistoryId,evidenceRecorded=true,
                providerVerified=false,transferAuthorized=false,payable=false});
        }catch(Npgsql.PostgresException pg) when(pg.SqlState==Npgsql.PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
    }
    [HttpPost("payable-review")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> RecordPayableReview([FromBody] RecordCreatorPayableReview request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorPayableReviewRequested || !auth.CreatorSettlementEvidenceRequested || !auth.CreatorAllocationHistoryRequested
            || !auth.CreatorAgreementEvidenceRequested || !auth.CreatorCompensationProposalRequested
            || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out var subject))return StatusCode(403);
        if(request.HistoryId==Guid.Empty || !CreatorCompensationProposal.ValidDocumentHash(request.Digest)
            || !CreatorCompensationProposal.ValidDocumentHash(request.FulfillmentSha256)
            || !CreatorCompensationProposal.ValidDocumentHash(request.FundingSha256)
            || request.Amount<=0 || request.CurrencyCode is not {Length:3}
            || !request.FulfillmentAcceptedReviewed || !request.FundingReceivedReviewed || !request.PaymentTermsReviewed
            || !request.NoDisputeReviewed || !request.NoPriorPaymentReviewed)return BadRequest();
        var fulfilledAt=DateTimeOffset.FromUnixTimeSeconds(request.FulfilledAt.ToUnixTimeSeconds());
        if(fulfilledAt.Year<2020 || fulfilledAt>DateTimeOffset.UtcNow)return BadRequest();
        try{
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await CreatorSettlementEvidence.Current(db,request.HistoryId,ct);
            if(row is null || CreatorSettlementEvidence.Digest(row)!=request.Digest || row.Allocation.CreatorAllocation!=request.Amount
                || row.Allocation.CurrencyCode!=request.CurrencyCode || fulfilledAt.UtcDateTime<row.AgreementRecordedAt)return Conflict();
            if(await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==row.Allocation.CreatorId && m.Subject==subject,ct))return StatusCode(403);
            var settlementId=CreatorSettlementEvidence.Id(row.Allocation.PlacementId);
            if(await db.WeddingPlannerAuditEvents.AnyAsync(a=>a.Id==settlementId,ct))return Conflict(new {error="Settlement evidence already exists."});
            var id=CreatorPayableReview.Id(row.Allocation.PlacementId);
            var detail=CreatorPayableReview.Detail(row,request.FulfillmentSha256,request.FundingSha256,fulfilledAt);
            var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
            if(prior is not null && (!CreatorPayableReview.Matches(prior,row,out _) || prior.Detail!=detail))return Conflict();
            if(prior is null){
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent {Id=id,AdvertiserId=row.Allocation.AdvertiserId,
                    WorkspaceId=row.Allocation.WorkspaceId,Action=CreatorPayableReview.Action,ActorType="OPERATOR",
                    ActorLabel=subject,Outcome="REVIEW_RECORDED",RequestId=row.Allocation.CreatorId.ToString("N"),
                    Detail=detail,OccurredAt=DateTime.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new {payableReviewId=id,historyId=request.HistoryId,reviewRecorded=true,liabilityVerified=false,
                payable=false,transferAuthorized=false,providerVerified=false});
        }catch(Npgsql.PostgresException pg) when(pg.SqlState==Npgsql.PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
    }

    [HttpGet("settlement-exception")]
    public async Task<IActionResult> PreviewSettlementException([FromQuery] Guid evidenceId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorSettlementExceptionRequested || !auth.CreatorSettlementEvidenceRequested || !TryOperator(out _))return StatusCode(403);
        if(evidenceId==Guid.Empty)return BadRequest();
        var row=await CreatorSettlementException.Original(db,evidenceId,ct);
        if(row is null)return NotFound();
        var id=CreatorSettlementException.Id(evidenceId);
        var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
        return Ok(new {original=row,exceptionExists=prior is not null,
            exceptionStatus=prior is null?"NONE":CreatorSettlementException.Matches(prior,row)?prior.Outcome:"REVIEW_REQUIRED",
            providerVerified=false,refundAuthorized=false,transferAuthorized=false,payable=false});
    }

    [HttpPost("settlement-exception")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> RecordSettlementException([FromBody] RecordCreatorSettlementException request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorSettlementExceptionRequested || !auth.CreatorSettlementEvidenceRequested || !TryOperator(out var subject))return StatusCode(403);
        if(request.EvidenceId==Guid.Empty || !CreatorCompensationProposal.ValidDocumentHash(request.OriginalDigest)
            || !CreatorCompensationProposal.ValidDocumentHash(request.DocumentSha256) || !CreatorSettlementException.ValidReason(request.Reason)
            || !request.OriginalTransferReviewed || !request.ExceptionEvidenceReviewed || !request.ConfirmHold)return BadRequest();
        var observed=DateTimeOffset.FromUnixTimeSeconds(request.ObservedAt.ToUnixTimeSeconds());
        if(observed.Year<2020 || observed>DateTimeOffset.UtcNow)return BadRequest();
        try{
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var row=await CreatorSettlementException.Original(db,request.EvidenceId,ct);
            if(row is null || row.OriginalDigest!=request.OriginalDigest || observed<row.TransferAt)return Conflict();
            if(await db.CreatorAccountMemberships.AnyAsync(m=>m.CreatorId==row.CreatorId && m.Subject==subject,ct))return StatusCode(403);
            var id=CreatorSettlementException.Id(row.EvidenceId);
            var detail=CreatorSettlementException.Detail(row,request.DocumentSha256,observed);
            var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
            if(prior is not null && (!CreatorSettlementException.Matches(prior,row) || prior.Detail!=detail || prior.Outcome!=request.Reason))return Conflict();
            if(prior is null){
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent {Id=id,AdvertiserId=row.AdvertiserId,WorkspaceId=row.WorkspaceId,
                    Action=CreatorSettlementException.Action,ActorType="OPERATOR",ActorLabel=subject,Outcome=request.Reason,
                    RequestId=row.CreatorId.ToString("N"),Detail=detail,OccurredAt=DateTime.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new {exceptionId=id,evidenceId=row.EvidenceId,status=request.Reason,holdRecorded=true,
                providerVerified=false,refundAuthorized=false,transferAuthorized=false,payable=false});
        }catch(Npgsql.PostgresException pg) when(pg.SqlState==Npgsql.PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
    }

    [HttpGet("fulfillment-manifest")]
    public async Task<IActionResult> FulfillmentManifest([FromQuery] Guid handoffId,[FromQuery] Guid illustrationId,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorFulfillmentManifestRequested || !auth.CreatorCampaignInvitationRequested || !auth.CreatorCompensationProposalRequested
            || !auth.CreatorAgreementEvidenceRequested || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out _))return StatusCode(403);
        if(handoffId==Guid.Empty || illustrationId==Guid.Empty)return BadRequest();
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var manifest=await CreatorFulfillmentManifest.Build(db,handoffId,illustrationId,ct);
        if(manifest is null)return NotFound();
        await tx.CommitAsync(ct);
        return Ok(new {manifest,manifestSha256=ControlledAiService.Hash(CreatorFactorySubmission.Serialize(manifest)),
            factorySubmissionEnabled=auth.CreatorFactorySubmissionRequested});
    }

    [HttpPost("factory-submission")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> SubmitFactoryPreparation([FromBody] SubmitCreatorFactoryPreparation request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorFactorySubmissionRequested || !auth.CreatorFulfillmentManifestRequested || !auth.CreatorCampaignInvitationRequested
            || !auth.CreatorCompensationProposalRequested || !auth.CreatorAgreementEvidenceRequested || !auth.CreatorAuthorityVerificationRequested
            || !TryOperator(out _))return StatusCode(403);
        if(request.HandoffId==Guid.Empty || request.IllustrationId==Guid.Empty || !request.ConfirmHeldMetadataOnly
            || !CreatorCompensationProposal.ValidDocumentHash(request.ManifestSha256))return BadRequest();
        string body;
        await using(var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct)){
            var manifest=await CreatorFulfillmentManifest.Build(db,request.HandoffId,request.IllustrationId,ct);
            if(manifest is null)return NotFound();
            body=CreatorFactorySubmission.Serialize(manifest);
            if(ControlledAiService.Hash(body)!=request.ManifestSha256)return Conflict(new {error="Current manifest changed. Review again."});
            await tx.CommitAsync(ct);
        }
        var receipt=await CreatorFactorySubmission.Submit(body,request.HandoffId,ct);
        return receipt is null?StatusCode(503,new {error="Factory admission not confirmed. Recheck the exact manifest before retrying."}):Ok(receipt.Value);
    }

    [HttpPost("factory-artwork")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> RetrieveFactoryArtwork([FromBody] RetrieveCreatorFactoryArtwork request,CancellationToken ct)
    {
        Response.Headers.CacheControl="no-store";
        if(!auth.CreatorFactoryArtworkRequested || !auth.CreatorFactorySubmissionRequested || !auth.CreatorFulfillmentManifestRequested
            || !auth.CreatorCampaignInvitationRequested || !auth.CreatorCompensationProposalRequested || !auth.CreatorAgreementEvidenceRequested
            || !auth.CreatorAuthorityVerificationRequested || !TryOperator(out _))return StatusCode(403);
        if(!request.ConfirmPrivateArtworkHandling || request.HandoffId==Guid.Empty || request.IllustrationId==Guid.Empty
            || !CreatorCompensationProposal.ValidDocumentHash(request.ManifestSha256))return BadRequest();
        string body;
        await using(var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct)){
            var current=await CreatorFulfillmentManifest.Build(db,request.HandoffId,request.IllustrationId,ct);
            if(current is null)return NotFound();
            body=CreatorFactorySubmission.Serialize(current);
            if(ControlledAiService.Hash(body)!=request.ManifestSha256)return Conflict();
            await tx.CommitAsync(ct);
        }
        if(await CreatorFactorySubmission.Submit(body,request.HandoffId,ct,readOnly:true) is null)
            return StatusCode(503,new {error="Exact Factory admission not confirmed."});
        await using var snapshot=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var refreshed=await CreatorFulfillmentManifest.Build(db,request.HandoffId,request.IllustrationId,ct);
        if(refreshed is null || CreatorFactorySubmission.Serialize(refreshed)!=body)return Conflict();
        using var doc=System.Text.Json.JsonDocument.Parse(body);
        var manifest=doc.RootElement;
        var candidateId=manifest.GetProperty("candidateId").GetGuid();
        var candidate=await db.AcademyProductionCandidates.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==candidateId,ct);
        if(candidate is null || candidate.ImageBytes.Length is <33 or >16000000)return NotFound();
        var bytes=candidate.ImageBytes;
        var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        if(hash!=manifest.GetProperty("imageSha256").GetString()
            || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})
            || !bytes.AsSpan(12,4).SequenceEqual(new byte[]{73,72,68,82})
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8,4))!=13
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4))!=manifest.GetProperty("imageWidth").GetInt32()
            || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4))!=manifest.GetProperty("imageHeight").GetInt32())return Conflict();
        await snapshot.CommitAsync(ct);
        Response.Headers["X-Content-Type-Options"]="nosniff";
        return File(bytes,"image/png","alpha-private-"+candidateId.ToString("N")+".png");
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

public sealed record RecordCreatorAgreementEvidence(Guid InvitationId,Guid IllustrationId,string Digest,string DocumentSha256,
    bool SignedAgreementReviewed,bool PartiesAndScopeReviewed,bool CompensationAndPaymentTermsReviewed,bool CancellationAndRightsReviewed);

public sealed record ShareCreatorAllocationHistory(Guid HistoryId,string Digest,bool AllocationReviewed,bool PermissionToShareReviewed);

public sealed record RecordCreatorSettlementEvidence(Guid HistoryId,string Digest,decimal Amount,string CurrencyCode,
    string TransferReferenceSha256,string EvidenceDocumentSha256,DateTimeOffset CompletedAt,bool FulfillmentReviewed,
    bool RecipientReviewed,bool CompletedTransferReviewed,bool NoReversalReviewed,bool NoDuplicateReviewed);

public sealed record RecordCreatorPayableReview(Guid HistoryId,string Digest,decimal Amount,string CurrencyCode,
    string FulfillmentSha256,string FundingSha256,DateTimeOffset FulfilledAt,bool FulfillmentAcceptedReviewed,
    bool FundingReceivedReviewed,bool PaymentTermsReviewed,bool NoDisputeReviewed,bool NoPriorPaymentReviewed);

public sealed record RecordCreatorSettlementException(Guid EvidenceId,string OriginalDigest,string DocumentSha256,string Reason,
    DateTimeOffset ObservedAt,bool OriginalTransferReviewed,bool ExceptionEvidenceReviewed,bool ConfirmHold);

public sealed record SubmitCreatorFactoryPreparation(Guid HandoffId,Guid IllustrationId,string ManifestSha256,bool ConfirmHeldMetadataOnly);

public sealed record RetrieveCreatorFactoryArtwork(Guid HandoffId,Guid IllustrationId,string ManifestSha256,bool ConfirmPrivateArtworkHandling);
