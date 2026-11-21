using System.Data;
using Bliss.Api.Runtime;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Controllers;

// Customer draft intake only. Never creates placements, spend, creative jobs,
// verified commercial authority or outbound work.
[ApiController]
[Authorize]
[Route("api/advertisers/my-campaign-drafts")]
public sealed class AdvertiserCampaignDraftsController(BlissDbContext db, BlissAuthenticationOptions auth) : ControllerBase
{
    private const string Draft = "DRAFT";
    private const string Unverified = "UNVERIFIED";
    private const string DraftProgramMarker = "alpha:customer-campaign-draft:v1";

    private IQueryable<Guid> ActiveBusiness()
    {
        if (!auth.Enabled || !auth.SupabaseEnabled || !auth.AdvertiserSelfServiceRequested
            || !auth.AdvertiserCampaignDraftsRequested)
            return db.Advertisers.Where(x => false).Select(x => x.Id);

        var membershipLane = BlissAuthorization.CanResolveAdvertiserMembershipProfile(User, auth);
        var legacyLane = BlissAuthorization.TryResolveVerifiedAdvertiserId(User, auth, out var legacyId);
        var subject = User.FindFirst("sub")?.Value;
        if ((!membershipLane && !legacyLane) || string.IsNullOrWhiteSpace(subject))
            return db.Advertisers.Where(x => false).Select(x => x.Id);

        // Unlike the historical profile path, drafts ALWAYS require a registered,
        // live membership and original consent, even for an issuer-approved customer.
        return from m in db.AdvertiserAccountMemberships.AsNoTracking()
               join r in db.AdvertiserRegistrationReceipts.AsNoTracking()
                   on new { m.Subject, m.AdvertiserId } equals new { r.Subject, r.AdvertiserId }
               where m.Subject == subject && m.Status == "ACTIVE"
                   && (membershipLane || m.AdvertiserId == legacyId)
                   && r.ProcessingConsent && r.TermsAccepted
               select m.AdvertiserId;
    }

    // Compose predicates and ordering over mapped EF entities, not over a
    // constructed positional DTO. Project only at the final SQL boundary.
    private IQueryable<Campaign> OwnDrafts(Guid advertiserId) =>
        db.Campaigns.AsNoTracking().Where(c =>
            c.AdvertiserOpportunity != null
            && c.AdvertiserOpportunity.AdvertiserProgram.AdvertiserId == advertiserId
            && c.AdvertiserOpportunity.AdvertiserProgram.ExternalProgramId == DraftProgramMarker
            && c.AdvertiserOpportunity.AdvertiserProgram.Status == Draft
            && c.AdvertiserOpportunity.Status == Draft
            && c.AdvertiserOpportunity.AuthorityStatus == Unverified
            && c.Status == Draft && c.AuthorityStatus == Unverified);

    private static IQueryable<CampaignDraftSummary> Summaries(IQueryable<Campaign> source) =>
        source.Select(c => new CampaignDraftSummary(c.Id, c.Name,
            c.AdvertiserOpportunity!.Description!, c.Status, c.CreatedAt));

    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var allowed = ActiveBusiness();
        var id = await allowed.SingleOrDefaultAsync(ct);
        if (id == Guid.Empty) return StatusCode(403);
        return Ok(await Summaries(OwnDrafts(id).Where(_ => allowed.Contains(id))
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Take(50)).ToArrayAsync(ct));
    }

    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Create([FromBody] CampaignDraftRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var name = request.Name?.Trim();
        var brief = request.Brief?.Trim();
        if (request.RequestKey == Guid.Empty || string.IsNullOrWhiteSpace(name) || name.Length > 256
            || string.IsNullOrWhiteSpace(brief) || brief.Length > 4000)
            return BadRequest(new { error = "A request key, campaign name and brief are required." });
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var id = await ActiveBusiness().SingleOrDefaultAsync(ct);
            if (id == Guid.Empty) return StatusCode(403);
            var existing = await Summaries(OwnDrafts(id).Where(x => x.Id == request.RequestKey))
                .SingleOrDefaultAsync(ct);
            if (existing is not null)
                return existing.Name == name && existing.Brief == brief
                    ? Ok(existing) : Conflict(new { error = "Draft request conflicts with an existing record." });
            if (await db.Campaigns.AnyAsync(x => x.Id == request.RequestKey, ct))
                return Conflict(new { error = "Draft request conflicts with an existing record." });

            var program = new AdvertiserProgram { Id = Guid.NewGuid(), AdvertiserId = id,
                Name = name, ExternalProgramId = DraftProgramMarker, Status = Draft };
            var opportunity = new AdvertiserOpportunity { Id = Guid.NewGuid(), AdvertiserProgramId = program.Id,
                Name = name, Description = brief, Status = Draft, AuthorityStatus = Unverified };
            var campaign = new Campaign { Id = request.RequestKey, AdvertiserOpportunityId = opportunity.Id,
                Name = name, Status = Draft, AuthorityStatus = Unverified, CreatedAt = DateTime.UtcNow };
            db.AdvertiserPrograms.Add(program);
            db.AdvertiserOpportunities.Add(opportunity);
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return StatusCode(201, new CampaignDraftSummary(campaign.Id, name, brief, Draft, campaign.CreatedAt));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg &&
            (pg.SqlState == PostgresErrorCodes.UniqueViolation || pg.SqlState == PostgresErrorCodes.SerializationFailure))
        {
            return Conflict(new { error = "Draft changed concurrently. Retry with the same request key." });
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return Conflict(new { error = "Draft changed concurrently. Retry with the same request key." });
        }
    }
}

public sealed record CampaignDraftRequest(Guid RequestKey, string Name, string Brief);
public sealed record CampaignDraftSummary(Guid CampaignId, string Name, string Brief, string Status, DateTime CreatedAt);
