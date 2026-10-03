using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.WeddingPlanner;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/creative")]
public sealed class CreativeApprovalController(CreativeApprovalService creative) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read([FromQuery] Guid? workspaceId, CancellationToken cancellationToken)
    {
        try
        {
            var board = await creative.ReadAsync(workspaceId, cancellationToken);
            return Ok(Body(board, null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Refused(ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Decide([FromBody] CreativeDecisionRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await creative.DecideAsync(
                request?.WorkspaceId ?? Guid.Empty,
                request?.Title,
                request?.Decision,
                request?.ActorType,
                request?.IdempotencyKey,
                cancellationToken);
            var board = await creative.ReadAsync(write.Decision.WorkspaceId, cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(Refused(ex.Message));
        }
    }

    private static object Refused(string error) => new
    {
        error,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        campaignReady = false,
        matchWritten = false,
        priceInvented = false,
        modelCalls = 0
    };

    private static object Body(CreativeBoard board, CreativeWrite? write) => new
    {
        notice = write?.Decision.Notice ?? CreativeApproval.Notice,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        campaignReady = false,
        matchWritten = false,
        priceInvented = false,
        modelCalls = 0,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        workspaceId = board.Workspace?.Id,
        advertiserName = board.Workspace?.AdvertiserName ?? string.Empty,
        workspaces = board.Workspaces.Select(item => new
        {
            id = item.Id,
            advertiserName = item.AdvertiserName
        }),
        decisions = board.Decisions.Select(item => new
        {
            item.WorkspaceId,
            item.Title,
            item.Status,
            item.ActorType,
            item.CampaignReady,
            item.MatchWritten,
            item.PriceInvented,
            item.ModelCalls,
            item.Delivery
        }),
        messages = board.Messages.Select(item => new
        {
            item.WorkspaceId,
            item.ActorType,
            item.Body
        }),
        audits = board.Audits.Select(item => new
        {
            item.WorkspaceId,
            item.Action,
            item.ActorType,
            item.Outcome
        })
    };
}

public sealed record CreativeDecisionRequest(
    Guid WorkspaceId,
    string? Title,
    string? Decision,
    string? ActorType,
    string? IdempotencyKey);
