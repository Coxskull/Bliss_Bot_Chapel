using Bliss.Api.Contracts;
using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/creator-ingestions")]
public sealed class CreatorIngestionsController : ControllerBase
{
    private readonly BlissDbContext _db;
    private readonly CreatorIngestionService _ingestion;

    public CreatorIngestionsController(BlissDbContext db, CreatorIngestionService ingestion)
    {
        _db = db;
        _ingestion = ingestion;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreatorIngestionRunSummaryDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var items = await _db.CreatorIngestionRuns
            .AsNoTracking()
            .OrderByDescending(x => x.CompletedAt)
            .ThenBy(x => x.Id)
            .Select(x => new CreatorIngestionRunSummaryDto(
                x.Id,
                x.CreatorId,
                x.CreatorPlatformId,
                x.SourceSystem,
                x.IdempotencyKey,
                x.IdentityKey,
                x.Status,
                x.Outcome,
                x.CompletedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreatorIngestionRunDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var run = await _db.CreatorIngestionRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (run is null)
        {
            return NotFound();
        }

        return Ok(new CreatorIngestionRunDetailDto(
            run.Id,
            run.CreatorId,
            run.CreatorPlatformId,
            run.SourceSystem,
            run.IdempotencyKey,
            run.IdentityKey,
            run.Status,
            run.Outcome,
            run.StartedAt,
            run.CompletedAt,
            run.InputSnapshot));
    }

    [Authorize(Policy = BlissAuthorization.WritePolicy)]
    [HttpPost]
    public async Task<ActionResult<CreatorIngestionResultDto>> Create(
        CreatorIngestionRequest request,
        CancellationToken cancellationToken)
    {
        CreatorIngestionResult result;
        try
        {
            result = await _ingestion.IngestAsync(
                new CreatorIngestionCommand(
                    request.SourceSystem,
                    request.IdempotencyKey,
                    request.Platform,
                    request.ExternalProfileId,
                    request.CreatorName,
                    request.ProfileUrl,
                    request.CountryCode,
                    request.PrimaryLanguage,
                    request.AudienceSize,
                    request.Followers,
                    request.FemalePercentage,
                    request.MalePercentage,
                    request.PrimaryAgeRange,
                    request.PrimaryGeography,
                    request.EngagementLevel,
                    request.SourceUrl,
                    request.ConfidenceLevel,
                    request.CollectedAt),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var dto = new CreatorIngestionResultDto(
            result.RunId,
            result.CreatorId,
            result.CreatorPlatformId,
            result.IdentityKey,
            result.SourceSystem,
            result.IdempotencyKey,
            result.Status,
            result.Outcome,
            result.CompletedAt,
            result.IsReplay);

        return result.IsReplay
            ? Ok(dto)
            : CreatedAtAction(nameof(GetById), new { id = result.RunId }, dto);
    }
}
