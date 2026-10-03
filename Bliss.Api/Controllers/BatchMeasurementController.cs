using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/measure")]
public sealed class BatchMeasurementController(BatchMeasurementService measurements) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await measurements.ReadAsync(cancellationToken);
        return Ok(Body(board.Measurements, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Measure([FromBody] MeasureRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var attempt = request?.Attempt ?? 1;
            var write = await measurements.MeasureAsync(request?.IdempotencyKey, attempt, cancellationToken);
            return Ok(Body([write.Measurement], write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                delivery = "NOT_SENT",
                greenMeansSend = false,
                hostedAcceptanceClaimed = false,
                factoryTargetClaimed = false,
                censusClaimed = false
            });
        }
    }

    private static object Body(IReadOnlyList<Bliss.Domain.Entities.BatchMeasurementRow> rows, MeasurementWrite? write)
    {
        var latest = rows.OrderBy(item => item.RecordedAt).LastOrDefault();
        return new
        {
            notice = latest?.Notice ?? BatchMeasurement.Notice,
            delivery = "NOT_SENT",
            greenMeansSend = false,
            hostedAcceptanceClaimed = false,
            factoryTargetClaimed = false,
            censusClaimed = false,
            measured = latest is not null,
            duplicate = write?.Duplicate ?? false,
            written = write?.Written ?? false,
            latest = latest is null ? null : Item(latest),
            measurements = rows.OrderBy(item => item.RecordedAt).Select(Item)
        };
    }

    private static object Item(Bliss.Domain.Entities.BatchMeasurementRow item) => new
    {
        item.IdempotencyKey,
        item.StoredProspects,
        item.ElapsedMilliseconds,
        item.WorkingSetBytes,
        item.ResourceLine,
        item.CostLine,
        item.Retries,
        item.PartialFailures,
        item.Recovered,
        item.Leakage,
        item.HostedAcceptanceClaimed,
        item.FactoryTargetClaimed,
        item.CensusClaimed,
        item.Notice,
        failures = string.IsNullOrWhiteSpace(item.Failures)
            ? Array.Empty<string>()
            : item.Failures.Split('\n', StringSplitOptions.RemoveEmptyEntries),
        item.Delivery
    };
}

public sealed record MeasureRequest(string? IdempotencyKey, int? Attempt);
