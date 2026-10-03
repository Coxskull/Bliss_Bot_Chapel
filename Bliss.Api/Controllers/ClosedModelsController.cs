using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/models")]
public sealed class ClosedModelsController(ClosedModelsService models) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await models.ReadAsync(cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Store([FromBody] ClosedModelsRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await models.StoreAsync(
                request?.ReadingKey,
                request?.ConfigureModel ?? false,
                request?.EngagementCount,
                request?.AuthorizedTraffic ?? false,
                cancellationToken);
            var board = await models.ReadAsync(cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                delivery = "NOT_SENT",
                greenMeansSend = false,
                modelsConfigured = false,
                authorizedTraffic = false,
                productionChanged = false,
                configuredModels = 0
            });
        }
    }

    private static object Body(ModelBoard board, ModelWrite? write) => new
    {
        notice = write?.Reading.Notice ?? ClosedModels.Notice,
        learningNotice = LearningLedger.Notice,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        configuredModels = 0,
        modelCalls = board.Snapshot.ModelCalls,
        noteCount = board.Snapshot.NoteCount,
        laboratoryPassed = board.Snapshot.LaboratoryPassed,
        passedCount = board.Snapshot.PassedCount,
        scenarioCount = board.Snapshot.ScenarioCount,
        modelsConfigured = false,
        authorizedTraffic = false,
        productionChanged = false,
        history = board.History.Select(item => new
        {
            item.ReadingKey,
            item.ConfiguredModels,
            item.ModelCalls,
            item.NoteCount,
            item.LaboratoryPassed,
            item.PassedCount,
            item.ScenarioCount,
            item.ModelsConfigured,
            item.AuthorizedTraffic,
            item.ProductionChanged,
            item.Delivery
        })
    };
}

public sealed record ClosedModelsRequest(string? ReadingKey, bool? ConfigureModel, int? EngagementCount, bool? AuthorizedTraffic);
