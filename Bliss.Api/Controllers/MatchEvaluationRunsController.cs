using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/match-evaluation-runs")]
public sealed class MatchEvaluationRunsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public MatchEvaluationRunsController(BlissDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchEvaluationRunSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.MatchEvaluationRuns
            .AsNoTracking()
            .OrderBy(x => x.StartedAt)
            .ThenBy(x => x.Id)
            .Select(x => new MatchEvaluationRunSummaryDto(
                x.Id,
                x.BlissMatchId,
                x.CreatorId,
                x.RuleVersionId,
                x.AlgorithmVersion,
                x.Status,
                x.MatchStatus,
                x.OverallScore,
                x.ConfidenceScore,
                x.StartedAt,
                x.CompletedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchEvaluationRunDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var run = await _db.MatchEvaluationRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (run is null)
        {
            return NotFound();
        }

        return Ok(new MatchEvaluationRunDetailDto(
            run.Id,
            run.BlissMatchId,
            run.CreatorId,
            run.RuleVersionId,
            run.AlgorithmVersion,
            run.Status,
            run.MatchStatus,
            run.OverallScore,
            run.ConfidenceScore,
            run.StartedAt,
            run.CompletedAt,
            run.InputSnapshot,
            run.OutputSnapshot));
    }
}
