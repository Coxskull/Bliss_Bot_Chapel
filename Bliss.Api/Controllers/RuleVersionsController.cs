using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/rule-versions")]
public sealed class RuleVersionsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public RuleVersionsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RuleVersionDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.RuleVersions
            .AsNoTracking()
            .OrderBy(x => x.CreatedAt)
            .Select(x => new RuleVersionDto(
                x.Id,
                x.Version,
                x.Name,
                x.Description,
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }
}
