using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/advertiser-opportunities")]
public sealed class AdvertiserOpportunitiesController : ControllerBase
{
    private readonly BlissDbContext _db;

    public AdvertiserOpportunitiesController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdvertiserOpportunityListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.AdvertiserOpportunities
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AdvertiserOpportunityListDto(
                x.Id,
                x.AdvertiserProgramId,
                x.Name,
                x.ProductName,
                x.Category,
                x.Status))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}
