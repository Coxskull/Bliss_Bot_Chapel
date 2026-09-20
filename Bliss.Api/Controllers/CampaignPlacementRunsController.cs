using Bliss.Api.Contracts;
using Bliss.Domain.Common;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/campaign-placements")]
public sealed class CampaignPlacementsController : ControllerBase
{
    private readonly CampaignPlacementService _service;

    public CampaignPlacementsController(CampaignPlacementService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<ActionResult<CampaignPlacementResultDto>> Create(
        CampaignPlacementRequest request,
        CancellationToken cancellationToken)
    {
        CampaignPlacementResult result;
        try
        {
            result = await _service.BindAsync(
                new CampaignPlacementCommand(
                    request.SourceSystem,
                    request.IdempotencyKey,
                    request.OperatorLabel,
                    request.BlissMatchId,
                    request.CampaignId,
                    request.ContentItemId,
                    request.AdInventorySlotId),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var dto = new CampaignPlacementResultDto(
            result.RunId,
            result.CampaignPlacementId,
            result.BlissMatchId,
            result.CampaignId,
            result.ContentItemId,
            result.AdInventorySlotId,
            result.SourceSystem,
            result.IdempotencyKey,
            result.OperatorLabel,
            result.Status,
            result.Outcome,
            result.CompletedAt,
            result.IsReplay);

        return result.IsReplay
            ? Ok(dto)
            : CreatedAtAction(
                nameof(CampaignPlacementRunsController.GetById),
                "CampaignPlacementRuns",
                new { id = result.RunId },
                dto);
    }
}

[ApiController]
[Route("api/campaign-placement-runs")]
public sealed class CampaignPlacementRunsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public CampaignPlacementRunsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampaignPlacementRunSummaryDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var items = await _db.CampaignPlacementRuns
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Select(x => new CampaignPlacementRunSummaryDto(
                x.Id,
                x.CampaignPlacementId,
                x.BlissMatchId,
                x.CampaignId,
                x.CreatorId,
                x.AdvertiserOpportunityId,
                x.ContentItemId,
                x.AdInventorySlotId,
                x.SourceSystem,
                x.IdempotencyKey,
                x.OperatorLabel,
                x.Status,
                x.Outcome,
                x.CompletedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignPlacementRunDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _db.CampaignPlacementRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(new CampaignPlacementRunDetailDto(
            item.Id,
            item.CampaignPlacementId,
            item.BlissMatchId,
            item.CampaignId,
            item.CreatorId,
            item.AdvertiserOpportunityId,
            item.ContentItemId,
            item.AdInventorySlotId,
            item.SourceSystem,
            item.IdempotencyKey,
            item.OperatorLabel,
            item.Status,
            item.Outcome,
            item.StartedAt,
            item.CompletedAt,
            item.InputSnapshot));
    }
}

[ApiController]
[Route("api/campaign-bindings")]
public sealed class CampaignBindingsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public CampaignBindingsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<CampaignBindingQueueItemDto>>> GetQueue(
        CancellationToken cancellationToken)
    {
        var items = await _db.BlissMatches
            .AsNoTracking()
            .Where(x => x.Status == EntityStatuses.Approved)
            .Where(x => !_db.CampaignPlacementRuns.Any(run => run.BlissMatchId == x.Id))
            .OrderBy(x => x.CreatedAt)
            .Select(x => new CampaignBindingQueueItemDto(
                x.Id,
                x.CreatorId,
                x.Creator.Name,
                x.AdvertiserOpportunityId,
                x.AdvertiserOpportunity.Name,
                x.OverallScore,
                x.ConfidenceScore,
                _db.ContentItems.Count(content => content.CreatorId == x.CreatorId),
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}
