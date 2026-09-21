using Bliss.Api.Contracts;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Common;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/match-review-decisions")]
public sealed class MatchReviewDecisionsController : ControllerBase
{
    private readonly BlissDbContext _db;
    private readonly MatchReviewService _review;
    private readonly OperatorIdentity _operatorIdentity;

    public MatchReviewDecisionsController(
        BlissDbContext db,
        MatchReviewService review,
        OperatorIdentity operatorIdentity)
    {
        _db = db;
        _review = review;
        _operatorIdentity = operatorIdentity;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MatchReviewDecisionSummaryDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var items = await _db.MatchReviewDecisions
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Select(x => new MatchReviewDecisionSummaryDto(
                x.Id,
                x.BlissMatchId,
                x.CreatorId,
                x.MatchEvaluationRunId,
                x.SourceSystem,
                x.IdempotencyKey,
                x.ReviewerLabel,
                x.Decision,
                x.ResultingMatchStatus,
                x.CompletedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchReviewDecisionDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _db.MatchReviewDecisions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        return Ok(new MatchReviewDecisionDetailDto(
            item.Id,
            item.BlissMatchId,
            item.CreatorId,
            item.MatchEvaluationRunId,
            item.SourceSystem,
            item.IdempotencyKey,
            item.ReviewerLabel,
            item.Decision,
            item.ResultingMatchStatus,
            item.Rationale,
            item.Status,
            item.StartedAt,
            item.CompletedAt,
            item.InputSnapshot));
    }

    [Authorize(Policy = BlissAuthorization.ReviewPolicy)]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [HttpPost]
    public async Task<ActionResult<MatchReviewResultDto>> Create(
        MatchReviewRequest request,
        CancellationToken cancellationToken)
    {
        MatchReviewResult result;
        try
        {
            result = await _review.DecideAsync(
                new MatchReviewCommand(
                    request.SourceSystem,
                    request.IdempotencyKey,
                    request.BlissMatchId,
                    _operatorIdentity.ResolveLabel(User, request.ReviewerLabel),
                    request.Decision,
                    request.Rationale),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var dto = new MatchReviewResultDto(
            result.DecisionId,
            result.BlissMatchId,
            result.CreatorId,
            result.MatchEvaluationRunId,
            result.SourceSystem,
            result.IdempotencyKey,
            result.ReviewerLabel,
            result.Decision,
            result.ResultingMatchStatus,
            result.Status,
            result.CompletedAt,
            result.IsReplay);

        return result.IsReplay
            ? Ok(dto)
            : CreatedAtAction(nameof(GetById), new { id = result.DecisionId }, dto);
    }
}

[ApiController]
[Route("api/match-reviews")]
public sealed class MatchReviewsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public MatchReviewsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet("queue")]
    public async Task<ActionResult<IReadOnlyList<MatchReviewQueueItemDto>>> GetQueue(
        CancellationToken cancellationToken)
    {
        var items = await _db.BlissMatches
            .AsNoTracking()
            .Where(x => x.Status == EntityStatuses.ReviewRequired)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new MatchReviewQueueItemDto(
                x.Id,
                x.CreatorId,
                x.Creator.Name,
                x.AdvertiserOpportunityId,
                x.AdvertiserOpportunity.Name,
                x.RuleVersionId,
                x.Status,
                x.OverallScore,
                x.ConfidenceScore,
                x.CreatedAt,
                _db.MatchEvaluationRuns.Count(run => run.BlissMatchId == x.Id)))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}
