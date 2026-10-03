using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/rotation")]
public sealed class RotationPeriodController(RotationPeriodService periods) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await periods.ReadAsync(cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Store([FromBody] RotationPeriodRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await periods.StoreAsync(
                request?.Pair,
                request?.CreatorApproved,
                request?.PeriodKey,
                request?.FillOpenSlots ?? false,
                request?.RevenueAmount,
                cancellationToken);
            var board = await periods.ReadAsync(cancellationToken);
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
                slotsChanged = false,
                revenueLine = RotationPeriod.RevenueUnrecorded
            });
        }
    }

    private static object Body(PeriodBoard board, PeriodWrite? write) => new
    {
        notice = write?.Period.Notice ?? RotationPeriod.Notice,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        censusClaimed = false,
        slotsChanged = false,
        revenueLine = RotationPeriod.RevenueUnrecorded,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        slotCount = write?.SlotCount ?? board.SlotCount,
        advertiserCount = write?.AdvertiserCount ?? board.AdvertiserCount,
        advertisers = board.Preview.Advertisers,
        history = board.History.Select(item => new
        {
            item.PeriodKey,
            item.TheoreticalSlots,
            item.PlacedAdvertisers,
            item.OpenSlots,
            item.SlotCount,
            item.CreatorApproved,
            item.RevenueLine,
            item.CensusClaimed,
            item.SlotsChanged,
            item.Delivery
        })
    };
}

public sealed record RotationPeriodRequest(
    int? Pair,
    bool? CreatorApproved,
    string? PeriodKey,
    bool? FillOpenSlots,
    decimal? RevenueAmount);
