using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/internal/creative-image-reservation")]
public sealed class StaffCreativeImageReservationController(BlissDbContext db, BlissAuthenticationOptions auth,
    WeddingPlannerService planner, ControlledAiService ai) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(2048)]
    public async Task<IActionResult> Reserve([FromBody] StaffImageReservationRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.StaffCreativePreparationRequested || !auth.AdvertiserCreativeProviderConsentRequested
            || !StaffCreativeReadinessController.IsStaff(User, auth)) return StatusCode(403);
        if (request.CampaignId == Guid.Empty || request.WorkspaceId == Guid.Empty || request.BriefId == Guid.Empty
            || request.BriefSha256 is not { Length: 64 } || request.Action is not ("REVIEW" or "RESERVE")
            || (request.Action == "RESERVE" && (request.ConsentReceiptId is null || request.ConsentReceiptId == Guid.Empty
                || !request.OperatorBudgetApproved))) return BadRequest();
        var grant = await CampaignImageAuthorization.Current(db, request.WorkspaceId, request.BriefId, ct);
        if (grant is null || grant.CampaignId != request.CampaignId || grant.CampaignDigest != request.BriefSha256)
            return Conflict(new { error = "Current campaign recipe and active advertiser provider consent required." });
        if (request.Action == "RESERVE" && request.ConsentReceiptId != grant.ReceiptId)
            return Conflict(new { error = "Consent changed. Review the current grant before reserving." });
        var subject = User.FindFirst("sub")!.Value;
        var key = CampaignImageAuthorization.RunKey(request.BriefId);
        var existing = await db.ControlledAiRuns.AsNoTracking().SingleOrDefaultAsync(x => x.WorkspaceId == request.WorkspaceId
            && x.IdempotencyKey == key, ct);
        if (existing is not null)
        {
            if (existing.Kind != "IMAGE" || existing.BriefId != request.BriefId
                || CampaignImageAuthorization.ProviderInput(existing.InputSnapshotJson, grant) is null)
                return Conflict(new { error = "A prior image reservation is bound to a different receipt; no additional run can be reserved." });
            return Ok(Receipt(existing.Id, existing.WorkspaceId, request.BriefId, existing.InputSha256, existing.ReservedUsd, existing.Status, grant.ReceiptId));
        }
        if (request.Action == "REVIEW")
            return Ok(new { campaignId = request.CampaignId, consentReceiptId = grant.ReceiptId,
                imagePilotConfigured = ai.RestrictedCampaignPilotConfigured, readiness = await ai.Readiness(ct),
                dispatchAuthorized = false, release = "HELD", delivery = "NOT_SENT" });
        if (request.ConsentReceiptId != grant.ReceiptId || !ai.RestrictedCampaignPilotConfigured)
            return Conflict(new { error = "Consent changed or exact pilot configuration unavailable." });
        try
        {
            var workspace = await planner.GetWorkspaceAsync(request.WorkspaceId, true, null, "OPERATOR", subject,
                RequestCorrelation.Resolve(HttpContext), ct);
            var result = await ai.Submit(workspace, new SubmitAiRun("IMAGE", null, request.BriefId, null, "", key, true),
                "OPERATOR", subject, RequestCorrelation.Resolve(HttpContext), ct, grant.ReceiptId);
            return StatusCode(result.Replay ? 200 : 202, Receipt(result.Run.Id, result.Run.WorkspaceId, request.BriefId,
                result.Run.InputSha256, result.Run.ReservedUsd, result.Run.Status, grant.ReceiptId));
        }
        catch (AiQuotaException) { return StatusCode(429, new { error = "Pilot budget or billing reconciliation prevents reservation." }); }
        catch (WeddingPlannerNotFoundException) { return NotFound(); }
        catch (WeddingPlannerForbiddenException) { return StatusCode(403); }
        catch (WeddingPlannerConflictException) { return Conflict(new { error = "Campaign or reservation changed. Review before retry." }); }
        catch (InvalidOperationException) { return Conflict(new { error = "Image reservation prerequisites not satisfied." }); }
    }
    private static object Receipt(Guid runId, Guid workspaceId, Guid briefId, string inputSha256, decimal reservedUsd,
        string status, Guid consentReceiptId) => new { runId, workspaceId, briefId, inputSha256, reservedUsd, status,
            consentReceiptId, dispatchAuthorized = false, release = "HELD", delivery = "NOT_SENT" };
}
public sealed record StaffImageReservationRequest(Guid CampaignId, string BriefSha256, Guid WorkspaceId,
    Guid BriefId, string Action, Guid? ConsentReceiptId, bool OperatorBudgetApproved);
