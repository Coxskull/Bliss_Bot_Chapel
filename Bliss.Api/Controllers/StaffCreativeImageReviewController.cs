using System.Data;
using Bliss.Domain.Entities;
using Npgsql;
using System.Security.Cryptography;
using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.CreativeAcademy;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/internal/creative-image-review")]
public sealed class StaffCreativeImageReviewController(BlissDbContext db, BlissAuthenticationOptions auth,
    WeddingPlannerService planner, AcademyProductionService production) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Review([FromBody] StaffImageReviewRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        if (!auth.StaffCreativePreparationRequested || !auth.AdvertiserCreativeProviderConsentRequested
            || !StaffCreativeReadinessController.IsStaff(User, auth)) return StatusCode(403);
        if (request.CampaignId == Guid.Empty || request.WorkspaceId == Guid.Empty || request.BriefId == Guid.Empty
            || request.RunId == Guid.Empty || request.BriefSha256 is not { Length: 64 }
            || request.Action is not ("PREVIEW" or "STATUS" or "ATTACH" or "REVIEW" or "RELEASE")
            || (request.Action == "REVIEW" && (request.Review is null || request.CandidateId is null))
            || (request.Action != "REVIEW" && request.Review is not null)
            || (request.Action is not ("REVIEW" or "RELEASE") && request.CandidateId is not null)
            || (request.Action == "RELEASE" && (request.CandidateId is null || request.ReconciliationId is null
                || request.ReconciliationId == Guid.Empty || !request.CurrentPaymentVerified || !request.ScopeVerified)))
            return BadRequest();
        var grant = await CampaignImageAuthorization.Current(db, request.WorkspaceId, request.BriefId, ct);
        if (grant is null || grant.CampaignId != request.CampaignId || grant.CampaignDigest != request.BriefSha256)
            return Conflict(new { error = "Current campaign and consent receipt required." });
        var run = await db.ControlledAiRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.RunId
            && x.WorkspaceId == request.WorkspaceId && x.BriefId == request.BriefId
            && x.IdempotencyKey == CampaignImageAuthorization.RunKey(request.BriefId)
            && x.Kind == "IMAGE" && x.Status == "SUCCEEDED_PENDING_REVIEW", ct);
        if (run is null || run.OutputImage is null || run.OutputImage.Length > CandidatePng.MaxGeneratedBytes
            || run.CostStatus != "PROVIDER_REPORTED" || string.IsNullOrWhiteSpace(run.ProviderRequestId)
            || !run.ActualUsd.HasValue || run.ActualUsd.Value < 0 || run.ActualUsd.Value > run.ReservedUsd
            || run.Model != AdvertiserCampaignDraftsController.ProviderConsentModel
            || !string.Equals(run.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase)
            || ControlledAiService.Hash(run.InputSnapshotJson) != run.InputSha256
            || CampaignImageAuthorization.ProviderInput(run.InputSnapshotJson, grant) is null
            || Convert.ToHexString(SHA256.HashData(run.OutputImage)).ToLowerInvariant() != run.OutputSha256)
            return Conflict(new { error = "Verified successful image and provider receipt required." });
        try
        {
            CandidatePng.ValidateGenerated(run.OutputImage);
            if (request.Action == "PREVIEW") return File(run.OutputImage, "image/png");
            var subject = User.FindFirst("sub")!.Value;
            var workspace = await planner.GetWorkspaceAsync(request.WorkspaceId, true, null, "OPERATOR", subject,
                RequestCorrelation.Resolve(HttpContext), ct);
            var key = "ai-run-" + run.Id;
            var candidate = await db.AcademyProductionCandidates.AsNoTracking()
                .Where(x => x.BriefId == request.BriefId && x.IdempotencyKey == key)
                .Select(x => new { x.Id, x.Version, x.ExpectedVersion, x.ImageSha256 }).SingleOrDefaultAsync(ct);
            if (candidate is not null && candidate.ImageSha256 != run.OutputSha256)
                return Conflict(new { error = "Candidate and image receipt differ." });
            if (request.Action == "ATTACH" && candidate is null)
            {
                // A single immutable candidate per exact run. No client-supplied image bytes.
                var version = await db.AcademyProductionCandidates.Where(x => x.BriefId == request.BriefId)
                    .Select(x => (int?)x.Version).MaxAsync(ct) ?? 0;
                var saved = await production.SubmitAsync(workspace, request.BriefId,
                    new SubmitProductionCandidate(version, "AI advertising draft", "Controlled image run " + run.Id,
                        "", key), "OPERATOR", subject, RequestCorrelation.Resolve(HttpContext), ct, run.OutputImage);
                candidate = new { saved.Row.Id, saved.Row.Version, saved.Row.ExpectedVersion, saved.Row.ImageSha256 };
            }
            if (request.Action == "RELEASE")
            {
                if (!auth.StarterArtworkDeliveryRequested || !auth.StarterPaymentReconciliationRequested
                    || !auth.AdvertiserCreativeArtworkRequested) return StatusCode(403);
                if (candidate is null || candidate.Id != request.CandidateId) return Conflict();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var activeGrant = await CampaignImageAuthorization.Current(db, request.WorkspaceId, request.BriefId, ct);
                if (activeGrant is null || CampaignImageAuthorization.ProviderInput(run.InputSnapshotJson, activeGrant) is null) return Conflict();
                var latestCandidate = await db.AcademyProductionCandidates.AsNoTracking().Where(x => x.BriefId == request.BriefId)
                    .OrderByDescending(x => x.Version).Select(x => new { x.Id, x.ImageSha256 }).FirstOrDefaultAsync(ct);
                if (latestCandidate?.Id != candidate.Id || latestCandidate.ImageSha256 != run.OutputSha256) return Conflict();
                var review = await db.AcademyProductionReviews.AsNoTracking().Where(x => x.CandidateId == candidate.Id)
                    .OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
                var approval = await db.CandidateApprovalDecisions.AsNoTracking().Where(x => x.CandidateId == candidate.Id
                    && x.ScopeKey == "ADVERTISER").OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
                if (review is null || review.Decision != "PASS" || review.CriticalDefect
                    || new[] { review.Customization, review.QualityParity, review.Originality, review.GeographicAuthenticity }.Any(x => x != "PASS")
                    || approval is null || approval.ReviewId != review.Id || approval.Decision != "APPROVE"
                    || approval.ActorSubject == subject || approval.ActorSubject == review.ActorLabel)
                    return Conflict(new { error = "Latest staff PASS and independent advertiser approval required." });
                var payment = await StarterArtworkDelivery.Reconciliation(db, workspace.AdvertiserId,
                    workspace.WorkspaceId, request.CampaignId, request.ReconciliationId!.Value, ct);
                if (payment is null) return Conflict(new { error = "Matching current reconciled order required." });
                var detail = payment.Id.ToString("N") + StarterArtworkDelivery.Suffix(request.CampaignId,candidate.Id,review.Id,approval.Id,candidate.ImageSha256);
                var releaseId = StarterArtworkDelivery.StableId(StarterArtworkDelivery.ReleaseAction,payment.Id.ToString("N"));
                var existing = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == releaseId,ct);
                if (existing is not null && (existing.Detail != detail || existing.Action != StarterArtworkDelivery.ReleaseAction
                    || existing.AdvertiserId != workspace.AdvertiserId || existing.WorkspaceId != workspace.WorkspaceId)) return Conflict();
                if (existing is null)
                {
                    db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent { Id=releaseId,AdvertiserId=workspace.AdvertiserId,
                        WorkspaceId=workspace.WorkspaceId,Action=StarterArtworkDelivery.ReleaseAction,ActorType="OPERATOR",
                        ActorLabel=subject,Outcome="RELEASED",Detail=detail,OccurredAt=DateTime.UtcNow });
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
                return Ok(new {runId=run.Id,candidateId=candidate.Id,imageSha256=candidate.ImageSha256,releaseId,
                    reviewVersion=review.Version,release="ARTWORK_DOWNLOAD_AVAILABLE",delivery="DOWNLOAD_NOT_CONFIRMED",dispatchAuthorized=false});
            }
            if (request.Action == "REVIEW")
            {
                if (candidate is null || candidate.Id != request.CandidateId)
                    return Conflict(new { error = "Attach and review this exact candidate." });
                var saved = await production.ReviewAsync(workspace, request.BriefId, candidate.Id, request.Review!,
                    "OPERATOR", subject, RequestCorrelation.Resolve(HttpContext), ct);
                return Ok(new { runId = run.Id, candidateId = candidate.Id, imageSha256 = run.OutputSha256,
                    reviewId = saved.Row.Id, reviewVersion = saved.Row.Version, decision = saved.Row.Decision,
                    release = "HELD", delivery = "NOT_SENT", dispatchAuthorized = false });
            }
            var latestReview=candidate is null?null:await db.AcademyProductionReviews.AsNoTracking().Where(x=>x.CandidateId==candidate.Id)
                .OrderByDescending(x=>x.Version).Select(x=>new{x.Id,x.Version,x.Decision}).FirstOrDefaultAsync(ct);
            return Ok(new { runId = run.Id, candidateId = candidate?.Id, imageSha256 = run.OutputSha256,
                candidateVersion = candidate?.Version, reviewVersion=latestReview?.Version??0,reviewId=latestReview?.Id,decision=latestReview?.Decision,actualUsd = run.ActualUsd,
                release = "HELD", delivery = "NOT_SENT", dispatchAuthorized = false });
        }
        catch (WeddingPlannerNotFoundException) { return NotFound(); }
        catch (WeddingPlannerForbiddenException) { return StatusCode(403); }
        catch (WeddingPlannerConflictException) { return Conflict(new { error = "Candidate or review changed. Refresh." }); }
        catch (InvalidOperationException) { return BadRequest(new { error = "Invalid image or review requirements." }); }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure) { return Conflict(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && (pg.SqlState == PostgresErrorCodes.SerializationFailure || pg.SqlState == PostgresErrorCodes.UniqueViolation)) { return Conflict(); }
    }
}

public sealed record StaffImageReviewRequest(Guid CampaignId, string BriefSha256, Guid WorkspaceId, Guid BriefId,
    Guid RunId, string Action, Guid? CandidateId, RecordProductionReview? Review,
    Guid? ReconciliationId = null, bool CurrentPaymentVerified = false, bool ScopeVerified = false);
