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
[Route("api/wedding-planner/workspaces/{workspaceId:guid}/measurement")]
public sealed class CampaignMeasurementController(CampaignMeasurementService measurements, WeddingPlannerService planner,
    WeddingPlannerAccess access, BlissAuthenticationOptions authentication) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult> History(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    {
        await Workspace(workspaceId, ct);
        return Ok(new { history = await measurements.History(workspaceId, ct), canWrite = BlissAuthorization.CanWrite(User, authentication),
            canReview = BlissAuthorization.CanReview(User, authentication), canGiveFeedback = AdvertiserAuthority(), developmentIdentity = !authentication.Enabled });
    });
    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("observations")]
    public Task<ActionResult> Record(Guid workspaceId, RecordCampaignObservation request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, ct); var actor = access.Resolve(User);
        if (!BlissAuthorization.CanWrite(User, authentication)
            && (!AdvertiserAuthority() || !string.Equals(request.Kind?.Trim(), "ADVERTISER_FEEDBACK", StringComparison.OrdinalIgnoreCase)))
            throw new WeddingPlannerForbiddenException("Only staff can record validation or external reports; owning advertisers can record feedback.");
        var result = await measurements.Record(workspace, request, Subject(), actor.ActorLabel, ct);
        return StatusCode(result.Replay ? 200 : 201, new { observation = result.Row, isReplay = result.Replay, productionChanged = false, delivery = "NOT_SENT" });
    });
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("proposals")]
    public Task<ActionResult> Propose(Guid workspaceId, ProposeCampaignLearning request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, ct); var actor = access.Resolve(User);
        var result = await measurements.Propose(workspace, request, Subject(), actor.ActorLabel, ct);
        return StatusCode(result.Replay ? 200 : 201, new { proposal = result.Row, isReplay = result.Replay, productionChanged = false });
    });
    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("proposals/{proposalId:guid}/reviews")]
    public Task<ActionResult> Review(Guid workspaceId, Guid proposalId, ReviewCampaignLearning request, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await Workspace(workspaceId, ct); var actor = access.Resolve(User);
        var result = await measurements.Review(workspace, proposalId, request, Subject(), actor.ActorLabel, authentication.Enabled, ct);
        return StatusCode(result.Replay ? 200 : 201, new { review = result.Row, isReplay = result.Replay, productionChanged = false });
    });
    private bool AdvertiserAuthority() => !authentication.Enabled || User.IsInRole(authentication.AdvertiserRole) && access.Resolve(User).BoundAdvertiserId.HasValue;
    private string Subject() => !authentication.Enabled ? "DEV_OPERATOR" : User.FindFirst("sub")?.Value
        ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new WeddingPlannerForbiddenException("A stable authenticated subject is required.");
    private Task<WeddingPlannerWorkspaceResult> Workspace(Guid id, CancellationToken ct)
    {
        var actor = access.Resolve(User);
        return planner.GetWorkspaceAsync(id, actor.IsChapelStaff, actor.BoundAdvertiserId, actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
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
