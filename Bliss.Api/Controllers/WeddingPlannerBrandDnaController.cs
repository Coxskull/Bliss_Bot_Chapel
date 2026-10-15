using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

public sealed record SaveBrandDnaRequest(BrandDnaFields Fields, int ExpectedVersion, string IdempotencyKey);
public sealed record ConfirmBrandDnaRequest(int ExpectedVersion, string IdempotencyKey);

[ApiController]
[Route("api/wedding-planner/workspaces/{workspaceId:guid}/brand-dna")]
public sealed class WeddingPlannerBrandDnaController(WeddingPlannerService planner,
    WeddingPlannerBrandDnaService brands, WeddingPlannerAccess access) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult> Read(Guid workspaceId, CancellationToken ct) => Execute(async () =>
    {
        await AuthorizeWorkspace(workspaceId, false, ct);
        return Ok(await brands.ReadAsync(workspaceId, ct));
    });

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost]
    public Task<ActionResult> Save(Guid workspaceId, SaveBrandDnaRequest request, CancellationToken ct) =>
        Append(workspaceId, request.Fields, request.ExpectedVersion, request.IdempotencyKey, false, ct);

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("confirm")]
    public Task<ActionResult> Confirm(Guid workspaceId, ConfirmBrandDnaRequest request, CancellationToken ct) =>
        Append(workspaceId, null, request.ExpectedVersion, request.IdempotencyKey, true, ct);

    private Task<ActionResult> Append(Guid workspaceId, BrandDnaFields? fields, int version, string key,
        bool confirm, CancellationToken ct) => Execute(async () =>
    {
        var workspace = await AuthorizeWorkspace(workspaceId, true, ct);
        var actor = access.Resolve(User);
        var result = await brands.AppendAsync(workspace, fields, version, key, confirm,
            actor.ActorType, actor.ActorLabel, RequestCorrelation.Resolve(HttpContext), ct);
        return result.IsReplay ? Ok(new { result.Revision, result.IsReplay })
            : StatusCode(201, new { result.Revision, result.IsReplay });
    });

    private async Task<WeddingPlannerWorkspaceResult> AuthorizeWorkspace(Guid id, bool write, CancellationToken ct)
    {
        var actor = access.Resolve(User);
        if (write && !access.CanWrite(actor)) throw new WeddingPlannerForbiddenException("This identity cannot write brand intake.");
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
