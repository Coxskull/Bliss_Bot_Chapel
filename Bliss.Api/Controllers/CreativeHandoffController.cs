using System.Security.Claims;
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
[Route("api/wedding-planner/workspaces/{workspaceId:guid}/handoff")]
public sealed class CreativeHandoffController(CreativeHandoffService handoff, WeddingPlannerService planner,
    WeddingPlannerAccess access, BlissAuthenticationOptions authentication) : ControllerBase
{
    [HttpGet("options")]
    public Task<ActionResult> Options(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, ct);
        return Ok(new { options = await handoff.Options(workspace, ct),
            canAdvertiserApprove = AdvertiserAuthority(workspace), canAlphaQa = BlissAuthorization.CanReview(User, authentication),
            canPlan = BlissAuthorization.CanWrite(User, authentication), developmentIdentity = !authentication.Enabled });
    });
    [HttpGet("candidates/{candidateId:guid}/approvals")]
    public Task<ActionResult> Decisions(Guid workspaceId, Guid candidateId, CancellationToken ct) => Execute(async () =>
    { await Workspace(workspaceId, ct); return Ok(await handoff.Decisions(workspaceId, candidateId, ct)); });
    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("candidates/{candidateId:guid}/approvals")]
    public Task<ActionResult> Decide(Guid workspaceId, Guid candidateId, DecideCandidateApproval request, CancellationToken ct) => Execute(async () =>
    {
        // Only QA and inventory-fit decisions may be made by a reviewer without
        // an advertiser binding. An ADVERTISER decision never uses this bypass.
        var advertiserStage = string.Equals(request.Stage?.Trim(), "ADVERTISER", StringComparison.OrdinalIgnoreCase);
        if (!advertiserStage && !BlissAuthorization.CanReview(User, authentication))
            throw new WeddingPlannerForbiddenException("Alpha QA and inventory fit require reviewer authority.");
        var workspace = advertiserStage ? await Workspace(workspaceId, ct) : await ReviewerWorkspace(workspaceId, ct);
        if (advertiserStage && !AdvertiserAuthority(workspace))
            throw new WeddingPlannerForbiddenException("Only this advertiser's authenticated principal can record advertiser approval.");
        var actor = access.Resolve(User);
        var result = await handoff.Decide(workspace, candidateId, request, Subject(), actor.ActorLabel, authentication.Enabled, ct);
        return StatusCode(result.Replay ? 200 : 201, new { decision = result.Row, isReplay = result.Replay, delivery = "NOT_SENT" });
    });
    [HttpGet("plans")]
    public Task<ActionResult> Plans(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    { await Workspace(workspaceId, ct); return Ok(await handoff.Handoffs(workspaceId, ct)); });
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("campaigns")]
    public Task<ActionResult> CreateCampaign(Guid workspaceId, CreateHandoffCampaign request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, ct); var actor = access.Resolve(User);
        var result = await handoff.CreateCampaign(workspace, request, actor.ActorLabel, ct);
        return StatusCode(result.Replay ? 200 : 201, new { campaign = result.Campaign, isReplay = result.Replay });
    });
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("plans")]
    public Task<ActionResult> Plan(Guid workspaceId, PlanCreativeHandoff request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, ct); var actor = access.Resolve(User);
        var result = await handoff.Plan(workspace, request, Subject(), actor.ActorLabel, authentication.Enabled, ct);
        return StatusCode(result.Replay ? 200 : 201, new { handoff = result.Row, isReplay = result.Replay, delivery = "NOT_SENT", campaignActivated = false });
    });
    private Task<WeddingPlannerWorkspaceResult> ReviewerWorkspace(Guid id, CancellationToken ct)
    {
        // This helper is deliberately called only by the explicitly checked
        // ALPHA_QA / INVENTORY_FIT decision branch, not advertiser/planning APIs.
        var actor = access.Resolve(User);
        return planner.GetWorkspaceAsync(id,
            actor.IsChapelStaff || User.IsInRole(authentication.ReviewerRole),
            actor.BoundAdvertiserId, actor.ActorType, actor.ActorLabel, null, ct);
    }
    private bool AdvertiserAuthority(WeddingPlannerWorkspaceResult workspace) => !authentication.Enabled
        || User.IsInRole(authentication.AdvertiserRole) && access.Resolve(User).BoundAdvertiserId == workspace.AdvertiserId;
    private string Subject() => !authentication.Enabled ? "DEV_OPERATOR" : User.FindFirst("sub")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new WeddingPlannerForbiddenException("A stable authenticated subject is required.");
    private Task<WeddingPlannerWorkspaceResult> Workspace(Guid id, CancellationToken ct)
    {
        var actor = access.Resolve(User);
        return planner.GetWorkspaceAsync(id, actor.IsChapelStaff, actor.BoundAdvertiserId, actor.ActorType, actor.ActorLabel, null, ct);
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
