using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/advertiser-programs")]
public sealed class AdvertiserProgramsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public AdvertiserProgramsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdvertiserProgramListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.AdvertiserPrograms
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdvertiserProgramListDto(
                x.Id,
                x.AdvertiserId,
                x.Name,
                x.ExternalProgramId,
                x.Status))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}
