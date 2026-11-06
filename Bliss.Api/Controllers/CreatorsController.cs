using Bliss.Api.Contracts;
using Bliss.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/creators")]
public sealed class CreatorsController : ControllerBase
{
    private readonly BlissDbContext _db;
    private readonly BlissAuthenticationOptions _auth;

    public CreatorsController(BlissDbContext db, BlissAuthenticationOptions auth)
    {
        _db = db;
        _auth = auth;
    }

    // Creator-owned account preview. This is not a public registration
    // endpoint. It is fail-closed until the separate issuer/verification,
    // Railway ingress, recovery and legal acceptance gates are approved.
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CreatorSelfSummaryDto>> GetMyVerifiedCreator(
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!BlissAuthorization.TryResolveVerifiedCreatorId(User, _auth, out var creatorId))
            return StatusCode(StatusCodes.Status403Forbidden);

        // Do not disclose unverified creators or details from a different
        // tenant even if an issuer claim or cookie was replayed or became stale.
        var record = await _db.Creators.AsNoTracking()
            .Where(c => c.Id == creatorId && c.AuthorityStatus == "VERIFIED")
            .Select(c => new CreatorSelfSummaryDto(
                c.Id, c.Name, c.CountryCode, c.PrimaryLanguage,
                c.AuthorityStatus))
            .SingleOrDefaultAsync(cancellationToken);
        return record is null ? NotFound() : Ok(record);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CreatorListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.Creators
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new CreatorListDto(
                x.Id,
                x.Name,
                x.CountryCode,
                x.PrimaryLanguage,
                x.AudienceSize,
                x.FemalePercentage,
                x.MalePercentage))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreatorDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var creator = await _db.Creators
            .AsNoTracking()
            .Include(x => x.Platforms)
            .Include(x => x.ContentItems)
                .ThenInclude(x => x.AdInventorySlots)
            .Include(x => x.BlissMatches)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (creator is null)
        {
            return NotFound();
        }

        var dto = new CreatorDetailDto(
            creator.Id,
            creator.Name,
            creator.CountryCode,
            creator.PrimaryLanguage,
            creator.AudienceSize,
            creator.FemalePercentage,
            creator.MalePercentage,
            creator.PrimaryAgeRange,
            creator.PrimaryGeography,
            creator.EngagementLevel,
            creator.CreatedAt,
            creator.UpdatedAt,
            creator.Platforms.Select(p => new CreatorPlatformDto(
                p.Id,
                p.Platform,
                p.ExternalProfileId,
                p.ProfileUrl,
                p.Followers,
                p.LastCollectedAt)).ToList(),
            creator.ContentItems.Select(c => new ContentItemDetailDto(
                c.Id,
                c.CreatorId,
                c.ContentType,
                c.Title,
                c.ExternalContentId,
                c.Url,
                c.PublishedAt,
                c.CreatedAt,
                c.AdInventorySlots.Select(s => new AdInventorySlotDto(
                    s.Id,
                    s.SlotType,
                    s.StartSecond,
                    s.DurationSeconds,
                    s.IsAvailable)).ToList())).ToList(),
            creator.BlissMatches.Select(m => new BlissMatchSummaryDto(
                m.Id,
                m.CreatorId,
                m.AdvertiserOpportunityId,
                m.RuleVersionId,
                m.Status,
                m.OverallScore,
                m.ConfidenceScore,
                m.CreatedAt)).ToList());

        return Ok(dto);
    }
}

// Deliberately minimal read-only response; never include prospect emails,
// CRM contact IDs, intake receipts, inventory or marketplace credentials.
public sealed record CreatorSelfSummaryDto(
    Guid CreatorId, string Name, string? CountryCode,
    string? PrimaryLanguage, string AuthorityStatus);
