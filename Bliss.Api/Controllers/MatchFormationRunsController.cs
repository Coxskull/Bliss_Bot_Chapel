using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/match-formation-runs")]
public sealed class MatchFormationRunsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public MatchFormationRunsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchFormationRunSummaryDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var items = await _db.MatchFormationRuns
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Select(x => new MatchFormationRunSummaryDto(
                x.Id,
                x.BlissMatchId,
                x.CreatorId,
                x.AdvertiserOpportunityId,
                x.RuleVersionId,
                x.SourceSystem,
                x.IdempotencyKey,
                x.Status,
                x.Outcome,
                x.EvaluateOnCreate,
                x.CompletedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchFormationRunDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var run = await _db.MatchFormationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (run is null)
        {
            return NotFound();
        }

        return Ok(new MatchFormationRunDetailDto(
            run.Id,
            run.BlissMatchId,
            run.CreatorId,
            run.AdvertiserOpportunityId,
            run.RuleVersionId,
            run.SourceSystem,
            run.IdempotencyKey,
            run.Status,
            run.Outcome,
            run.EvaluateOnCreate,
            run.StartedAt,
            run.CompletedAt,
            run.InputSnapshot));
    }
}
