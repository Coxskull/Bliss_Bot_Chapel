using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/week")]
public sealed class CoverageWeekController(CoverageWeekService weeks) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await weeks.ReadAsync(cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Store([FromBody] CoverageWeekRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await weeks.StoreAsync(request?.WeekKey, request?.AddMissingMarket ?? false, cancellationToken);
            var board = await weeks.ReadAsync(cancellationToken);
            return Ok(Body(board, write));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new
            {
                error = ex.Message,
                delivery = "NOT_SENT",
                greenMeansSend = false,
                censusClaimed = false,
                slicesChanged = false
            });
        }
    }

    private static object Body(WeekBoard board, WeekWrite? write) => new
    {
        notice = write?.Week.Notice ?? CoverageWeek.Notice,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        censusClaimed = false,
        slicesChanged = false,
        qualifiedSlices = board.Reading.Fuel.QualifiedUnique,
        fuelStatus = board.Reading.Fuel.FuelStatus,
        markets = board.Reading.Markets.Select(item => new
        {
            item.Market,
            item.CountryLine,
            item.QualifiedSlices
        }),
        history = board.History.Select(item => new
        {
            item.WeekKey,
            item.QualifiedSlices,
            item.MarketCount,
            item.MarketLine,
            item.FuelStatus,
            item.CensusClaimed,
            item.SlicesChanged,
            item.Delivery
        })
    };
}

public sealed record CoverageWeekRequest(string? WeekKey, bool? AddMissingMarket);
