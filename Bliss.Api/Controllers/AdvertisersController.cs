using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/advertisers")]
public sealed class AdvertisersController : ControllerBase
{
    private readonly BlissDbContext _db;

    public AdvertisersController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdvertiserListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.Advertisers
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdvertiserListDto(x.Id, x.Name, x.Website, x.CountryCode))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdvertiserDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var advertiser = await _db.Advertisers
            .AsNoTracking()
            .Include(x => x.Programs)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (advertiser is null)
        {
            return NotFound();
        }

        return Ok(new AdvertiserDetailDto(
            advertiser.Id,
            advertiser.Name,
            advertiser.Website,
            advertiser.CountryCode,
            advertiser.Description,
            advertiser.CreatedAt,
            advertiser.Programs.Select(p => new AdvertiserProgramListDto(
                p.Id,
                p.AdvertiserId,
                p.Name,
                p.ExternalProgramId,
                p.Status)).ToList()));
    }
}
