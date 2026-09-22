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
    WeddingPlannerOrchestrationService orchestration,
    WeddingPlannerColorIntelligenceService colorIntelligence,
    WeddingPlannerCuratorOrchestrationService curator,
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

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("sessions/{sessionId:guid}/turns")]
    public async Task<ActionResult> ExecuteTurn(
        Guid sessionId,
        WeddingPlannerTurnRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await orchestration.ExecuteConciergeTurnAsync(
                sessionId,
                request.Body,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToTurnDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/agent-runs/{dto.AgentRunId}", dto);
        });
    }

    [HttpGet("sessions/{sessionId:guid}/agent-runs")]
    public async Task<ActionResult> ListSessionAgentRuns(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await orchestration.ListSessionAgentRunsAsync(
                sessionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToAgentRunDto).ToList());
        });
    }

    [HttpGet("agent-runs/{agentRunId:guid}")]
    public async Task<ActionResult> GetAgentRun(
        Guid agentRunId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await orchestration.GetAgentRunAsync(
                agentRunId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToAgentRunDto(result));
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces/{workspaceId:guid}/brand-dna/interpret")]
    public async Task<ActionResult> InterpretBrandDna(
        Guid workspaceId,
        InterpretWeddingPlannerBrandDnaRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await orchestration.InterpretBrandDnaAsync(
                workspaceId,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToBrandDnaDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/brand-dna/{dto.BrandDnaVersionId}", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/brand-dna")]
    public async Task<ActionResult> ListBrandDna(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await orchestration.ListBrandDnaAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(new WeddingPlannerBrandDnaListDto(
                result.WorkspaceId,
                result.AdvertiserId,
                result.CurrentApprovedBrandDnaVersionId,
                result.Versions.Select(ToBrandDnaDto).ToList()));
        });
    }

    [HttpGet("brand-dna/{brandDnaVersionId:guid}")]
    public async Task<ActionResult> GetBrandDna(
        Guid brandDnaVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await orchestration.GetBrandDnaAsync(
                brandDnaVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToBrandDnaDto(result));
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("brand-dna/{brandDnaVersionId:guid}/decisions")]
    public async Task<ActionResult> DecideBrandDna(
        Guid brandDnaVersionId,
        WeddingPlannerBrandDnaDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await orchestration.DecideBrandDnaAsync(
                brandDnaVersionId,
                request.Decision,
                request.Rationale,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToDecisionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/brand-dna/{dto.BrandDnaVersionId}/decisions", dto);
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces/{workspaceId:guid}/color-profiles/compute")]
    public async Task<ActionResult> ComputeColorProfile(
        Guid workspaceId,
        ComputeWeddingPlannerColorProfileRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await colorIntelligence.ComputeAsync(
                workspaceId,
                request.PrimaryHex,
                request.SecondaryHex,
                request.AccentHex,
                request.BackgroundHex,
                request.SurfaceHex,
                request.Notes,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToColorProfileDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/color-profiles/{dto.ColorProfileVersionId}", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/color-profiles")]
    public async Task<ActionResult> ListColorProfiles(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await colorIntelligence.ListAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(new WeddingPlannerColorProfileListDto(
                result.WorkspaceId,
                result.AdvertiserId,
                result.CurrentApprovedColorProfileVersionId,
                result.Versions.Select(ToColorProfileDto).ToList()));
        });
    }

    [HttpGet("color-profiles/{colorProfileVersionId:guid}")]
    public async Task<ActionResult> GetColorProfile(
        Guid colorProfileVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await colorIntelligence.GetAsync(
                colorProfileVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToColorProfileDto(result));
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("color-profiles/{colorProfileVersionId:guid}/decisions")]
    public async Task<ActionResult> DecideColorProfile(
        Guid colorProfileVersionId,
        WeddingPlannerColorProfileDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await colorIntelligence.DecideAsync(
                colorProfileVersionId,
                request.Decision,
                request.Rationale,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToColorProfileDecisionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/color-profiles/{dto.ColorProfileVersionId}/decisions", dto);
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces/{workspaceId:guid}/research-jobs")]
    public async Task<ActionResult> CreateResearchJob(
        Guid workspaceId,
        CreateWeddingPlannerResearchJobRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await curator.CreateResearchJobAsync(
                workspaceId,
                request.Topic,
                request.Objective,
                request.Questions,
                request.Geography,
                request.Language,
                request.AllowedDomains,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToResearchJobDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/research-jobs/{dto.ResearchJobId}", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/research-jobs")]
    public async Task<ActionResult> ListResearchJobs(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await curator.ListResearchJobsAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToResearchJobDto).ToList());
        });
    }

    [HttpGet("research-jobs/{researchJobId:guid}")]
    public async Task<ActionResult> GetResearchJob(
        Guid researchJobId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await curator.GetResearchJobAsync(
                researchJobId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToResearchJobDto(result));
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/research-reports")]
    public async Task<ActionResult> ListResearchReports(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await curator.ListResearchReportsAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(new WeddingPlannerResearchReportListDto(
                result.WorkspaceId,
                result.AdvertiserId,
                result.CurrentApprovedResearchReportVersionId,
                result.Versions.Select(ToResearchReportDto).ToList()));
        });
    }

    [HttpGet("research-reports/{researchReportVersionId:guid}")]
    public async Task<ActionResult> GetResearchReport(
        Guid researchReportVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await curator.GetResearchReportAsync(
                researchReportVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToResearchReportDto(result));
        });
    }

    [HttpGet("research-reports/{researchReportVersionId:guid}/contributions")]
    public async Task<ActionResult> ListResearchReportContributions(
        Guid researchReportVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await curator.ListContributionsAsync(
                researchReportVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToContributionDto).ToList());
        });
    }

    [HttpGet("research-reports/{researchReportVersionId:guid}/agent-runs")]
    public async Task<ActionResult> ListResearchReportAgentRuns(
        Guid researchReportVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await curator.ListReportAgentRunsAsync(
                researchReportVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToAgentRunDto).ToList());
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/agent-runs")]
    public async Task<ActionResult> ListWorkspaceAgentRuns(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await curator.ListWorkspaceAgentRunsAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToAgentRunDto).ToList());
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("research-reports/{researchReportVersionId:guid}/decisions")]
    public async Task<ActionResult> DecideResearchReport(
        Guid researchReportVersionId,
        WeddingPlannerResearchReportDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await curator.DecideAsync(
                researchReportVersionId,
                request.Decision,
                request.Rationale,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToResearchDecisionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/research-reports/{dto.ResearchReportVersionId}/decisions", dto);
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
        catch (WeddingPlannerProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message, agentRunId = ex.AgentRunId });
        }
        catch (WeddingPlannerResearchJobProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message, researchJobId = ex.ResearchJobId, errorCode = ex.ErrorCode });
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

    private static WeddingPlannerAgentRunDto ToAgentRunDto(WeddingPlannerAgentRunResult result) =>
        new(
            result.AgentRunId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.SessionId,
            result.LogicalRole,
            result.WorkerKey,
            result.PromptPackVersion,
            result.ProviderKey,
            result.ModelId,
            result.AdapterVersion,
            result.TriggerMessageId,
            result.OutputMessageId,
            result.OutputBrandDnaVersionId,
            result.WorkerProfileVersion,
            result.AssignedRolesJson,
            result.OutputResearchReportVersionId,
            result.RequestId,
            result.ProviderRequestId,
            result.SourceSystem,
            result.IdempotencyKey,
            result.Status,
            result.Outcome,
            result.ErrorCode,
            result.ErrorMessage,
            result.StartedAt,
            result.CompletedAt,
            result.PromptTokens,
            result.CompletionTokens,
            result.TotalTokens,
            result.EstimatedCostUsd,
            result.IsReplay);

    private static WeddingPlannerTurnDto ToTurnDto(WeddingPlannerTurnResult result) =>
        new(
            result.AgentRunId,
            result.SessionId,
            result.WorkspaceId,
            result.AdvertiserId,
            ToMessageDto(result.HumanMessage),
            result.PlannerMessage is null ? null : ToMessageDto(result.PlannerMessage),
            ToAgentRunDto(result.AgentRun),
            result.IsReplay);

    private static WeddingPlannerBrandDnaVersionDto ToBrandDnaDto(WeddingPlannerBrandDnaVersionResult result) =>
        new(
            result.BrandDnaVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.VersionNumber,
            result.SchemaVersion,
            result.DocumentJson,
            result.Summary,
            result.ProducingAgentRunId,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.CreatedAt,
            result.IsCurrentApproved,
            result.IsReplay);

    private static WeddingPlannerBrandDnaDecisionDto ToDecisionDto(WeddingPlannerBrandDnaDecisionResult result) =>
        new(
            result.DecisionId,
            result.BrandDnaVersionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Decision,
            result.ActorType,
            result.ActorLabel,
            result.Rationale,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToBrandDnaDto(result.Version),
            result.IsReplay);

    private static WeddingPlannerColorProfileVersionDto ToColorProfileDto(WeddingPlannerColorProfileVersionResult result) =>
        new(
            result.ColorProfileVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.VersionNumber,
            result.SchemaVersion,
            result.AlgorithmVersion,
            result.ApprovedBrandDnaVersionId,
            result.DocumentJson,
            result.Summary,
            result.InputJson,
            result.InputSha256,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ActorType,
            result.ActorLabel,
            result.CreatedAt,
            result.IsCurrentApproved,
            result.IsReplay);

    private static WeddingPlannerColorProfileDecisionDto ToColorProfileDecisionDto(
        WeddingPlannerColorProfileDecisionResult result) =>
        new(
            result.DecisionId,
            result.ColorProfileVersionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Decision,
            result.ActorType,
            result.ActorLabel,
            result.Rationale,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToColorProfileDto(result.Version),
            result.IsReplay);

    private static WeddingPlannerResearchJobDto ToResearchJobDto(WeddingPlannerResearchJobResult result) =>
        new(
            result.ResearchJobId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.Topic,
            result.Objective,
            result.Questions,
            result.Geography,
            result.Language,
            result.AllowedDomains,
            result.InputJson,
            result.InputSha256,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedColorProfileVersionId,
            result.ResearchProviderKey,
            result.ResearchAdapterVersion,
            result.ResearchProviderRequestId,
            result.ResearchWorkerKey,
            result.ResearchEstimatedCostUsd,
            result.SourceCatalogJson,
            result.ResearchAgentRunId,
            result.EvidenceAgentRunId,
            result.SynthesisRiskAgentRunId,
            result.OutputResearchReportVersionId,
            result.Status,
            result.ErrorCode,
            result.ErrorMessage,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ActorType,
            result.ActorLabel,
            result.StartedAt,
            result.CompletedAt,
            result.IsReplay);

    private static WeddingPlannerResearchReportVersionDto ToResearchReportDto(
        WeddingPlannerResearchReportVersionResult result) =>
        new(
            result.ResearchReportVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.VersionNumber,
            result.SchemaVersion,
            result.DocumentJson,
            result.Summary,
            result.ProducingResearchJobId,
            result.ProducingAgentRunId,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedColorProfileVersionId,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ActorType,
            result.ActorLabel,
            result.CreatedAt,
            result.IsCurrentApproved,
            result.EstimatedTotalCostUsd,
            result.IsReplay);

    private static WeddingPlannerResearchRoleContributionDto ToContributionDto(
        WeddingPlannerResearchRoleContributionResult result) =>
        new(
            result.ContributionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.ResearchReportVersionId,
            result.ResearchJobId,
            result.LogicalRole,
            result.ProducingAgentRunId,
            result.ContributionJson,
            result.CreatedAt);

    private static WeddingPlannerResearchReportDecisionDto ToResearchDecisionDto(
        WeddingPlannerResearchReportDecisionResult result) =>
        new(
            result.DecisionId,
            result.ResearchReportVersionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Decision,
            result.ActorType,
            result.ActorLabel,
            result.Rationale,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToResearchReportDto(result.Version),
            result.IsReplay);
}
