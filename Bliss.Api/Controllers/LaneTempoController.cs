using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Operations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/tempo")]
public sealed class LaneTempoController(LaneTempoService tempos) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await tempos.ReadAsync(cancellationToken);
        return Ok(Body(board));
    }

    [HttpGet("fleets")]
    [AllowAnonymous]
    public async Task<ActionResult> Fleets(CancellationToken cancellationToken)
    {
        try
        {
            var board = await tempos.ReadAsync(cancellationToken);
            var reading = FleetLanes.Read(board.Lanes.Select(item => new FleetLane(item.Lane, item.Tempo)));
            return Ok(new
            {
                reading.Notice,
                reading.Ocean,
                reading.Bliss,
                reading.OceanMoving,
                reading.GreenMeansSend,
                reading.Delivery,
                fleets = reading.Fleets.Select(fleet => new
                {
                    fleet.Fleet,
                    fleet.DisplayName,
                    fleet.Moving,
                    fleet.Notice,
                    lanes = fleet.Lanes.Select(lane => new
                    {
                        lane.Lane,
                        lane.DisplayName,
                        lane.Tempo,
                        lane.Place
                    })
                })
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false });
        }
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Apply([FromBody] LaneTempoRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var board = await tempos.ApplyAsync(
                request?.Lane,
                request?.Tempo,
                request?.CeilingAmount,
                request?.CeilingCurrency,
                request?.Reason,
                cancellationToken);
            return Ok(Body(board));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message, delivery = "NOT_SENT", greenMeansSend = false });
        }
    }

    private static object Body(LaneTempoBoard board) => new
    {
        board.Notice,
        board.Delivery,
        board.GreenMeansSend,
        lanes = board.Lanes.Select(item => new
        {
            item.Lane,
            item.DisplayName,
            item.Tempo,
            item.CeilingAmount,
            item.CeilingCurrency,
            item.Notice
        }),
        audits = board.Audits.Select(item => new
        {
            item.Id,
            item.Lane,
            item.Tempo,
            item.CeilingAmount,
            item.CeilingCurrency,
            item.Reason,
            item.Notice,
            item.RecordedAt
        })
    };
}

public sealed record LaneTempoRequest(
    string? Lane,
    string? Tempo,
    decimal? CeilingAmount,
    string? CeilingCurrency,
    string? Reason);
