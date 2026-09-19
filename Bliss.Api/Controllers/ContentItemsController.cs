using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/content-items")]
public sealed class ContentItemsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public ContentItemsController(BlissDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContentItemListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.ContentItems
            .AsNoTracking()
            .OrderBy(x => x.Title)
            .Select(x => new ContentItemListDto(
                x.Id,
                x.CreatorId,
                x.ContentType,
                x.Title,
                x.ExternalContentId,
                x.Url))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContentItemDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _db.ContentItems
            .AsNoTracking()
            .Include(x => x.AdInventorySlots)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        return Ok(new ContentItemDetailDto(
            item.Id,
            item.CreatorId,
            item.ContentType,
            item.Title,
            item.ExternalContentId,
            item.Url,
            item.PublishedAt,
            item.CreatedAt,
            item.AdInventorySlots.Select(s => new AdInventorySlotDto(
                s.Id,
                s.SlotType,
                s.StartSecond,
                s.DurationSeconds,
                s.IsAvailable)).ToList()));
    }
}
