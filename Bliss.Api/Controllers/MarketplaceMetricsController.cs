using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Demonstrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/operations/metrics")]
public sealed class MarketplaceMetricsController(MarketplaceMetricsService metrics) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult> Read(CancellationToken cancellationToken)
    {
        var board = await metrics.ReadAsync(cancellationToken);
        return Ok(Body(board, null));
    }

    [HttpPost]
    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<ActionResult> Store([FromBody] MarketplaceMetricsRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var write = await metrics.StoreAsync(request?.MetricKey, request?.RevenueAmount, request?.AddSlot ?? false, cancellationToken);
            var board = await metrics.ReadAsync(cancellationToken);
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
                revenueRecorded = false,
                slotsChanged = false
            });
        }
    }

    private static object Body(MetricBoard board, MetricWrite? write) => new
    {
        notice = write?.Reading.Notice ?? MarketplaceMetrics.Notice,
        delivery = "NOT_SENT",
        greenMeansSend = false,
        duplicate = write?.Duplicate ?? false,
        written = write?.Written ?? false,
        censusClaimed = false,
        revenueRecorded = false,
        slotsChanged = false,
        advertiserCount = board.Snapshot.AdvertiserCount,
        creatorCount = board.Snapshot.CreatorCount,
        slotCount = board.Snapshot.SlotCount,
        revenueRowCount = board.Snapshot.RevenueRowCount,
        pressure = board.Snapshot.Pressure,
        revenueLine = board.Snapshot.RevenueLine,
        balanceRevenue = MarketplaceBalance.RevenueUnrecorded,
        balanceInventory = MarketplaceBalance.InventoryUnrecorded,
        advertisers = board.Snapshot.Advertisers,
        creators = board.Snapshot.Creators,
        history = board.History.Select(item => new
        {
            item.MetricKey,
            item.AdvertiserCount,
            item.CreatorCount,
            item.SlotCount,
            item.RevenueRowCount,
            item.Pressure,
            item.RevenueLine,
            item.CensusClaimed,
            item.RevenueRecorded,
            item.SlotsChanged,
            item.Delivery
        })
    };
}

public sealed record MarketplaceMetricsRequest(string? MetricKey, decimal? RevenueAmount, bool? AddSlot);
