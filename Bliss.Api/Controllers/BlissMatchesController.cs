using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/bliss/matches")]
public sealed class BlissMatchesController : ControllerBase
{
    private readonly BlissDbContext _db;

    public BlissMatchesController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BlissMatchSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.BlissMatches
            .AsNoTracking()
            .OrderBy(x => x.CreatedAt)
            .Select(x => new BlissMatchSummaryDto(
                x.Id,
                x.CreatorId,
                x.AdvertiserOpportunityId,
                x.RuleVersionId,
                x.Status,
                x.OverallScore,
                x.ConfidenceScore,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BlissMatchDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var match = await _db.BlissMatches
            .AsNoTracking()
            .Include(x => x.Creator)
            .Include(x => x.AdvertiserOpportunity)
                .ThenInclude(x => x.AdvertiserProgram)
                    .ThenInclude(x => x.Advertiser)
            .Include(x => x.RuleVersion)
            .Include(x => x.ScoreComponents)
            .Include(x => x.EligibilityChecks)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (match is null)
        {
            return NotFound();
        }

        return Ok(new BlissMatchDetailDto(
            match.Id,
            match.Status,
            match.OverallScore,
            match.ConfidenceScore,
            match.CreatedAt,
            new CreatorListDto(
                match.Creator.Id,
                match.Creator.Name,
                match.Creator.CountryCode,
                match.Creator.PrimaryLanguage,
                match.Creator.AudienceSize,
                match.Creator.FemalePercentage,
                match.Creator.MalePercentage),
            new AdvertiserOpportunityDetailDto(
                match.AdvertiserOpportunity.Id,
                match.AdvertiserOpportunity.Name,
                match.AdvertiserOpportunity.ProductName,
                match.AdvertiserOpportunity.Category,
                match.AdvertiserOpportunity.Description,
                match.AdvertiserOpportunity.Status,
                new AdvertiserProgramDetailDto(
                    match.AdvertiserOpportunity.AdvertiserProgram.Id,
                    match.AdvertiserOpportunity.AdvertiserProgram.Name,
                    match.AdvertiserOpportunity.AdvertiserProgram.Status,
                    new AdvertiserListDto(
                        match.AdvertiserOpportunity.AdvertiserProgram.Advertiser.Id,
                        match.AdvertiserOpportunity.AdvertiserProgram.Advertiser.Name,
                        match.AdvertiserOpportunity.AdvertiserProgram.Advertiser.Website,
                        match.AdvertiserOpportunity.AdvertiserProgram.Advertiser.CountryCode))),
            new RuleVersionDto(
                match.RuleVersion.Id,
                match.RuleVersion.Version,
                match.RuleVersion.Name,
                match.RuleVersion.Description,
                match.RuleVersion.IsActive,
                match.RuleVersion.CreatedAt),
            match.ScoreComponents.Select(s => new MatchScoreComponentDto(
                s.Id,
                s.ComponentName,
                s.Score,
                s.Weight,
                s.Explanation)).ToList(),
            match.EligibilityChecks.Select(e => new EligibilityCheckDto(
                e.Id,
                e.CheckType,
                e.Result,
                e.ReasonCode,
                e.Explanation)).ToList()));
    }
}
