using Bliss.Api.Runtime;
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

    // Staff visibility is independently default-denied: both a verified issuer role
    // and an explicitly configured trusted operator subject are required.
    [HttpGet("operator-queue")]
    public async Task<IActionResult> OperatorQueue(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsVerifiedPrivacyOperator(out _)) return StatusCode(StatusCodes.Status403Forbidden);

        var cases = await db.AdvertiserPrivacyRequests.AsNoTracking()
            .Where(x => x.Status == "RECEIVED" || x.Status == "VERIFYING")
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .Select(x => new AdvertiserPrivacyOperatorCase(x.Id, x.Subject, x.AdvertiserId,
                x.RequestType, x.Status, x.CreatedAt, x.AcknowledgedAt))
            .ToArrayAsync(cancellationToken);
        return Ok(cases);
    }

    // This is an ACK / identity-verification queue transition, NOT an erasure,
    // marketing withdrawal, or resolution claim. It records who acknowledged it.
    [HttpPost("{caseId:guid}/acknowledge")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    public async Task<IActionResult> Acknowledge(Guid caseId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsVerifiedPrivacyOperator(out var operatorSubject))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (caseId == Guid.Empty) return BadRequest(new { error = "Invalid case identifier." });

        var acknowledgedAt = DateTime.UtcNow;
        var count = await db.AdvertiserPrivacyRequests
            .Where(x => x.Id == caseId && x.Status == "RECEIVED")
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, "VERIFYING")
                .SetProperty(x => x.AcknowledgedBySubject, operatorSubject)
                .SetProperty(x => x.AcknowledgedAt, acknowledgedAt), cancellationToken);
        if (count == 1)
            return Ok(new { caseId, status = "VERIFYING", acknowledgedAt });
        var exists = await db.AdvertiserPrivacyRequests.AsNoTracking()
            .AnyAsync(x => x.Id == caseId, cancellationToken);
        return exists ? Conflict(new { error = "Case already acknowledged or disposition changed." }) : NotFound();
    }

    private bool IsVerifiedPrivacyOperator(out string operatorSubject)
    {
        operatorSubject = string.Empty;
        if (!auth.Enabled || !auth.SupabaseEnabled || !auth.AdvertiserPrivacyRequestsRequested
            || User.Identity?.IsAuthenticated != true
            || string.IsNullOrWhiteSpace(auth.Authority) || string.IsNullOrWhiteSpace(auth.RoleClaimType)
            || User.Claims.Any(claim => string.Equals(claim.Type,
                BlissAuthenticationOptions.AdvertiserRegistrationPendingMarkerClaimType,
                StringComparison.OrdinalIgnoreCase)))
            return false;
        var subjects = User.Claims.Where(c => string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (subjects.Length != 1 || subjects[0].Type != "sub"
            || string.IsNullOrWhiteSpace(subjects[0].Value)
            || auth.ApprovedPrivacyOperatorSubjects?.Contains(subjects[0].Value, StringComparer.Ordinal) != true
            || auth.ApprovedTestSubjects.Contains(subjects[0].Value, StringComparer.Ordinal))
            return false;

        var roles = User.Claims.Where(c => string.Equals(c.Type, auth.RoleClaimType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, System.Security.Claims.ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, "roles", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (roles.Length != 1 || roles[0].Type != auth.RoleClaimType
            || roles[0].Issuer != auth.Authority || roles[0].OriginalIssuer != auth.Authority
            || (roles[0].Value != auth.OperatorRole && roles[0].Value != auth.AdminRole)
            || User.HasClaim(c => c.Type == auth.AdvertiserIdClaimType
                || c.Type == auth.CreatorIdClaimType
                || c.Type == auth.ReviewerWorkspaceClaimType))
            return false;
        operatorSubject = subjects[0].Value;
        return true;
    }

    private async Task<AdvertiserAccountMembership?> ResolveMembershipAsync(CancellationToken cancellationToken)
    {
        if (!auth.Enabled || !auth.SupabaseEnabled || !auth.AdvertiserPrivacyRequestsRequested
            || User.Identity?.IsAuthenticated != true)
            return null;
        var subjects = User.FindAll("sub").ToArray();
        if (subjects.Length != 1 || subjects[0].Type != "sub" || string.IsNullOrWhiteSpace(subjects[0].Value))
            return null;

        var pending = BlissAuthorization.IsPendingAdvertiserIdentity(User, auth);
        var approved = BlissAuthorization.TryResolveVerifiedAdvertiserId(User, auth, out var advertiserId);
        if (!pending && !approved) return null;

        var member = await db.AdvertiserAccountMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Subject == subjects[0].Value, cancellationToken);
        if (member is null) return null;

        // Account revocation must never reactivate business or marketplace
        // permissions, but it must not silently remove a verified person's
        // ability to request access/correction/deletion of retained records.
        // The issuer-bound subject and exact database membership remain
        // mandatory; this exception is scoped to this privacy-only controller.
        if (pending && (member.Status == "PENDING_REVIEW" || member.Status == "REVOKED"))
            return member;
        return approved && (member.Status == "ACTIVE" || member.Status == "REVOKED")
            && member.AdvertiserId == advertiserId
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

public sealed record AdvertiserPrivacyOperatorCase(Guid CaseId, string Subject, Guid AdvertiserId,
    string RequestType, string Status, DateTime CreatedAt, DateTime? AcknowledgedAt);
