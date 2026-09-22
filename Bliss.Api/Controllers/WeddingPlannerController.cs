using Bliss.Api.Contracts;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/wedding-planner")]
public sealed class WeddingPlannerController(
    WeddingPlannerService weddingPlanner,
    WeddingPlannerAccess access) : ControllerBase
{
    [HttpGet("workspaces")]
    public async Task<ActionResult<IReadOnlyList<WeddingPlannerWorkspaceDto>>> ListWorkspaces(
        CancellationToken cancellationToken)
    {
        var actor = access.Resolve(User);
        var items = await weddingPlanner.ListWorkspacesAsync(
            actor.IsChapelStaff, actor.BoundAdvertiserId, cancellationToken);
        return Ok(items.Select(ToWorkspaceDto).ToList());
    }

    [HttpGet("workspaces/{workspaceId:guid}")]
    public async Task<ActionResult> GetWorkspace(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await weddingPlanner.GetWorkspaceAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            return Ok(ToWorkspaceDto(result));
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces")]
    public async Task<ActionResult> OpenWorkspace(
        OpenWeddingPlannerWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await weddingPlanner.OpenPrimaryWorkspaceAsync(
                request.AdvertiserId,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToWorkspaceDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/workspaces/{dto.WorkspaceId}", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/sessions")]
    public async Task<ActionResult> ListSessions(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await weddingPlanner.ListSessionsAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            return Ok(items.Select(ToSessionDto).ToList());
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces/{workspaceId:guid}/sessions")]
    public async Task<ActionResult> CreateSession(
        Guid workspaceId,
        CreateWeddingPlannerSessionRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await weddingPlanner.CreateSessionAsync(
                workspaceId,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToSessionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/sessions/{dto.SessionId}", dto);
        });
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<ActionResult> GetSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await weddingPlanner.GetSessionAsync(
                sessionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            return Ok(ToSessionDto(result));
        });
    }

    [HttpGet("sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult> ListMessages(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await weddingPlanner.ListMessagesAsync(
                sessionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            return Ok(items.Select(ToMessageDto).ToList());
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("sessions/{sessionId:guid}/messages")]
    public async Task<ActionResult> AppendMessage(
        Guid sessionId,
        AppendWeddingPlannerMessageRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await weddingPlanner.AppendMessageAsync(
                sessionId,
                request.ActorType,
                request.Body,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToMessageDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/messages/{dto.MessageId}", dto);
        });
    }

    [HttpGet("messages/{messageId:guid}")]
    public async Task<ActionResult> GetMessage(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await weddingPlanner.GetMessageAsync(
                messageId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            return Ok(ToMessageDto(result));
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/audit")]
    public async Task<ActionResult> ListAudit(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await weddingPlanner.ListAuditAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            return Ok(items.Select(x => new WeddingPlannerAuditEventDto(
                x.Id,
                x.AdvertiserId,
                x.WorkspaceId,
                x.SessionId,
                x.MessageId,
                x.Action,
                x.ActorType,
                x.ActorLabel,
                x.Outcome,
                x.RequestId,
                x.Detail,
                x.OccurredAt)).ToList());
        });
    }

    private WeddingPlannerActor RequireWrite()
    {
        var actor = access.Resolve(User);
        if (!access.CanWrite(actor))
        {
            throw new WeddingPlannerForbiddenException("The authenticated identity cannot write Wedding Planner records.");
        }

        return actor;
    }

    private async Task<ActionResult> ExecuteAsync(Func<Task<ActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (WeddingPlannerForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (WeddingPlannerNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static WeddingPlannerWorkspaceDto ToWorkspaceDto(WeddingPlannerWorkspaceResult result) =>
        new(
            result.WorkspaceId,
            result.AdvertiserId,
            result.AdvertiserName,
            result.IsPrimary,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.CreatedAt,
            result.UpdatedAt,
            result.IsReplay);

    private static WeddingPlannerSessionDto ToSessionDto(WeddingPlannerSessionResult result) =>
        new(
            result.SessionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.CreatedAt,
            result.UpdatedAt,
            result.MessageCount,
            result.IsReplay);

    private static WeddingPlannerMessageDto ToMessageDto(WeddingPlannerMessageResult result) =>
        new(
            result.MessageId,
            result.SessionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.SequenceNumber,
            result.ActorType,
            result.ActorLabel,
            result.Body,
            result.SourceSystem,
            result.IdempotencyKey,
            result.CreatedAt,
            result.IsReplay);
}
