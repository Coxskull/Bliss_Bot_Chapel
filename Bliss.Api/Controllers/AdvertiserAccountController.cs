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
        if (BlissAuthorization.CanResolveAdvertiserMembershipProfile(User, _auth))
        {
            var verifiedSubject = User.FindFirst("sub")!.Value;
            // One query binds the signed-in subject to the approved membership,
            // original consents and business row. Revocation takes effect at the
            // next request, even with a previously authenticated browser cookie.
            var own = await (from m in _db.AdvertiserAccountMemberships.AsNoTracking()
                             join r in _db.AdvertiserRegistrationReceipts.AsNoTracking()
                                 on new { m.Subject, m.AdvertiserId } equals new { r.Subject, r.AdvertiserId }
                             join b in _db.Advertisers.AsNoTracking() on m.AdvertiserId equals b.Id
                             where m.Subject == verifiedSubject && m.Status == "ACTIVE"
                                 && r.ProcessingConsent && r.TermsAccepted
                             select new AdvertiserSelfSummaryDto(b.Id, b.Name, b.CountryCode, b.Website))
                .SingleOrDefaultAsync(cancellationToken);
            return own is null ? StatusCode(StatusCodes.Status403Forbidden) : Ok(own);
        }
        if (!BlissAuthorization.TryResolveVerifiedAdvertiserId(User, _auth, out var advertiserId))
            return StatusCode(StatusCodes.Status403Forbidden);

        var subject = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return StatusCode(StatusCodes.Status403Forbidden);
        // If either the subject or claimed business participates in registration,
        // an exact ACTIVE pair is mandatory. A mismatched issuer assignment
        // must not look like an old workspace with no membership.
        var memberships = await _db.AdvertiserAccountMemberships.AsNoTracking()
            .Where(x => x.Subject == subject || x.AdvertiserId == advertiserId)
            .Select(x => new { x.Subject, x.AdvertiserId, x.Status })
            .ToListAsync(cancellationToken);
        if (memberships.Count != 0 && (memberships.Count != 1
            || memberships[0].Subject != subject || memberships[0].AdvertiserId != advertiserId
            || memberships[0].Status != "ACTIVE"))
            return StatusCode(StatusCodes.Status403Forbidden);

        var record = await _db.Advertisers.AsNoTracking()
            .Where(a => a.Id == advertiserId)
            .Select(a => new AdvertiserSelfSummaryDto(a.Id, a.Name, a.CountryCode, a.Website))
            .SingleOrDefaultAsync(cancellationToken);
        return record is null ? NotFound() : Ok(record);
    }
}

// Minimal business identity only; never disclose private prospect, lead,
// campaign, billing, match, CRM or payout records.
public sealed record AdvertiserSelfSummaryDto(Guid AdvertiserId, string Name,
    string? CountryCode, string? Website);
