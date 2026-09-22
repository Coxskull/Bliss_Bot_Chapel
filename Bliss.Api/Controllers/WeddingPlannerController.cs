using Bliss.Api.Contracts;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.WeddingPlanner;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/wedding-planner")]
public sealed class WeddingPlannerController(
    WeddingPlannerService weddingPlanner,
    WeddingPlannerOrchestrationService orchestration,
    WeddingPlannerColorIntelligenceService colorIntelligence,
    WeddingPlannerCuratorOrchestrationService curator,
    WeddingPlannerConceptWorkshopOrchestrationService workshop,
    WeddingPlannerCreativeDepartmentOrchestrationService creative,
    WeddingPlannerQaReviewOrchestrationService qaReview,
    WeddingPlannerCampaignReadinessOrchestrationService campaignReadiness,
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

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces/{workspaceId:guid}/workshop-jobs")]
    public async Task<ActionResult> CreateWorkshopJob(
        Guid workspaceId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            if (body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new InvalidOperationException("Workshop job body is required.");
            }

            var root = JsonNode.Parse(body.GetRawText())
                ?? throw new InvalidOperationException("Workshop job body is required.");
            WeddingPlannerConceptWorkshopValidation.RejectForbiddenBriefFields(root);

            var request = body.Deserialize<CreateWeddingPlannerWorkshopJobRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Workshop job body is invalid.");

            var result = await workshop.CreateWorkshopJobAsync(
                workspaceId,
                request.Objective,
                request.CampaignGoal,
                request.AudienceFocus,
                request.ChannelFormat,
                request.Deliverables,
                request.Cta,
                request.Constraints,
                request.CanvasWidth,
                request.CanvasHeight,
                root,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToWorkshopJobDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/workshop-jobs/{dto.WorkshopJobId}", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/workshop-jobs")]
    public async Task<ActionResult> ListWorkshopJobs(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await workshop.ListWorkshopJobsAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToWorkshopJobDto).ToList());
        });
    }

    [HttpGet("workshop-jobs/{workshopJobId:guid}")]
    public async Task<ActionResult> GetWorkshopJob(
        Guid workshopJobId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await workshop.GetWorkshopJobAsync(
                workshopJobId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToWorkshopJobDto(result));
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/concept-packages")]
    public async Task<ActionResult> ListConceptPackages(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await workshop.ListConceptPackagesAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(new WeddingPlannerConceptPackageListDto(
                result.WorkspaceId,
                result.AdvertiserId,
                result.CurrentApprovedConceptPackageVersionId,
                result.Versions.Select(ToConceptPackageDto).ToList()));
        });
    }

    [HttpGet("concept-packages/{conceptPackageVersionId:guid}")]
    public async Task<ActionResult> GetConceptPackage(
        Guid conceptPackageVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await workshop.GetConceptPackageAsync(
                conceptPackageVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToConceptPackageDto(result));
        });
    }

    [HttpGet("concept-packages/{conceptPackageVersionId:guid}/contributions")]
    public async Task<ActionResult> ListConceptPackageContributions(
        Guid conceptPackageVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await workshop.ListContributionsAsync(
                conceptPackageVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToConceptContributionDto).ToList());
        });
    }

    [HttpGet("concept-packages/{conceptPackageVersionId:guid}/agent-runs")]
    public async Task<ActionResult> ListConceptPackageAgentRuns(
        Guid conceptPackageVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await workshop.ListPackageAgentRunsAsync(
                conceptPackageVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToAgentRunDto).ToList());
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("concept-packages/{conceptPackageVersionId:guid}/decisions")]
    public async Task<ActionResult> DecideConceptPackage(
        Guid conceptPackageVersionId,
        WeddingPlannerConceptPackageDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await workshop.DecideAsync(
                conceptPackageVersionId,
                request.Decision,
                request.Rationale,
                request.SelectedConceptId,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToConceptDecisionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/concept-packages/{dto.ConceptPackageVersionId}/decisions", dto);
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces/{workspaceId:guid}/creative-production-jobs")]
    public async Task<ActionResult> CreateCreativeProductionJob(
        Guid workspaceId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            if (body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new InvalidOperationException("Creative production job body is required.");
            }

            var root = JsonNode.Parse(body.GetRawText())
                ?? throw new InvalidOperationException("Creative production job body is required.");
            WeddingPlannerCreativeDepartmentValidation.RejectForbiddenBriefFields(root);

            var request = body.Deserialize<CreateWeddingPlannerCreativeProductionJobRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Creative production job body is invalid.");

            var result = await creative.CreateCreativeProductionJobAsync(
                workspaceId,
                request.JobKind,
                request.Objective,
                request.Formats,
                request.RequestedVariantCount,
                request.RevisionParentCreativePackageVersionId,
                request.RevisionNotes,
                request.CanvasWidth,
                request.CanvasHeight,
                root,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToCreativeProductionJobDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/creative-production-jobs/{dto.CreativeProductionJobId}", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/creative-production-jobs")]
    public async Task<ActionResult> ListCreativeProductionJobs(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await creative.ListCreativeProductionJobsAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToCreativeProductionJobDto).ToList());
        });
    }

    [HttpGet("creative-production-jobs/{creativeProductionJobId:guid}")]
    public async Task<ActionResult> GetCreativeProductionJob(
        Guid creativeProductionJobId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await creative.GetCreativeProductionJobAsync(
                creativeProductionJobId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToCreativeProductionJobDto(result));
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/creative-packages")]
    public async Task<ActionResult> ListCreativePackages(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await creative.ListCreativePackagesAsync(
                workspaceId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(new WeddingPlannerCreativePackageListDto(
                result.WorkspaceId,
                result.AdvertiserId,
                result.CurrentApprovedCreativePackageVersionId,
                result.Versions.Select(ToCreativePackageDto).ToList()));
        });
    }

    [HttpGet("creative-packages/{creativePackageVersionId:guid}")]
    public async Task<ActionResult> GetCreativePackage(
        Guid creativePackageVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await creative.GetCreativePackageAsync(
                creativePackageVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToCreativePackageDto(result));
        });
    }

    [HttpGet("creative-packages/{creativePackageVersionId:guid}/contributions")]
    public async Task<ActionResult> ListCreativePackageContributions(
        Guid creativePackageVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await creative.ListContributionsAsync(
                creativePackageVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToCreativeContributionDto).ToList());
        });
    }

    [HttpGet("creative-packages/{creativePackageVersionId:guid}/assets")]
    public async Task<ActionResult> ListCreativePackageAssets(
        Guid creativePackageVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await creative.ListAssetsAsync(
                creativePackageVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToCreativeAssetDto).ToList());
        });
    }

    [HttpGet("creative-packages/{creativePackageVersionId:guid}/agent-runs")]
    public async Task<ActionResult> ListCreativePackageAgentRuns(
        Guid creativePackageVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await creative.ListPackageAgentRunsAsync(
                creativePackageVersionId,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToAgentRunDto).ToList());
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("creative-packages/{creativePackageVersionId:guid}/decisions")]
    public async Task<ActionResult> DecideCreativePackage(
        Guid creativePackageVersionId,
        WeddingPlannerCreativePackageDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireWrite();
            var result = await creative.DecideAsync(
                creativePackageVersionId,
                request.Decision,
                request.Rationale,
                request.SelectedVariantId,
                request.SourceSystem,
                request.IdempotencyKey,
                actor.IsChapelStaff,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToCreativeDecisionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/creative-packages/{dto.CreativePackageVersionId}/decisions", dto);
        });
    }

    [HttpGet("creative-assets/{creativeAssetId:guid}")]
    public async Task<ActionResult> GetCreativeAsset(
        Guid creativeAssetId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            // Reviewers need metadata for QA visual-review UI; do not widen Phase 1–6 writes.
            var canAccess = actor.IsChapelStaff || access.CanAccessQaAcrossWorkspaces(actor);
            var result = await creative.GetAssetAsync(
                creativeAssetId,
                canAccess,
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToCreativeAssetDto(result));
        });
    }

    [HttpGet("creative-assets/{creativeAssetId:guid}/content")]
    public async Task<ActionResult> GetCreativeAssetContent(
        Guid creativeAssetId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            // Reviewers who can decide QA must view same-origin selected PNG content.
            var canAccess = actor.IsChapelStaff || access.CanAccessQaAcrossWorkspaces(actor);
            var result = await creative.GetAssetContentAsync(
                creativeAssetId,
                canAccess,
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);

            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.ETag = $"\"{result.Sha256}\"";
            Response.Headers.ContentDisposition = $"inline; filename=\"{result.FileName}\"";
            return File(result.Bytes, result.ContentType);
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("workspaces/{workspaceId:guid}/qa-review-jobs")]
    public async Task<ActionResult> CreateQaReviewJob(
        Guid workspaceId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireQaCreate();
            if (body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new InvalidOperationException("QA review job body is required.");
            }

            var root = JsonNode.Parse(body.GetRawText())
                ?? throw new InvalidOperationException("QA review job body is required.");
            WeddingPlannerQaReviewValidation.RejectForbiddenBriefFields(root);

            var request = body.Deserialize<CreateWeddingPlannerQaReviewJobRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("QA review job body is invalid.");

            var result = await qaReview.CreateQaReviewJobAsync(
                workspaceId,
                request.ReviewObjective,
                request.FocusAreas,
                request.Notes,
                root,
                request.SourceSystem,
                request.IdempotencyKey,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToQaReviewJobDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/qa-review-jobs/{dto.QaReviewJobId}", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/qa-review-jobs")]
    public async Task<ActionResult> ListQaReviewJobs(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await qaReview.ListQaReviewJobsAsync(
                workspaceId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToQaReviewJobDto).ToList());
        });
    }

    [HttpGet("qa-review-jobs/{qaReviewJobId:guid}")]
    public async Task<ActionResult> GetQaReviewJob(
        Guid qaReviewJobId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await qaReview.GetQaReviewJobAsync(
                qaReviewJobId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToQaReviewJobDto(result));
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/qa-review-reports")]
    public async Task<ActionResult> ListQaReviewReports(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await qaReview.ListQaReviewReportsAsync(
                workspaceId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(new WeddingPlannerQaReviewReportListDto(
                result.WorkspaceId,
                result.AdvertiserId,
                result.CurrentAcceptedQaReviewReportVersionId,
                result.Versions.Select(ToQaReviewReportDto).ToList()));
        });
    }

    [HttpGet("qa-review-reports/{qaReviewReportVersionId:guid}")]
    public async Task<ActionResult> GetQaReviewReport(
        Guid qaReviewReportVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await qaReview.GetQaReviewReportAsync(
                qaReviewReportVersionId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToQaReviewReportDto(result));
        });
    }

    [HttpGet("qa-review-reports/{qaReviewReportVersionId:guid}/contributions")]
    public async Task<ActionResult> ListQaReviewContributions(
        Guid qaReviewReportVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await qaReview.ListContributionsAsync(
                qaReviewReportVersionId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToQaContributionDto).ToList());
        });
    }

    [HttpGet("qa-review-reports/{qaReviewReportVersionId:guid}/agent-runs")]
    public async Task<ActionResult> ListQaReviewAgentRuns(
        Guid qaReviewReportVersionId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await qaReview.ListReportAgentRunsAsync(
                qaReviewReportVersionId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToAgentRunDto).ToList());
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("qa-review-reports/{qaReviewReportVersionId:guid}/decisions")]
    public async Task<ActionResult> DecideQaReviewReport(
        Guid qaReviewReportVersionId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireQaDecide();
            if (body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new InvalidOperationException("QA review decision body is required.");
            }

            var root = JsonNode.Parse(body.GetRawText())
                ?? throw new InvalidOperationException("QA review decision body is required.");
            var request = body.Deserialize<WeddingPlannerQaReviewDecisionRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("QA review decision body is invalid.");

            var result = await qaReview.DecideQaReviewReportAsync(
                qaReviewReportVersionId,
                request.Decision,
                request.Rationale,
                request.SelectedVariantId,
                request.VisualReviewConfirmed,
                request.CopyReviewConfirmed,
                request.ProvenanceReviewConfirmed,
                request.SyntheticMarkerAcknowledged,
                request.EscalationCategory,
                root,
                request.SourceSystem,
                request.IdempotencyKey,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                access.CanDecideQaReview(actor),
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToQaDecisionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/qa-review-reports/{dto.QaReviewReportVersionId}/decisions", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/qa-escalation-cases")]
    public async Task<ActionResult> ListQaEscalationCases(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var items = await qaReview.ListEscalationCasesAsync(
                workspaceId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToQaEscalationCaseDto).ToList());
        });
    }

    [HttpGet("qa-escalation-cases/{escalationCaseId:guid}")]
    public async Task<ActionResult> GetQaEscalationCase(
        Guid escalationCaseId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = access.Resolve(User);
            var result = await qaReview.GetEscalationCaseAsync(
                escalationCaseId,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToQaEscalationCaseDto(result));
        });
    }

    [Authorize]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost("qa-escalation-cases/{escalationCaseId:guid}/resolutions")]
    public async Task<ActionResult> ResolveQaEscalationCase(
        Guid escalationCaseId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireQaDecide();
            if (body.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                throw new InvalidOperationException("QA escalation resolution body is required.");
            }

            var root = JsonNode.Parse(body.GetRawText())
                ?? throw new InvalidOperationException("QA escalation resolution body is required.");
            var request = body.Deserialize<WeddingPlannerQaEscalationResolutionRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("QA escalation resolution body is invalid.");

            var result = await qaReview.ResolveEscalationCaseAsync(
                escalationCaseId,
                request.Resolution,
                request.Rationale,
                request.ExceptionRationale,
                request.ExceptionAcknowledged,
                request.AcknowledgedBlockerCodes,
                root,
                request.SourceSystem,
                request.IdempotencyKey,
                access.CanAccessQaAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                access.CanDecideQaReview(actor),
                access.CanWaiveQaEscalation(actor),
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToQaEscalationResolutionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/qa-escalation-cases/{dto.EscalationCaseId}/resolutions", dto);
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/campaign-readiness/eligibility")]
    public async Task<ActionResult> GetCampaignReadinessEligibility(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireCampaignReadinessRead();
            var result = await campaignReadiness.GetEligibilityAsync(
                workspaceId,
                access.CanAccessCampaignReadinessAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToCampaignReadinessEligibilityDto(result));
        });
    }

    [HttpGet("workspaces/{workspaceId:guid}/campaign-readiness-handshakes")]
    public async Task<ActionResult> ListCampaignReadinessHandshakes(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireCampaignReadinessRead();
            var items = await campaignReadiness.ListHandshakesAsync(
                workspaceId,
                access.CanAccessCampaignReadinessAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToCampaignReadinessHandshakeDto).ToList());
        });
    }

    [HttpGet("campaign-readiness-handshakes/{handshakeId:guid}")]
    public async Task<ActionResult> GetCampaignReadinessHandshake(
        Guid handshakeId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireCampaignReadinessRead();
            var result = await campaignReadiness.GetHandshakeAsync(
                handshakeId,
                access.CanAccessCampaignReadinessAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(ToCampaignReadinessHandshakeDto(result));
        });
    }

    [HttpGet("campaign-readiness-handshakes/{handshakeId:guid}/decisions")]
    public async Task<ActionResult> ListCampaignReadinessDecisions(
        Guid handshakeId,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireCampaignReadinessRead();
            var items = await campaignReadiness.ListDecisionsAsync(
                handshakeId,
                access.CanAccessCampaignReadinessAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                cancellationToken);
            return Ok(items.Select(ToCampaignReadinessDecisionDto).ToList());
        });
    }

    [HttpPost("workspaces/{workspaceId:guid}/campaign-readiness-handshakes")]
    public async Task<ActionResult> CommitCampaignReadinessHandshake(
        Guid workspaceId,
        [FromBody] JsonNode? body,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireCampaignReadinessCommit();
            var root = body as JsonObject
                ?? throw new InvalidOperationException("Request body must be a JSON object.");
            WeddingPlannerCampaignReadinessValidation.RejectForbiddenCommitFields(root);
            var request = body.Deserialize<CommitWeddingPlannerCampaignReadinessHandshakeRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Request body is required.");
            var result = await campaignReadiness.CommitAsync(
                workspaceId,
                request.BlissMatchId,
                request.CampaignId,
                request.ContentItemId,
                request.AdInventorySlotId,
                request.Rationale,
                request.DisclaimerAcknowledged,
                request.SyntheticMarkerAcknowledged,
                root,
                request.SourceSystem,
                request.IdempotencyKey,
                access.CanCommitCampaignReadiness(actor),
                access.CanAccessCampaignReadinessAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToCampaignReadinessHandshakeDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/campaign-readiness-handshakes/{dto.CampaignReadinessHandshakeVersionId}", dto);
        });
    }

    [HttpPost("campaign-readiness-handshakes/{handshakeId:guid}/decisions")]
    public async Task<ActionResult> DecideCampaignReadinessHandshake(
        Guid handshakeId,
        [FromBody] JsonNode? body,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync(async () =>
        {
            var actor = RequireCampaignReadinessCommit();
            var root = body as JsonObject
                ?? throw new InvalidOperationException("Request body must be a JSON object.");
            WeddingPlannerCampaignReadinessValidation.RejectForbiddenRevokeFields(root);
            var request = body.Deserialize<WeddingPlannerCampaignReadinessDecisionRequest>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException("Request body is required.");
            var result = await campaignReadiness.RevokeAsync(
                handshakeId,
                request.Decision,
                request.Rationale,
                root,
                request.SourceSystem,
                request.IdempotencyKey,
                access.CanCommitCampaignReadiness(actor),
                access.CanAccessCampaignReadinessAcrossWorkspaces(actor),
                actor.BoundAdvertiserId,
                actor.ActorType,
                actor.ActorLabel,
                RequestCorrelation.Resolve(HttpContext),
                cancellationToken);
            var dto = ToCampaignReadinessDecisionDto(result);
            return result.IsReplay
                ? Ok(dto)
                : Created($"/api/wedding-planner/campaign-readiness-handshakes/{dto.CampaignReadinessHandshakeVersionId}/decisions", dto);
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

    private WeddingPlannerActor RequireQaCreate()
    {
        var actor = access.Resolve(User);
        if (!access.CanCreateQaReview(actor))
        {
            throw new WeddingPlannerForbiddenException("The authenticated identity cannot create QA review jobs.");
        }

        return actor;
    }

    private WeddingPlannerActor RequireQaDecide()
    {
        var actor = access.Resolve(User);
        if (!access.CanDecideQaReview(actor))
        {
            throw new WeddingPlannerForbiddenException("The authenticated identity cannot record QA review decisions.");
        }

        return actor;
    }

    private WeddingPlannerActor RequireCampaignReadinessRead()
    {
        var actor = access.Resolve(User);
        if (!access.CanReadCampaignReadiness(actor))
        {
            throw new WeddingPlannerNotFoundException("Campaign readiness resource was not found.");
        }

        return actor;
    }

    private WeddingPlannerActor RequireCampaignReadinessCommit()
    {
        var actor = access.Resolve(User);
        if (!access.CanCommitCampaignReadiness(actor))
        {
            throw new WeddingPlannerForbiddenException(
                "Only operator or admin may commit or revoke campaign-readiness handshakes.");
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
        catch (WeddingPlannerCreativeAssetJobProviderException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message, creativeProductionJobId = ex.CreativeProductionJobId, errorCode = ex.ErrorCode });
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
            result.OutputConceptPackageVersionId,
            result.OutputCreativePackageVersionId,
            result.OutputQaReviewReportVersionId,
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

    private static WeddingPlannerWorkshopJobDto ToWorkshopJobDto(WeddingPlannerWorkshopJobResult result) =>
        new(
            result.WorkshopJobId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.Objective,
            result.CampaignGoal,
            result.AudienceFocus,
            result.ChannelFormat,
            result.CanvasWidth,
            result.CanvasHeight,
            result.Deliverables,
            result.Cta,
            result.Constraints,
            result.InputJson,
            result.InputSha256,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedBrandDnaVersionNumber,
            result.ApprovedColorProfileVersionId,
            result.ApprovedColorProfileVersionNumber,
            result.ApprovedResearchReportVersionId,
            result.ApprovedResearchReportVersionNumber,
            result.StrategyAgentRunId,
            result.CreativeAgentRunId,
            result.ProductionAgentRunId,
            result.OutputConceptPackageVersionId,
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

    private static WeddingPlannerConceptPackageVersionDto ToConceptPackageDto(
        WeddingPlannerConceptPackageVersionResult result) =>
        new(
            result.ConceptPackageVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.VersionNumber,
            result.SchemaVersion,
            result.DocumentJson,
            result.Summary,
            result.ProducingWorkshopJobId,
            result.ProducingAgentRunId,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedBrandDnaVersionNumber,
            result.ApprovedColorProfileVersionId,
            result.ApprovedColorProfileVersionNumber,
            result.ApprovedResearchReportVersionId,
            result.ApprovedResearchReportVersionNumber,
            result.ChannelFormat,
            result.CanvasWidth,
            result.CanvasHeight,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ActorType,
            result.ActorLabel,
            result.CreatedAt,
            result.IsCurrentApproved,
            result.EstimatedTotalCostUsd,
            result.IsReplay);

    private static WeddingPlannerConceptRoleContributionDto ToConceptContributionDto(
        WeddingPlannerConceptRoleContributionResult result) =>
        new(
            result.ContributionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.ConceptPackageVersionId,
            result.WorkshopJobId,
            result.LogicalRole,
            result.ProducingAgentRunId,
            result.ContributionJson,
            result.CreatedAt);

    private static WeddingPlannerConceptPackageDecisionDto ToConceptDecisionDto(
        WeddingPlannerConceptPackageDecisionResult result) =>
        new(
            result.DecisionId,
            result.ConceptPackageVersionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Decision,
            result.SelectedConceptId,
            result.ActorType,
            result.ActorLabel,
            result.Rationale,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToConceptPackageDto(result.Version),
            result.IsReplay);

    private static WeddingPlannerCreativeProductionJobDto ToCreativeProductionJobDto(
        WeddingPlannerCreativeProductionJobResult result) =>
        new(
            result.CreativeProductionJobId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.JobKind,
            result.Objective,
            result.Formats,
            result.RequestedVariantCount,
            result.RevisionParentCreativePackageVersionId,
            result.RevisionNotes,
            result.InputJson,
            result.InputSha256,
            result.ApprovedConceptPackageVersionId,
            result.SelectedConceptId,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedBrandDnaVersionNumber,
            result.ApprovedColorProfileVersionId,
            result.ApprovedColorProfileVersionNumber,
            result.ApprovedResearchReportVersionId,
            result.ApprovedResearchReportVersionNumber,
            result.CreativeDirectionAgentRunId,
            result.StrategyAdaptationAgentRunId,
            result.VisualSystemAgentRunId,
            result.ImageDirectionAgentRunId,
            result.CopySystemAgentRunId,
            result.VariantProductionAgentRunId,
            result.AssetProviderKey,
            result.AssetProviderAdapterVersion,
            result.AssetProviderRequestId,
            result.AssetProviderEstimatedCostUsd,
            result.OutputCreativePackageVersionId,
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

    private static WeddingPlannerCreativePackageVersionDto ToCreativePackageDto(
        WeddingPlannerCreativePackageVersionResult result) =>
        new(
            result.CreativePackageVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.VersionNumber,
            result.SchemaVersion,
            result.DocumentJson,
            result.Summary,
            result.ProducingCreativeProductionJobId,
            result.ProducingAgentRunId,
            result.ApprovedConceptPackageVersionId,
            result.SelectedConceptId,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedBrandDnaVersionNumber,
            result.ApprovedColorProfileVersionId,
            result.ApprovedColorProfileVersionNumber,
            result.ApprovedResearchReportVersionId,
            result.ApprovedResearchReportVersionNumber,
            result.JobKind,
            result.ParentCreativePackageVersionId,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ActorType,
            result.ActorLabel,
            result.CreatedAt,
            result.IsCurrentApproved,
            result.EstimatedTotalCostUsd,
            result.EstimatedAssetCostUsd,
            result.IsReplay);

    private static WeddingPlannerCreativeRoleContributionDto ToCreativeContributionDto(
        WeddingPlannerCreativeRoleContributionResult result) =>
        new(
            result.ContributionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.CreativePackageVersionId,
            result.CreativeProductionJobId,
            result.LogicalRole,
            result.ProducingAgentRunId,
            result.ContributionJson,
            result.CreatedAt);

    private static WeddingPlannerCreativeAssetDto ToCreativeAssetDto(
        WeddingPlannerCreativeAssetResult result) =>
        new(
            result.CreativeAssetId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.CreativePackageVersionId,
            result.CreativeProductionJobId,
            result.VariantId,
            result.Format,
            result.Width,
            result.Height,
            result.ContentType,
            result.ByteSize,
            result.Sha256,
            result.ProviderKey,
            result.AdapterVersion,
            result.ProviderRequestId,
            result.EstimatedCostUsd,
            result.CreatedAt);

    private static WeddingPlannerCreativePackageDecisionDto ToCreativeDecisionDto(
        WeddingPlannerCreativePackageDecisionResult result) =>
        new(
            result.DecisionId,
            result.CreativePackageVersionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Decision,
            result.SelectedVariantId,
            result.ActorType,
            result.ActorLabel,
            result.Rationale,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToCreativePackageDto(result.Version),
            result.IsReplay);

    private static WeddingPlannerQaReviewJobDto ToQaReviewJobDto(WeddingPlannerQaReviewJobResult result) =>
        new(
            result.QaReviewJobId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.ReviewObjective,
            result.FocusAreas,
            result.Notes,
            result.InputJson,
            result.InputSha256,
            result.ApprovedCreativePackageVersionId,
            result.CreativePackageDocumentSha256,
            result.CreativePackageDecisionId,
            result.SelectedVariantId,
            result.SelectedCreativeAssetId,
            result.SelectedCreativeAssetSha256,
            result.SelectedCreativeAssetContentType,
            result.SelectedCreativeAssetByteSize,
            result.SelectedCreativeAssetWidth,
            result.SelectedCreativeAssetHeight,
            result.SelectedConceptId,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedBrandDnaVersionNumber,
            result.ApprovedColorProfileVersionId,
            result.ApprovedColorProfileVersionNumber,
            result.ApprovedResearchReportVersionId,
            result.ApprovedResearchReportVersionNumber,
            result.RulesFindingsJson,
            result.RulesOverallSeverity,
            result.ChaperoneReviewAgentRunId,
            result.QaInspectionAgentRunId,
            result.OutputQaReviewReportVersionId,
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

    private static WeddingPlannerQaReviewReportVersionDto ToQaReviewReportDto(
        WeddingPlannerQaReviewReportVersionResult result) =>
        new(
            result.QaReviewReportVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.VersionNumber,
            result.SchemaVersion,
            result.DocumentJson,
            result.Summary,
            result.ProducingQaReviewJobId,
            result.ProducingAgentRunId,
            result.ApprovedCreativePackageVersionId,
            result.CreativePackageDocumentSha256,
            result.CreativePackageDecisionId,
            result.SelectedVariantId,
            result.SelectedCreativeAssetId,
            result.SelectedCreativeAssetSha256,
            result.SelectedConceptId,
            result.ApprovedBrandDnaVersionId,
            result.ApprovedBrandDnaVersionNumber,
            result.ApprovedColorProfileVersionId,
            result.ApprovedColorProfileVersionNumber,
            result.ApprovedResearchReportVersionId,
            result.ApprovedResearchReportVersionNumber,
            result.Status,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ActorType,
            result.ActorLabel,
            result.CreatedAt,
            result.IsCurrentAccepted,
            result.RulesOverallSeverity,
            result.EstimatedTotalCostUsd,
            result.IsReplay);

    private static WeddingPlannerQaRoleContributionDto ToQaContributionDto(
        WeddingPlannerQaRoleContributionResult result) =>
        new(
            result.ContributionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.QaReviewReportVersionId,
            result.QaReviewJobId,
            result.LogicalRole,
            result.ContributionSource,
            result.ProducingAgentRunId,
            result.ContributionJson,
            result.CreatedAt);

    private static WeddingPlannerQaReviewDecisionDto ToQaDecisionDto(
        WeddingPlannerQaReviewDecisionResult result) =>
        new(
            result.DecisionId,
            result.QaReviewReportVersionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Decision,
            result.SelectedVariantId,
            result.Rationale,
            result.VisualReviewConfirmed,
            result.CopyReviewConfirmed,
            result.ProvenanceReviewConfirmed,
            result.SyntheticMarkerAcknowledged,
            result.EscalationCategory,
            result.ActorType,
            result.ActorLabel,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToQaReviewReportDto(result.Version),
            result.EscalationCaseId,
            result.IsReplay);

    private static WeddingPlannerQaEscalationCaseDto ToQaEscalationCaseDto(
        WeddingPlannerQaEscalationCaseResult result) =>
        new(
            result.EscalationCaseId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.QaReviewReportVersionId,
            result.QaReviewDecisionId,
            result.Category,
            result.Status,
            result.RationaleSnapshot,
            result.SelectedVariantId,
            result.ApprovedCreativePackageVersionId,
            result.SelectedCreativeAssetId,
            result.ActorType,
            result.ActorLabel,
            result.SourceSystem,
            result.IdempotencyKey,
            result.CreatedAt,
            result.ResolutionId,
            result.IsReplay);

    private static WeddingPlannerQaEscalationResolutionDto ToQaEscalationResolutionDto(
        WeddingPlannerQaEscalationResolutionResult result) =>
        new(
            result.ResolutionId,
            result.EscalationCaseId,
            result.QaReviewReportVersionId,
            result.WorkspaceId,
            result.AdvertiserId,
            result.Resolution,
            result.Rationale,
            result.ExceptionRationale,
            result.ExceptionAcknowledged,
            result.AcknowledgedBlockerCodes,
            result.ActorType,
            result.ActorLabel,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToQaEscalationCaseDto(result.Case),
            ToQaReviewReportDto(result.Version),
            result.IsReplay);

    private static WeddingPlannerCampaignReadinessHandshakeDto ToCampaignReadinessHandshakeDto(
        WeddingPlannerCampaignReadinessHandshakeResult result) =>
        new(
            result.CampaignReadinessHandshakeVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.VersionNumber,
            result.SchemaVersion,
            result.DocumentJson,
            result.Summary,
            result.Status,
            result.QaReviewReportVersionId,
            result.QaAcceptDecisionId,
            result.ApprovedCreativePackageVersionId,
            result.CreativePackageDocumentSha256,
            result.CreativePackageDecisionId,
            result.SelectedVariantId,
            result.SelectedCreativeAssetId,
            result.SelectedCreativeAssetSha256,
            result.BlissMatchId,
            result.CampaignId,
            result.ContentItemId,
            result.AdInventorySlotId,
            result.CampaignPlacementId,
            result.CampaignPlacementRunId,
            result.RulesFindingsJson,
            result.Rationale,
            result.DisclaimerAcknowledged,
            result.SyntheticMarkerAcknowledged,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ActorType,
            result.ActorLabel,
            result.CreatedAt,
            result.IsCurrent,
            result.IsReplay);

    private static WeddingPlannerCampaignReadinessDecisionDto ToCampaignReadinessDecisionDto(
        WeddingPlannerCampaignReadinessDecisionResult result) =>
        new(
            result.CampaignReadinessDecisionId,
            result.CampaignReadinessHandshakeVersionId,
            result.AdvertiserId,
            result.WorkspaceId,
            result.Decision,
            result.Rationale,
            result.ActorType,
            result.ActorLabel,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OccurredAt,
            ToCampaignReadinessHandshakeDto(result.Version),
            result.IsReplay);

    private static WeddingPlannerCampaignReadinessEligibilityDto ToCampaignReadinessEligibilityDto(
        WeddingPlannerCampaignReadinessEligibilityResult result) =>
        new(
            result.WorkspaceId,
            result.AdvertiserId,
            result.HasCurrentQaPointer,
            result.CurrentAcceptedQaReviewReportVersionId,
            result.CurrentQaStatus,
            result.IsCleanAccepted,
            result.HasCleanAcceptDecision,
            result.CurrentApprovedCreativePackageVersionId,
            result.PackageReady,
            result.HasSyntheticUpstream,
            result.CurrentCampaignReadinessHandshakeVersionId,
            result.NoReservationDisclosure,
            result.Candidates.Select(m => new CampaignReadinessMatchCandidateDto(
                m.BlissMatchId,
                m.CreatorId,
                m.AdvertiserOpportunityId,
                m.MatchStatus,
                m.OverallScore,
                m.OpportunityStatus,
                m.OpportunityName,
                m.Campaigns.Select(c => new CampaignReadinessCampaignCandidateDto(
                    c.CampaignId,
                    c.Name,
                    c.Status,
                    c.AdvertiserOpportunityId,
                    c.OpportunityCompatible)).ToList(),
                m.ContentItems.Select(ci => new CampaignReadinessContentCandidateDto(
                    ci.ContentItemId,
                    ci.Title,
                    ci.ContentType,
                    ci.Slots.Select(s => new CampaignReadinessSlotCandidateDto(
                        s.AdInventorySlotId,
                        s.SlotType,
                        s.IsAvailable,
                        s.AvailabilityNote)).ToList())).ToList())).ToList());
}
