using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/learning")]
public sealed class LearningLedgerController(LearningLedgerService learning) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read([FromQuery] string? prospect, CancellationToken cancellationToken)
    {
        var board = await learning.ReadAsync(prospect, cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Append([FromBody] LearningNoteRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await learning.AppendAsync(
                request?.ProspectSlug,
                request?.ApplyToProduction ?? false,
                request?.AuthorizedTraffic ?? false,
                request?.EngagementCount,
                request?.IdempotencyKey,
                cancellationToken);
            var board = await learning.ReadAsync(write.Note.ProspectSlug, cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                delivery = "NOT_SENT",
                greenMeansSend = false,
                productionChanged = false,
                behaviorChanged = false,
                authorizedTraffic = false,
                modelCalls = 0
            });
        }
    }

    private static object Body(LearningBoard board, LearningWrite? write) => new
    {
        notice = write?.Note.Notice ?? LearningLedger.Notice,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        productionChanged = false,
        behaviorChanged = false,
        authorizedTraffic = false,
        modelCalls = 0,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        prospectState = write?.ProspectState ?? board.Prospects.FirstOrDefault(item => item.Slug == board.ProspectSlug)?.ProspectState ?? string.Empty,
        laboratoryPassed = board.Laboratory.Passed,
        laboratoryScenarios = board.Laboratory.ScenarioCount,
        laboratoryPassedCount = board.Laboratory.PassedCount,
        prospect = board.ProspectSlug,
        prospects = board.Prospects.Select(item => new
        {
            item.Slug,
            item.BusinessName,
            item.ProspectState
        }),
        notes = board.Notes.Select(item => new
        {
            item.ProspectSlug,
            item.Body,
            item.LaboratoryGraduated,
            item.AuthorizedTraffic,
            item.ProductionChanged,
            item.BehaviorChanged,
            item.ModelCalls,
            item.Delivery
        })
    };
}

public sealed record LearningNoteRequest(
    string? ProspectSlug,
    string? IdempotencyKey,
    bool? ApplyToProduction,
    bool? AuthorizedTraffic,
    int? EngagementCount);
