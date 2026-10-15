using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/wedding-planner/workspaces/{workspaceId:guid}/production")]
public sealed class AcademyProductionController(WeddingPlannerService planner, WeddingPlannerAccess access,
    AcademyProductionService production, BlissAuthenticationOptions authentication) : ControllerBase
{
    [HttpGet("references")]
    public Task<ActionResult> References(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, false, ct); return Ok(production.References());
    });
    [HttpGet("briefs")]
    public Task<ActionResult> Briefs(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, false, ct); return Ok(await production.ListAsync(workspaceId, ct));
    });
    [HttpGet("briefs/{briefId:guid}")]
    public Task<ActionResult> History(Guid workspaceId, Guid briefId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, false, ct); return Ok(await production.HistoryAsync(workspaceId, briefId, ct));
    });
    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("briefs")]
    public Task<ActionResult> Create(Guid workspaceId, CreateProductionBrief request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, true, ct); var actor = access.Resolve(User);
        var result = await production.CreateAsync(workspace, request, actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
        return StatusCode(result.Replay ? 200 : 201, new { brief = result.Row, isReplay = result.Replay, campaignReady = false, delivery = "NOT_SENT" });
    });
    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(1_450_000)]
    [HttpPost("briefs/{briefId:guid}/candidates")]
    public Task<ActionResult> Submit(Guid workspaceId, Guid briefId, SubmitProductionCandidate request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, true, ct); var actor = access.Resolve(User);
        var result = await production.SubmitAsync(workspace, briefId, request, actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
        return StatusCode(result.Replay ? 200 : 201, new { candidate = result.Row, isReplay = result.Replay, campaignReady = false, delivery = "NOT_SENT" });
    });
    [HttpGet("briefs/{briefId:guid}/candidates/{candidateId:guid}/image")]
    public Task<ActionResult> Image(Guid workspaceId, Guid briefId, Guid candidateId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, false, ct);
        await production.BriefAsync(workspaceId, briefId, ct);
        var candidate = await production.CandidateAsync(briefId, candidateId, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(candidate.ImageBytes, "image/png");
    });
    // Only the Review policy can read evidence from an unrelated tenant.
    // These routes do not widen ordinary advertiser-scoped brief/image reads.
    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [HttpGet("review/briefs/{briefId:guid}")]
    public Task<ActionResult> ReviewHistory(Guid workspaceId, Guid briefId, CancellationToken ct) => Execute(async () =>
    {
        await ReviewerWorkspace(workspaceId, ct);
        return Ok(await production.HistoryAsync(workspaceId, briefId, ct));
    });
    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [HttpGet("review/briefs/{briefId:guid}/candidates/{candidateId:guid}/image")]
    public Task<ActionResult> ReviewImage(Guid workspaceId, Guid briefId, Guid candidateId, CancellationToken ct) => Execute(async () =>
    {
        await ReviewerWorkspace(workspaceId, ct);
        await production.BriefAsync(workspaceId, briefId, ct);
        var candidate = await production.CandidateAsync(briefId, candidateId, ct);
        Response.Headers.CacheControl = "private, no-store";
        return File(candidate.ImageBytes, "image/png");
    });
    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("briefs/{briefId:guid}/candidates/{candidateId:guid}/reviews")]
    public Task<ActionResult> Review(Guid workspaceId, Guid briefId, Guid candidateId, RecordProductionReview request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await ReviewerWorkspace(workspaceId, ct); var actor = access.Resolve(User);
        var result = await production.ReviewAsync(workspace, briefId, candidateId, request, actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
        return StatusCode(result.Replay ? 200 : 201, new { review = result.Row, isReplay = result.Replay, campaignReady = false, delivery = "NOT_SENT" });
    });
    // Reviewer-role bypass is restricted to ReviewPolicy endpoints above.
    // Other tenant GET/POST routes continue to use ordinary workspace binding.
    private Task<WeddingPlannerWorkspaceResult> ReviewerWorkspace(Guid id, CancellationToken ct)
    {
        var actor = access.Resolve(User);
        return planner.GetWorkspaceAsync(id,
            actor.IsChapelStaff || User.IsInRole(authentication.ReviewerRole),
            actor.BoundAdvertiserId, actor.ActorType, actor.ActorLabel,
            RequestCorrelation.Resolve(HttpContext), ct);
    }
    private async Task<WeddingPlannerWorkspaceResult> Workspace(Guid id, bool write, CancellationToken ct)
    {
        var actor = access.Resolve(User);
        if (write && !access.CanWrite(actor)) throw new WeddingPlannerForbiddenException("This identity cannot write production records.");
        return await planner.GetWorkspaceAsync(id, actor.IsChapelStaff, actor.BoundAdvertiserId,
            actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
    }
    private async Task<ActionResult> Execute(Func<Task<ActionResult>> action)
    {
        try { return await action(); }
        catch (WeddingPlannerNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (WeddingPlannerForbiddenException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (WeddingPlannerConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
