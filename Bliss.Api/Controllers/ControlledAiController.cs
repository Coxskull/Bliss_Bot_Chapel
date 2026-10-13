using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Controllers;

public sealed record AttachAiCandidate(int ExpectedVersion, string Label);
[ApiController]
[Route("api/wedding-planner/workspaces/{workspaceId:guid}/ai")]
public sealed class ControlledAiController(ControlledAiService ai, WeddingPlannerService planner,
    WeddingPlannerAccess access, AcademyProductionService production, BlissDbContext db) : ControllerBase
{
    [HttpGet("readiness")]
    public Task<ActionResult> Readiness(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    { await Workspace(workspaceId, false, ct); return Ok(await ai.Readiness(ct)); });
    [HttpGet("runs")]
    public Task<ActionResult> List(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    { await Workspace(workspaceId, false, ct); return Ok(await ai.List(workspaceId, ct)); });
    [HttpGet("runs/{runId:guid}")]
    public Task<ActionResult> Detail(Guid workspaceId, Guid runId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, false, ct); var run = await ai.Run(workspaceId, runId, ct);
        return Ok(new { run, events = await ai.Events(runId, ct), campaignReady = false, delivery = "NOT_SENT" });
    });
    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("runs")]
    public Task<ActionResult> Submit(Guid workspaceId, SubmitAiRun request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, true, ct); var actor = access.Resolve(User);
        var result = await ai.Submit(workspace, request, actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
        return StatusCode(result.Replay ? 200 : 202, new { run = result.Run, isReplay = result.Replay, campaignReady = false, delivery = "NOT_SENT" });
    });
    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("runs/{runId:guid}/cancel")]
    public Task<ActionResult> Cancel(Guid workspaceId, Guid runId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, true, ct); var actor = access.Resolve(User);
        return Ok(await ai.Cancel(workspaceId, runId, actor.ActorLabel, ct));
    });
    [HttpGet("runs/{runId:guid}/image")]
    public Task<ActionResult> Image(Guid workspaceId, Guid runId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, false, ct); var run = await ai.Run(workspaceId, runId, ct);
        if (run.Status != "SUCCEEDED_PENDING_REVIEW" || run.Kind != "IMAGE" || run.OutputImage is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store"; return File(run.OutputImage, "image/png");
    });
    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("runs/{runId:guid}/attach-candidate")]
    public Task<ActionResult> Attach(Guid workspaceId, Guid runId, AttachAiCandidate request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, true, ct); var actor = access.Resolve(User);
        var run = await ai.Run(workspaceId, runId, ct);
        if (run.AttachedCandidateId.HasValue)
        {
            var saved = await production.CandidateAsync(run.BriefId!.Value, run.AttachedCandidateId.Value, ct);
            if (saved.Label != (request.Label ?? "").Trim()) throw new WeddingPlannerConflictException("This image is already attached with a different label. Its candidate is immutable.");
            return Ok(new { candidate = saved, isReplay = true });
        }
        if (run.Status != "SUCCEEDED_PENDING_REVIEW" || run.Kind != "IMAGE" || run.OutputImage is null || run.BriefId is null)
            throw new InvalidOperationException("Only a successful image draft can be attached for human review.");
        var submitted = await production.SubmitAsync(workspace, run.BriefId.Value,
            new SubmitProductionCandidate(request.ExpectedVersion, request.Label, $"AI draft from run {run.Id}; pending instructor assessment.", Convert.ToBase64String(run.OutputImage), "ai-run-" + run.Id),
            actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
        run.AttachedCandidateId = submitted.Row.Id;
        var attachment = new ControlledAiEvent { Id = Guid.NewGuid(), RunId = run.Id, Action = "IMAGE_ATTACHED", ActorLabel = actor.ActorLabel, CreatedAt = DateTime.UtcNow };
        db.Add(attachment);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { db.ChangeTracker.Clear(); }
        return Ok(new { candidate = submitted.Row, isReplay = submitted.Replay, campaignReady = false, delivery = "NOT_SENT" });
    });
    private async Task<WeddingPlannerWorkspaceResult> Workspace(Guid id, bool write, CancellationToken ct)
    {
        var actor = access.Resolve(User);
        if (write && !access.CanWrite(actor)) throw new WeddingPlannerForbiddenException("This identity cannot request AI work.");
        return await planner.GetWorkspaceAsync(id, actor.IsChapelStaff, actor.BoundAdvertiserId, actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
    }
    private async Task<ActionResult> Execute(Func<Task<ActionResult>> action)
    {
        try { return await action(); }
        catch (WeddingPlannerNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (WeddingPlannerForbiddenException ex) { return StatusCode(403, new { error = ex.Message }); }
        catch (WeddingPlannerConflictException ex) { return Conflict(new { error = ex.Message }); }
        catch (AiQuotaException ex) { return StatusCode(429, new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
