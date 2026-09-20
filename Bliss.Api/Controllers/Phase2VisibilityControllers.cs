using Bliss.Api.Contracts;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/affiliate-networks")]
public sealed class AffiliateNetworksController : ControllerBase
{
    private readonly BlissDbContext _db;

    public AffiliateNetworksController(BlissDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AffiliateNetworkDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.AffiliateNetworks
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new AffiliateNetworkDto(x.Id, x.Name, x.Website, x.Status))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }
}

[ApiController]
[Route("api/network-accesses")]
public sealed class NetworkAccessesController : ControllerBase
{
    private readonly BlissDbContext _db;

    public NetworkAccessesController(BlissDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NetworkAccessDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.NetworkAccesses
            .AsNoTracking()
            .Select(x => new NetworkAccessDto(x.Id, x.AdvertiserId, x.AffiliateNetworkId, x.Status, x.ExternalAccountId, x.ApprovedAt))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }
}

[ApiController]
[Route("api/program-accesses")]
public sealed class ProgramAccessesController : ControllerBase
{
    private readonly BlissDbContext _db;

    public ProgramAccessesController(BlissDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProgramAccessDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.ProgramAccesses
            .AsNoTracking()
            .Select(x => new ProgramAccessDto(x.Id, x.AdvertiserProgramId, x.Status, x.ApprovedAt))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }
}

[ApiController]
[Route("api/data-provenances")]
public sealed class DataProvenancesController : ControllerBase
{
    private readonly BlissDbContext _db;

    public DataProvenancesController(BlissDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DataProvenanceDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.DataProvenances
            .AsNoTracking()
            .OrderBy(x => x.CollectedAt)
            .Select(x => new DataProvenanceDto(
                x.Id, x.EntityType, x.EntityId, x.FieldName, x.SourceType, x.SourceName, x.SourceUrl, x.ConfidenceLevel, x.CollectedAt, x.Notes))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }
}

[ApiController]
[Route("api/campaigns")]
public sealed class CampaignsController : ControllerBase
{
    private readonly BlissDbContext _db;

    public CampaignsController(BlissDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampaignListDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _db.Campaigns
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new CampaignListDto(x.Id, x.Name, x.Status, x.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(x => x.Placements)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (campaign is null)
        {
            return NotFound();
        }

        return Ok(new CampaignDetailDto(
            campaign.Id,
            campaign.Name,
            campaign.Status,
            campaign.CreatedAt,
            campaign.Placements.Select(p => new CampaignPlacementDto(
                p.Id, p.CampaignId, p.ContentItemId, p.AdInventorySlotId, p.Status, p.StartAt, p.EndAt)).ToList()));
    }
}
