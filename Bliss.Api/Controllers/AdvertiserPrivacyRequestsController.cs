using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Controllers;

/// <summary>
/// A durable request-intake lane, NOT a self-service erasure or completed withdrawal.
/// Operator handling, response deadlines and final dispositions are separate release gates.
/// </summary>
[ApiController]
[Authorize]
[Route("api/advertisers/privacy-requests")]
public sealed class AdvertiserPrivacyRequestsController(BlissDbContext db, BlissAuthenticationOptions auth) : ControllerBase
{
    private static readonly HashSet<string> Types = new(StringComparer.Ordinal)
    {
        "ACCESS", "CORRECTION", "ERASURE", "MARKETING_WITHDRAWAL", "OBJECTION", "ACCOUNT_CLOSURE"
    };

    [HttpPost]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(4 * 1024)]
    public async Task<IActionResult> Create([FromBody] AdvertiserPrivacyCaseRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var membership = await ResolveMembershipAsync(cancellationToken);
        if (membership is null) return StatusCode(StatusCodes.Status403Forbidden);

        if (request.RequestKey == Guid.Empty || request.RequestType is null || !Types.Contains(request.RequestType))
            return BadRequest(new { error = "Select a supported account privacy request type." });

        var subject = membership.Subject;
        var previous = await db.AdvertiserPrivacyRequests.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, cancellationToken);
        if (previous is not null) return ReplayOrConflict(previous, subject, membership.AdvertiserId, request.RequestType);

        var record = new AdvertiserPrivacyRequest
        {
            Id = Guid.NewGuid(),
            RequestKey = request.RequestKey,
            Subject = subject,
            AdvertiserId = membership.AdvertiserId,
            RequestType = request.RequestType,
            Status = "RECEIVED",
            CreatedAt = DateTime.UtcNow
        };

        db.AdvertiserPrivacyRequests.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.AdvertiserPrivacyRequests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, cancellationToken);
            if (concurrent is not null)
                return ReplayOrConflict(concurrent, subject, membership.AdvertiserId, request.RequestType);
            return Conflict(new { error = "A duplicate privacy request was rejected." });
        }

        return StatusCode(StatusCodes.Status201Created, Receipt(record));
    }

    [HttpGet]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var membership = await ResolveMembershipAsync(cancellationToken);
        if (membership is null) return StatusCode(StatusCodes.Status403Forbidden);

        var requests = await db.AdvertiserPrivacyRequests.AsNoTracking()
            .Where(x => x.Subject == membership.Subject && x.AdvertiserId == membership.AdvertiserId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(25)
            .Select(x => new AdvertiserPrivacyCaseReceipt(x.Id, x.RequestType, x.Status, x.CreatedAt))
            .ToArrayAsync(cancellationToken);
        return Ok(requests);
    }

    private async Task<AdvertiserAccountMembership?> ResolveMembershipAsync(CancellationToken cancellationToken)
    {
        if (!auth.Enabled || !auth.SupabaseEnabled || User.Identity?.IsAuthenticated != true)
            return null;
        var subjects = User.FindAll("sub").ToArray();
        if (subjects.Length != 1 || subjects[0].Type != "sub" || string.IsNullOrWhiteSpace(subjects[0].Value))
            return null;

        var pending = BlissAuthorization.IsPendingAdvertiserRegistration(User, auth);
        var approved = BlissAuthorization.TryResolveVerifiedAdvertiserId(User, auth, out var advertiserId);
        if (!pending && !approved) return null;

        var member = await db.AdvertiserAccountMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Subject == subjects[0].Value, cancellationToken);
        if (member is null) return null;

        if (pending && member.Status == "PENDING_REVIEW") return member;
        return approved && member.Status == "ACTIVE" && member.AdvertiserId == advertiserId
            ? member
            : null;
    }

    private IActionResult ReplayOrConflict(AdvertiserPrivacyRequest previous, string subject, Guid advertiserId, string type) =>
        previous.Subject == subject && previous.AdvertiserId == advertiserId && previous.RequestType == type
            ? Ok(Receipt(previous))
            : Conflict(new { error = "The request key is already in use." });

    private static AdvertiserPrivacyCaseReceipt Receipt(AdvertiserPrivacyRequest record) =>
        new(record.Id, record.RequestType, record.Status, record.CreatedAt);
}

public sealed record AdvertiserPrivacyCaseRequest(Guid RequestKey, string RequestType);
public sealed record AdvertiserPrivacyCaseReceipt(Guid CaseId, string RequestType, string Status, DateTime CreatedAt);
