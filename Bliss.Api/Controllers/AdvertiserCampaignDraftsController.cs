using Bliss.Api.Operations;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.WeddingPlanner;
using System.Security.Cryptography;
using System.Data;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Controllers;

// Customer draft intake only. Never creates placements, spend, creative jobs,
// verified commercial authority or outbound work.
[ApiController]
[Authorize]
[Route("api/advertisers/my-campaign-drafts")]
public sealed class AdvertiserCampaignDraftsController(BlissDbContext db, BlissAuthenticationOptions auth) : ControllerBase
{
    private const string Draft = "DRAFT";
    private const string Unverified = "UNVERIFIED";
    private const string DraftProgramMarker = "alpha:customer-campaign-draft:v1";

    private IQueryable<Guid> ActiveBusiness()
    {
        if (!auth.Enabled || !auth.SupabaseEnabled || !auth.AdvertiserSelfServiceRequested
            || !auth.AdvertiserCampaignDraftsRequested)
            return db.Advertisers.Where(x => false).Select(x => x.Id);

        var membershipLane = BlissAuthorization.CanResolveAdvertiserMembershipProfile(User, auth);
        var legacyLane = BlissAuthorization.TryResolveVerifiedAdvertiserId(User, auth, out var legacyId);
        var subject = User.FindFirst("sub")?.Value;
        if ((!membershipLane && !legacyLane) || string.IsNullOrWhiteSpace(subject))
            return db.Advertisers.Where(x => false).Select(x => x.Id);

        // Unlike the historical profile path, drafts ALWAYS require a registered,
        // live membership and original consent, even for an issuer-approved customer.
        return from m in db.AdvertiserAccountMemberships.AsNoTracking()
               join r in db.AdvertiserRegistrationReceipts.AsNoTracking()
                   on new { m.Subject, m.AdvertiserId } equals new { r.Subject, r.AdvertiserId }
               where m.Subject == subject && m.Status == "ACTIVE"
                   && (membershipLane || m.AdvertiserId == legacyId)
                   && r.ProcessingConsent && r.TermsAccepted
               select m.AdvertiserId;
    }

    // Compose predicates and ordering over mapped EF entities, not over a
    // constructed positional DTO. Project only at the final SQL boundary.
    private IQueryable<Campaign> OwnDrafts(Guid advertiserId) =>
        db.Campaigns.AsNoTracking().Where(c =>
            c.AdvertiserOpportunity != null
            && c.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == advertiserId
            && c.AdvertiserOpportunity.AdvertiserProgram.ExternalProgramId == DraftProgramMarker
            && c.AdvertiserOpportunity.AdvertiserProgram.Status == Draft
            && c.AdvertiserOpportunity.Status == Draft
            && c.AdvertiserOpportunity.AuthorityStatus == Unverified
            && c.Status == Draft && c.AuthorityStatus == Unverified);

    private static IQueryable<CampaignDraftSummary> Summaries(IQueryable<Campaign> source) =>
        source.Select(c => new CampaignDraftSummary(c.Id, c.Name,
            c.AdvertiserOpportunity!.Description!, c.Status, c.CreatedAt));

    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var allowed = ActiveBusiness();
        var id = await allowed.SingleOrDefaultAsync(ct);
        if (id == Guid.Empty) return StatusCode(403);
        return Ok(await Summaries(OwnDrafts(id).Where(_ => allowed.Contains(id))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(50)).ToArrayAsync(ct));
    }

    // Source-only export of a customer-owned DRAFT to the existing offline
    // image/Factory QA workflow. This is data, not an executable AI instruction.
    // No customer-provided ID can select another advertiser's campaign.
    [HttpPost("starter-offer")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> StarterOffer([FromBody] CustomerStarterOfferRequest request,CancellationToken ct)
    {
        Response.Headers.CacheControl="private, no-store";
        if(!auth.StarterServiceOfferRequested || request.CampaignId==Guid.Empty || request.Action is not ("STATUS" or "ACCEPT"))return StatusCode(403);
        var subject=User.FindFirst("sub")?.Value;
        if(string.IsNullOrWhiteSpace(subject))return StatusCode(403);
        try{
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
            var advertiserId=await ActiveBusiness().SingleOrDefaultAsync(ct);
            if(advertiserId==Guid.Empty || !await StarterServiceOffer.ActiveMember(db,advertiserId,subject,ct))return StatusCode(403);
            var draft=await Summaries(OwnDrafts(advertiserId).Where(x=>x.Id==request.CampaignId)).SingleOrDefaultAsync(ct);
            if(draft is null)return NotFound();
            var digest=StaffCreativePreparationController.CampaignDigest(draft.CampaignId,draft.Name,draft.Brief);
            var offerId=StarterServiceOffer.OfferId(draft.CampaignId,digest);
            var offer=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==offerId && x.AdvertiserId==advertiserId
                && x.Action==StarterServiceOffer.OfferAction && x.ActorType=="OPERATOR" && x.Outcome=="OFFERED",ct);
            var terms=StarterServiceOffer.Parse(offer?.Detail);
            if(offer is null || terms is null || terms.CampaignId!=draft.CampaignId || terms.CampaignDigest!=digest
                || offer.ActorLabel==subject || offer.WorkspaceId is null
                || !await db.WeddingPlannerWorkspaces.AnyAsync(x=>x.Id==offer.WorkspaceId && x.AdvertiserId==advertiserId && x.IsPrimary && x.Status=="ACTIVE",ct))return NotFound();
            var sha=ControlledAiService.Hash(offer.Detail!);
            if(request.Action=="ACCEPT"){
                if(request.OfferId!=offerId || request.OfferSha256!=sha || !request.AcceptedExactTerms)return Conflict();
                var id=StarterServiceOffer.AcceptanceId(offerId);
                var prior=await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct);
                if(prior is not null && (prior.ActorLabel!=subject || prior.Detail!=sha || prior.Action!=StarterServiceOffer.AcceptAction
                    || prior.AdvertiserId!=advertiserId || prior.WorkspaceId!=offer.WorkspaceId || prior.Outcome!="ACCEPTED"))return Conflict();
                if(prior is null){
                    db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent{Id=id,AdvertiserId=advertiserId,WorkspaceId=offer.WorkspaceId,
                        Action=StarterServiceOffer.AcceptAction,ActorType="ADVERTISER",ActorLabel=subject,Outcome="ACCEPTED",Detail=sha,OccurredAt=DateTime.UtcNow});
                    await db.SaveChangesAsync(ct);
                }
            }
            var accepted=await StarterServiceOffer.CurrentAccepted(db,draft.CampaignId,offerId,ct);
            var orderProgress = accepted is null ? "AWAITING_ACCEPTANCE"
                : await StarterServiceOffer.OrderProgress(db,accepted,draft.CampaignId,ct);
            var creativeProgress = "NOT_AVAILABLE";
            if (auth.AdvertiserCreativeProviderConsentRequested && auth.AdvertiserCreativeArtworkRequested)
            {
                var prepared = await CampaignRecipeRevision.ForCampaign(db.AcademyProductionBriefs.AsNoTracking()
                    .Where(x => x.WorkspaceId == offer.WorkspaceId), draft.CampaignId, digest).FirstOrDefaultAsync(ct);
                creativeProgress = "AWAITING_RECIPE";
                if (prepared is not null && CampaignRecipeRevision.Matches(prepared.IdempotencyKey, draft.CampaignId, digest))
                {
                    creativeProgress = "RECIPE_RECORDED";
                    var grant = await CampaignImageAuthorization.Current(db, prepared.WorkspaceId, prepared.Id, ct);
                    if (grant is not null && grant.CampaignId == draft.CampaignId && grant.CampaignDigest == digest)
                    {
                        creativeProgress = "CONSENT_RECORDED";
                        var run = await db.ControlledAiRuns.AsNoTracking().Where(x => x.WorkspaceId == prepared.WorkspaceId
                            && x.BriefId == prepared.Id && x.IdempotencyKey == CampaignImageAuthorization.RunKey(prepared.Id)
                            && x.Kind == "IMAGE").Select(x => new { x.Status, x.InputSnapshotJson, x.InputSha256,
                                x.Model, x.Provider, HasOutput = x.OutputImage != null }).SingleOrDefaultAsync(ct);
                        if (run is not null && run.Model == ProviderConsentModel
                            && string.Equals(run.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
                            && ControlledAiService.Hash(run.InputSnapshotJson) == run.InputSha256
                            && CampaignImageAuthorization.ProviderInput(run.InputSnapshotJson, grant) is not null)
                            creativeProgress = run.Status == "SUCCEEDED_PENDING_REVIEW" && run.HasOutput
                                ? "IMAGE_RECORDED_PENDING_REVIEW" : "IMAGE_RUN_RECORDED";
                    }
                }
            }
            await tx.CommitAsync(ct);
            return Ok(new{campaignId=draft.CampaignId,offerId,offerSha256=sha,scope=terms.Scope,terms=terms.TermsText,
                amountCents=14900,currency="usd",status=accepted is null?"OFFERED":"ACCEPTED",
                orderProgress,creativeProgress,paymentAuthorized=false,release="HELD"});
        }catch(PostgresException pg) when(pg.SqlState==PostgresErrorCodes.SerializationFailure){return Conflict();}
        catch(DbUpdateException){return Conflict();}
    }

    [HttpPost("creative-handoff-preview")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> Preview([FromBody] CreativeHandoffPreviewRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.AdvertiserCreativeHandoffPreviewRequested || request.CampaignId == Guid.Empty)
            return StatusCode(403);
        var allowed = ActiveBusiness();
        var id = await allowed.SingleOrDefaultAsync(ct);
        if (id == Guid.Empty) return StatusCode(403);
        var draft = await Summaries(OwnDrafts(id).Where(c => c.Id == request.CampaignId && allowed.Contains(id)))
            .SingleOrDefaultAsync(ct);
        if (draft is null) return NotFound();
        // This checksum detects accidental changes in an offline handoff only.
        // It is NOT a signature, issuer authority, approval, or delivery permit.
        var binding = draft.CampaignId.ToString("D") + "\n" + draft.Name + "\n" + draft.Brief;
        var digest = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(binding)))
            .ToLowerInvariant();
        return Ok(new
        {
            schema = "alpha.advertiser.campaign-creative-handoff.v1",
            source = "Bliss_Bot_Chapel",
            campaignId = draft.CampaignId,
            campaignName = draft.Name,
            creativeBrief = draft.Brief,
            briefSha256 = digest,
            status = "DRAFT",
            authorityStatus = "UNVERIFIED",
            candidateModel = "gpt-image-2.5-flare",
            modelCall = "NOT_REQUESTED",
            billingAuthorization = "NOT_GRANTED",
            customerProviderConsent = "NOT_COLLECTED",
            humanReview = "PENDING",
            factoryAction = "OFFLINE_QA_ONLY",
            globalStop = "NOT_READ_NOT_CHANGED",
            release = "HELD",
            delivery = "NOT_SENT",
            signedAuthorization = false
        });
    }

    public const string ProviderConsentNotice = "alpha-creative-provider-consent-v1";
    public const string ProviderConsentModel = "gpt-image-2.5-flare";
    public const string ProviderConsentAction = "CREATIVE_PROVIDER_CONSENT";

    public static string ProviderConsentScope(Guid campaignId, string campaignDigest, Guid recipeId, string recipeDigest) =>
        Bliss.Api.Operations.ControlledAiService.Hash($"{ProviderConsentNotice}\nOpenAI\n{ProviderConsentModel}\n{campaignId:D}\n{campaignDigest}\n{recipeId:D}\n{recipeDigest}");

    [HttpPost("creative-artwork")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Artwork([FromBody] CampaignArtworkRequest request,
        [FromServices] CreativeHandoffService handoff, [FromServices] WeddingPlannerService planner, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!auth.AdvertiserCreativeArtworkRequested || !auth.AdvertiserCreativeProviderConsentRequested
            || request.CampaignId == Guid.Empty) return StatusCode(403);
        if (request.Action is not ("STATUS" or "PREVIEW" or "APPROVE" or "WITHHOLD" or "DOWNLOAD" or "ACKNOWLEDGE")
            || ((request.Action is "APPROVE" or "WITHHOLD") &&
                (request.CandidateId is null || request.ReviewId is null || request.ImageSha256 is not { Length: 64 }
                    || request.ExpectedVersion is null or < 0 || request.RequestKey is null || request.RequestKey == Guid.Empty
                    || string.IsNullOrWhiteSpace(request.Rationale) || request.Rationale.Length > 2000))) return BadRequest();
        var subject = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return StatusCode(403);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var allowed = ActiveBusiness();
            var advertiserId = await allowed.SingleOrDefaultAsync(ct);
            if (advertiserId == Guid.Empty) return StatusCode(403);
            var currentConsent = await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
                join receipt in db.AdvertiserRegistrationReceipts.AsNoTracking()
                    on new { m.Subject, m.AdvertiserId } equals new { receipt.Subject, receipt.AdvertiserId }
                where m.Subject == subject && m.AdvertiserId == advertiserId && m.Status == "ACTIVE"
                    && receipt.ProcessingConsent && receipt.TermsAccepted
                    && receipt.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                    && receipt.TermsVersion == AdvertiserRegistrationController.TermsVersion
                select m.Id).AnyAsync(ct);
            if (!currentConsent) return StatusCode(403);
            var draft = await Summaries(OwnDrafts(advertiserId).Where(x => x.Id == request.CampaignId && allowed.Contains(advertiserId))).SingleOrDefaultAsync(ct);
            if (draft is null) return NotFound();
            var digest = StaffCreativePreparationController.CampaignDigest(draft.CampaignId, draft.Name, draft.Brief);
            var prepared = await CampaignRecipeRevision.ForCampaign(
                from p in db.AcademyProductionBriefs.AsNoTracking()
                join w in db.WeddingPlannerWorkspaces.AsNoTracking() on p.WorkspaceId equals w.Id
                where w.AdvertiserId == advertiserId && w.IsPrimary && w.Status == "ACTIVE"
                select p,draft.CampaignId,digest).FirstOrDefaultAsync(ct);
            if (prepared is null) return NotFound();
            var grant = await CampaignImageAuthorization.Current(db, prepared.WorkspaceId, prepared.Id, ct);
            if (grant is null || grant.CampaignId != draft.CampaignId || grant.CampaignDigest != digest) return NotFound();
            var run = await db.ControlledAiRuns.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == prepared.WorkspaceId
                && x.BriefId == prepared.Id && x.IdempotencyKey == CampaignImageAuthorization.RunKey(prepared.Id)
                && x.Kind == "IMAGE" && x.Status == "SUCCEEDED_PENDING_REVIEW", ct);
            if (run is null || run.OutputImage is null || run.CostStatus != "PROVIDER_REPORTED"
                || !run.ActualUsd.HasValue || run.ActualUsd.Value < 0 || run.ActualUsd.Value > run.ReservedUsd
                || string.IsNullOrWhiteSpace(run.ProviderRequestId)
                || run.Model != ProviderConsentModel || !string.Equals(run.Provider,"OpenAI",StringComparison.OrdinalIgnoreCase)
                || ControlledAiService.Hash(run.InputSnapshotJson) != run.InputSha256
                || CampaignImageAuthorization.ProviderInput(run.InputSnapshotJson, grant) is null) return NotFound();
            var key = "ai-run-" + run.Id;
            var candidate = await db.AcademyProductionCandidates.AsNoTracking().Where(x => x.BriefId == prepared.Id)
                .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (candidate is null || candidate.IdempotencyKey != key || candidate.ImageSha256 != run.OutputSha256
                || candidate.ImageBytes.Length > CandidatePng.MaxGeneratedBytes
                || Convert.ToHexString(SHA256.HashData(candidate.ImageBytes)).ToLowerInvariant() != candidate.ImageSha256) return NotFound();
            var review = await db.AcademyProductionReviews.AsNoTracking().Where(x => x.CandidateId == candidate.Id)
                .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (review is null || review.Decision != "PASS" || review.CriticalDefect
                || new[] { review.Customization, review.QualityParity, review.Originality, review.GeographicAuthenticity }.Any(x => x != "PASS"))
                return NotFound();
            // Only an explicit staff PASS of this exact candidate makes it visible for private customer review.
            if (review.ActorLabel == subject) return StatusCode(403);
            CandidatePng.ValidateGenerated(candidate.ImageBytes);
            if (request.Action == "PREVIEW")
            {
                if (request.CandidateId != candidate.Id || request.ReviewId != review.Id || request.ImageSha256 != candidate.ImageSha256)
                    return Conflict(new { error = "Artwork changed. Refresh the review." });
                await tx.CommitAsync(ct);
                return File(candidate.ImageBytes, "image/png");
            }
            var latest = await db.CandidateApprovalDecisions.AsNoTracking().Where(x => x.CandidateId == candidate.Id && x.ScopeKey == "ADVERTISER")
                .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            if (request.Action is "APPROVE" or "WITHHOLD")
            {
                if (request.CandidateId != candidate.Id || request.ReviewId != review.Id || request.ImageSha256 != candidate.ImageSha256)
                    return Conflict(new { error = "Artwork changed. Refresh the review." });
                var workspace = await planner.GetWorkspaceAsync(prepared.WorkspaceId, false, advertiserId,
                    "ADVERTISER", subject, RequestCorrelation.Resolve(HttpContext), ct);
                await handoff.Decide(workspace, candidate.Id, new DecideCandidateApproval("ADVERTISER", request.Action,
                    review.Id, null, request.ExpectedVersion!.Value, request.Rationale!, false, false, false, request.RequestKey!.Value.ToString("D")),
                    subject, subject, true, ct);
                latest = await db.CandidateApprovalDecisions.AsNoTracking().Where(x => x.CandidateId == candidate.Id && x.ScopeKey == "ADVERTISER")
                    .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
            }
            Guid? releaseId = null;
            if (auth.StarterArtworkDeliveryRequested && auth.StarterPaymentReconciliationRequested
                && latest?.Decision == "APPROVE" && latest.ReviewId == review.Id)
            {
                var suffix = StarterArtworkDelivery.Suffix(draft.CampaignId,candidate.Id,review.Id,latest.Id,candidate.ImageSha256);
                var release = await db.WeddingPlannerAuditEvents.AsNoTracking().Where(x => x.AdvertiserId == advertiserId
                    && x.WorkspaceId == prepared.WorkspaceId && x.Action == StarterArtworkDelivery.ReleaseAction
                    && x.ActorType == "OPERATOR" && x.Outcome == "RELEASED" && x.Detail != null && x.Detail.EndsWith(suffix))
                    .OrderByDescending(x => x.OccurredAt).FirstOrDefaultAsync(ct);
                if (release?.Detail is not null && Guid.TryParseExact(release.Detail.Split(':')[0],"N",out var paymentId)
                    && release.Id == StarterArtworkDelivery.StableId(StarterArtworkDelivery.ReleaseAction,paymentId.ToString("N"))
                    && release.Detail == paymentId.ToString("N") + suffix
                    && await StarterArtworkDelivery.Reconciliation(db,advertiserId,prepared.WorkspaceId,draft.CampaignId,paymentId,ct) is not null)
                    releaseId = release.Id;
            }
            WeddingPlannerAuditEvent? downloadRequest = null;
            Guid? downloadRequestId = releaseId.HasValue ? StarterArtworkDelivery.DownloadRequestId(releaseId.Value, subject) : null;
            if (downloadRequestId.HasValue)
            {
                downloadRequest = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == downloadRequestId.Value, ct);
                if (downloadRequest is not null && !StarterArtworkDelivery.MatchesDownloadRequest(downloadRequest,
                    advertiserId, prepared.WorkspaceId, releaseId!.Value, subject)) return Conflict();
            }
            WeddingPlannerAuditEvent? acknowledgment = null;
            if (releaseId.HasValue)
            {
                var ackId = StarterArtworkDelivery.AcknowledgmentId(releaseId.Value, subject);
                acknowledgment = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ackId, ct);
                if (acknowledgment is not null && (downloadRequest is null ||
                    !StarterArtworkDelivery.MatchesAcknowledgment(acknowledgment, advertiserId,
                        prepared.WorkspaceId, releaseId.Value, subject, candidate.ImageSha256))) return Conflict();
                if (request.Action == "ACKNOWLEDGE")
                {
                    if (downloadRequest is null || request.ReleaseId != releaseId || request.CandidateId != candidate.Id
                        || request.ReviewId != review.Id || request.ImageSha256 != candidate.ImageSha256
                        || request.ConfirmedReceipt != true) return Conflict();
                    if (acknowledgment is null)
                    {
                        acknowledgment = new WeddingPlannerAuditEvent {Id=ackId,AdvertiserId=advertiserId,
                            WorkspaceId=prepared.WorkspaceId,Action=StarterArtworkDelivery.AcknowledgeAction,
                            ActorType="ADVERTISER",ActorLabel=subject,Outcome="ACKNOWLEDGED",
                            Detail=$"{releaseId.Value:N}:{candidate.ImageSha256}",OccurredAt=DateTime.UtcNow};
                        db.WeddingPlannerAuditEvents.Add(acknowledgment);
                        await db.SaveChangesAsync(ct);
                    }
                }
            }
            else if (request.Action == "ACKNOWLEDGE") return Conflict();
            if (request.Action == "DOWNLOAD")
            {
                if (releaseId is null || request.CandidateId != candidate.Id || request.ReviewId != review.Id
                    || request.ImageSha256 != candidate.ImageSha256) return Conflict();
                var id = downloadRequestId!.Value;
                if (downloadRequest is null)
                {
                    db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent {Id=id,AdvertiserId=advertiserId,WorkspaceId=prepared.WorkspaceId,
                        Action=StarterArtworkDelivery.DownloadAction,ActorType="ADVERTISER",ActorLabel=subject,
                        Outcome="REQUESTED",Detail=releaseId.Value.ToString("N"),OccurredAt=DateTime.UtcNow});
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
                return File(candidate.ImageBytes,"image/png","alpha-artwork-"+draft.CampaignId.ToString("N")+".png");
            }
            await tx.CommitAsync(ct);
            return Ok(new { campaignId = draft.CampaignId, candidateId = candidate.Id, reviewId = review.Id,
                imageSha256 = candidate.ImageSha256, approvalVersion = latest?.Version ?? 0,
                approval = latest?.ReviewId == review.Id ? latest.Decision : "PENDING",
                decisionReceipt = latest is not null && latest.ReviewId == review.Id && latest.ActorSubject == subject
                    ? new { decisionId = latest.Id, version = latest.Version, decision = latest.Decision,
                        rationale = latest.Rationale, recordedAt = latest.CreatedAt } : null,
                releaseId, release = releaseId.HasValue ? "ARTWORK_DOWNLOAD_AVAILABLE" : "HELD",
                delivery = releaseId.HasValue ? "DOWNLOAD_NOT_CONFIRMED" : "NOT_SENT",
                downloadRequestRecorded = downloadRequest is not null, downloadRequestedAt = downloadRequest?.OccurredAt,
                receiptAcknowledged = acknowledgment is not null, receiptAcknowledgedAt = acknowledgment?.OccurredAt,
                dispatchAuthorized = false, paymentAuthorized = false });
        }
        catch (WeddingPlannerNotFoundException) { return NotFound(); }
        catch (WeddingPlannerForbiddenException) { return StatusCode(403); }
        catch (WeddingPlannerConflictException) { return Conflict(new { error = "Artwork review changed. Refresh." }); }
        catch (InvalidOperationException) { return Conflict(new { error = "Artwork requirements not met." }); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            (pg.SqlState == PostgresErrorCodes.UniqueViolation || pg.SqlState == PostgresErrorCodes.SerializationFailure))
        { return Conflict(new { error = "Artwork decision changed concurrently." }); }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure)
        { return Conflict(new { error = "Artwork decision changed concurrently." }); }
    }

    [HttpPost("creative-provider-consent")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> ProviderConsent([FromBody] CampaignProviderConsentRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.AdvertiserCreativeProviderConsentRequested || request.CampaignId == Guid.Empty)
            return StatusCode(403);
        if (request.Action is not ("REVIEW" or "GRANT" or "REVOKE")
            || (request.Action != "REVIEW" && (request.RequestKey is null || request.RequestKey == Guid.Empty
                || request.ScopeSha256 is not { Length: 64 } || request.NoticeVersion != ProviderConsentNotice)))
            return BadRequest(new { error = "Exact recipe consent action and notice version required." });
        var subject = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return StatusCode(403);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var allowed = ActiveBusiness();
            var id = await allowed.SingleOrDefaultAsync(ct);
            if (id == Guid.Empty) return StatusCode(403);
            var currentConsent = await (from m in db.AdvertiserAccountMemberships.AsNoTracking()
                join receipt in db.AdvertiserRegistrationReceipts.AsNoTracking()
                    on new { m.Subject, m.AdvertiserId } equals new { receipt.Subject, receipt.AdvertiserId }
                where m.Subject == subject && m.AdvertiserId == id && m.Status == "ACTIVE"
                    && receipt.ProcessingConsent && receipt.TermsAccepted
                    && receipt.PrivacyNoticeVersion == AdvertiserRegistrationController.PrivacyNoticeVersion
                    && receipt.TermsVersion == AdvertiserRegistrationController.TermsVersion
                select m.Id).AnyAsync(ct);
            if (!currentConsent) return StatusCode(403);
            var draft = await Summaries(OwnDrafts(id).Where(c => c.Id == request.CampaignId && allowed.Contains(id))).SingleOrDefaultAsync(ct);
            if (draft is null) return NotFound();
            var digest = StaffCreativePreparationController.CampaignDigest(draft.CampaignId, draft.Name, draft.Brief);
            var prepared = await CampaignRecipeRevision.ForCampaign(
                from p in db.AcademyProductionBriefs.AsNoTracking()
                join w in db.WeddingPlannerWorkspaces.AsNoTracking() on p.WorkspaceId equals w.Id
                where w.AdvertiserId == id && w.IsPrimary && w.Status == "ACTIVE"
                select p,draft.CampaignId,digest).FirstOrDefaultAsync(ct);
            if (prepared is null) return NotFound(new { error = "No current production recipe has been prepared." });
            var latestBrand = await db.WeddingPlannerBrandDna.AsNoTracking().Where(x => x.WorkspaceId == prepared.WorkspaceId)
                .OrderByDescending(x => x.Version).Select(x => new { x.Id, x.Status }).FirstOrDefaultAsync(ct);
            if (latestBrand is null || latestBrand.Status != "CONFIRMED" || latestBrand.Id != prepared.BrandDnaId)
                return Conflict(new { error = "Current confirmed brand and recipe do not match." });
            using var json = System.Text.Json.JsonDocument.Parse(prepared.ProductionBriefJson);
            var recipe = json.RootElement.GetProperty("generationRecipe").GetString();
            if (string.IsNullOrWhiteSpace(recipe) || Bliss.Api.Operations.ControlledAiService.Hash(recipe) != prepared.RecipeSha256)
                return Conflict(new { error = "Production recipe integrity check failed." });
            var scope = ProviderConsentScope(draft.CampaignId, digest, prepared.Id, prepared.RecipeSha256);
            var history = db.WeddingPlannerAuditEvents.AsNoTracking().Where(e => e.AdvertiserId == id
                && e.WorkspaceId == prepared.WorkspaceId && e.Action == ProviderConsentAction && e.Detail == scope
                && e.ActorType == "ADVERTISER");
            var latest = await history.OrderByDescending(e => e.OccurredAt).ThenByDescending(e => e.Id).FirstOrDefaultAsync(ct);
            if (request.Action != "REVIEW")
            {
                if (request.ScopeSha256 != scope) return Conflict(new { error = "Recipe changed. Review the current recipe before responding." });
                var outcome = request.Action == "GRANT" ? "GRANTED" : "REVOKED";
                var prior = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == request.RequestKey!.Value, ct);
                if (prior is not null)
                {
                    if (prior.AdvertiserId != id || prior.WorkspaceId != prepared.WorkspaceId || prior.ActorType != "ADVERTISER"
                        || prior.ActorLabel != subject || prior.Action != ProviderConsentAction || prior.Detail != scope || prior.Outcome != outcome)
                        return Conflict(new { error = "Consent request key conflicts." });
                }
                else
                {
                    if (request.ExpectedReceiptId != latest?.Id)
                        return Conflict(new { error = "Consent changed. Review the latest status before responding." });
                    var now = DateTime.UtcNow;
                    if (latest is not null && now <= latest.OccurredAt) now = latest.OccurredAt.AddTicks(10);
                    latest = new WeddingPlannerAuditEvent { Id = request.RequestKey!.Value, AdvertiserId = id, WorkspaceId = prepared.WorkspaceId,
                        Action = ProviderConsentAction, ActorType = "ADVERTISER", ActorLabel = subject, Outcome = outcome,
                        Detail = scope, OccurredAt = now };
                    db.WeddingPlannerAuditEvents.Add(latest);
                    await db.SaveChangesAsync(ct);
                }
            }
            await tx.CommitAsync(ct);
            return Ok(new { campaignId = draft.CampaignId, recipeId = prepared.Id, recipeSha256 = prepared.RecipeSha256,
                scopeSha256 = scope, noticeVersion = ProviderConsentNotice, provider = "OpenAI", model = ProviderConsentModel,
                generationRecipe = recipe, consentStatus = latest?.Outcome ?? "NOT_COLLECTED", receiptId = latest?.Id,
                consentRecordedAt = latest?.OccurredAt, billingAuthorization = "NOT_GRANTED",
                providerCall = "NOT_REQUESTED", dispatchAuthorized = false, release = "HELD" });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            (pg.SqlState == PostgresErrorCodes.UniqueViolation || pg.SqlState == PostgresErrorCodes.SerializationFailure))
        { return Conflict(new { error = "Consent changed concurrently. Review before retrying." }); }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure)
        { return Conflict(new { error = "Consent changed concurrently. Review before retrying." }); }
    }

    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Create([FromBody] CampaignDraftRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var name = request.Name?.Trim();
        var brief = request.Brief?.Trim();
        if (request.RequestKey == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 256
            || string.IsNullOrWhiteSpace(brief) || brief.Length > 4000)
            return BadRequest(new { error = "A request key, campaign name and brief are required." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var id = await ActiveBusiness().SingleOrDefaultAsync(ct);
            if (id == Guid.Empty) return StatusCode(403);
            var existing = await Summaries(OwnDrafts(id).Where(x => x.Id == request.RequestKey))
                .SingleOrDefaultAsync(ct);
            if (existing is not null)
                return existing.Name == name && existing.Brief == brief
                    ? Ok(existing) : Conflict(new { error = "Draft request conflicts with an existing record." });
            if (await db.Campaigns.AnyAsync(x => x.Id == request.RequestKey, ct))
                return Conflict(new { error = "Draft request conflicts with an existing record." });

            var program = new AdvertiserProgram { Id = Guid.NewGuid(), AdvertiserId = id,
                Name = name, ExternalProgramId = DraftProgramMarker, Status = Draft };
            var opportunity = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id,
                Name = name, Description = brief, Status = Draft, AuthorityStatus = Unverified };
            var campaign = new Campaign { Id = request.RequestKey, AdvertiserOpportunityId = opportunity.Id,
                Name = name, Status = Draft, AuthorityStatus = Unverified, CreatedAt = DateTime.UtcNow };
            db.AdvertiserPrograms.Add(program);
            db.AdvertiserOpportunities.Add(opportunity);
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return StatusCode(201, new CampaignDraftSummary(campaign.Id, name, brief, Draft, campaign.CreatedAt));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            (pg.SqlState == PostgresErrorCodes.UniqueViolation || pg.SqlState == PostgresErrorCodes.SerializationFailure))
        {
            return Conflict(new { error = "Draft changed concurrently. Retry with the same request key." });
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return Conflict(new { error = "Draft changed concurrently. Retry with the same request key." });
        }
    }
}

public sealed record CampaignDraftRequest(Guid RequestKey, string Name, string Brief);
public sealed record CampaignDraftSummary(Guid CampaignId, string Name, string Brief, string Status, DateTime CreatedAt);

public sealed record CreativeHandoffPreviewRequest(Guid CampaignId);

public sealed record CampaignProviderConsentRequest(Guid CampaignId, string Action, string? ScopeSha256,
    string? NoticeVersion, Guid? RequestKey, Guid? ExpectedReceiptId);

public sealed record CampaignArtworkRequest(Guid CampaignId, string Action, Guid? CandidateId, Guid? ReviewId,
    string? ImageSha256, int? ExpectedVersion, Guid? RequestKey, string? Rationale,
    Guid? ReleaseId = null, bool? ConfirmedReceipt = null);

public sealed record CustomerStarterOfferRequest(Guid CampaignId,string Action,Guid? OfferId,string? OfferSha256,bool AcceptedExactTerms);
