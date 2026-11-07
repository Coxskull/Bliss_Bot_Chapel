using Bliss.Api.Security;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/advertisers")]
public sealed class AdvertiserAccountController : ControllerBase
{
    private readonly BlissDbContext _db;
    private readonly BlissAuthenticationOptions _auth;

    public AdvertiserAccountController(BlissDbContext db, BlissAuthenticationOptions auth)
    {
        _db = db;
        _auth = auth;
    }

    // Read-only, own-tenant profile: never an intake/CRM lookup.
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AdvertiserSelfSummaryDto>> GetMyBusiness(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!BlissAuthorization.TryResolveVerifiedAdvertiserId(User, _auth, out var advertiserId))
            return StatusCode(StatusCodes.Status403Forbidden);

        var subject = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return StatusCode(StatusCodes.Status403Forbidden);
        var membership = await _db.AdvertiserAccountMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Subject == subject && x.AdvertiserId == advertiserId, cancellationToken);
        // Existing workspaces remain bound by the issuer's approved subject →
        // advertiser_id assignment. New self-registrations must additionally
        // have an active database owner link before any workspace access.
        if (membership is not null && membership.Status != "ACTIVE")
            return StatusCode(StatusCodes.Status403Forbidden);

        var record = await _db.Advertisers.AsNoTracking()
            .Where(a => a.Id == membership.AdvertiserId)
            .Select(a => new AdvertiserSelfSummaryDto(a.Id, a.Name, a.CountryCode, a.Website))
            .SingleOrDefaultAsync(cancellationToken);
        return record is null ? NotFound() : Ok(record);
    }
}

// Minimal business identity only; never disclose private prospect, lead,
// campaign, billing, match, CRM or payout records.
public sealed record AdvertiserSelfSummaryDto(Guid AdvertiserId, string Name,
    string? CountryCode, string? Website);
