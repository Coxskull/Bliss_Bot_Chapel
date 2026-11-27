using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Security;
using Bliss.Api.Operations;
using Bliss.Api.Runtime;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/creators")]
public sealed class CreatorRegistrationController(BlissDbContext db, BlissAuthenticationOptions auth) : ControllerBase
{
    public const string PrivacyNoticeVersion = "creator-privacy-2026-11-22-v2";
    public const string TermsVersion = "creator-terms-2026-11-22-v2";

    [Authorize]
    [HttpPost("register")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Register([FromBody] CreatorRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!BlissAuthorization.IsPendingCreatorRegistration(User, auth))
            return StatusCode(StatusCodes.Status403Forbidden);

        var subject = User.FindFirst("sub")!.Value;
        if (request.RequestKey == Guid.Empty || !request.ProcessingConsent || !request.TermsAccepted
            || request.PrivacyNoticeVersion != PrivacyNoticeVersion || request.TermsVersion != TermsVersion)
            return BadRequest(new { error = "Privacy notice, terms, and processing consent must be accepted explicitly." });

        var name = request.DisplayName?.Trim();
        var country = request.CountryCode?.Trim().ToUpperInvariant();
        var language = request.PrimaryLanguage?.Trim();
        var channelUrl = request.ChannelUrl?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256
            || string.IsNullOrWhiteSpace(country) || country.Length != 2 || country.Any(c => c is < 'A' or > 'Z')
            || (!string.IsNullOrEmpty(language) && (language.Length > 35
                || language.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-'))))
            || !Uri.TryCreate(channelUrl, UriKind.Absolute, out var parsedChannel)
            || parsedChannel.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(parsedChannel.UserInfo)
            || channelUrl!.Length > 1024)
            return BadRequest(new { error = "Creator name, country code, or primary language is invalid." });

        var canonical = JsonSerializer.Serialize(new
        {
            displayName = name,
            countryCode = country,
            primaryLanguage = language,
            channelUrl,
            processingConsent = request.ProcessingConsent,
            termsAccepted = request.TermsAccepted,
            marketingOptIn = request.MarketingOptIn,
            privacyNoticeVersion = request.PrivacyNoticeVersion,
            termsVersion = request.TermsVersion
        });
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        var priorRequest = await db.CreatorRegistrationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, cancellationToken);
        if (priorRequest is not null)
            return ReplayOrConflict(priorRequest, subject, digest);
        if (await db.CreatorAccountMemberships.AnyAsync(x => x.Subject == subject, cancellationToken))
            return Conflict(new { error = "This identity already has a creator registration." });

        var creator = new Creator
        {
            Id = Guid.NewGuid(), Name = name, CountryCode = country,
            PrimaryLanguage = string.IsNullOrEmpty(language) ? null : language,
            AuthorityStatus = "UNVERIFIED", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var membership = new CreatorAccountMembership
        {
            Id = Guid.NewGuid(), Subject = subject, CreatorId = creator.Id,
            SubmittedChannelUrl = channelUrl!,
            Status = "PENDING_REVIEW", CreatedAt = DateTime.UtcNow
        };
        var receipt = new CreatorRegistrationReceipt
        {
            Id = Guid.NewGuid(), RequestKey = request.RequestKey, Subject = subject,
            CreatorId = creator.Id, RequestSha256 = digest,
            PrivacyNoticeVersion = PrivacyNoticeVersion, TermsVersion = TermsVersion,
            ProcessingConsent = true, TermsAccepted = true, MarketingOptIn = request.MarketingOptIn,
            CreatedAt = DateTime.UtcNow
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Creators.Add(creator);
        db.CreatorAccountMemberships.Add(membership);
        db.CreatorRegistrationReceipts.Add(receipt);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var concurrentReplay = await db.CreatorRegistrationReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, cancellationToken);
            if (concurrentReplay is not null) return ReplayOrConflict(concurrentReplay, subject, digest);
            return Conflict(new { error = "This identity or creator is already registered." });
        }

        return StatusCode(StatusCodes.Status201Created, new CreatorRegistrationReceiptDto(
            receipt.Id, creator.Id, "PENDING_REVIEW", receipt.CreatedAt, receipt.MarketingOptIn));
    }

    [Authorize]
    [HttpGet("registration")]
    public async Task<ActionResult<CreatorRegistrationStatusDto>> GetMyRegistration(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!BlissAuthorization.IsPendingCreatorRegistration(User, auth))
            return StatusCode(StatusCodes.Status403Forbidden);
        var subject = User.FindFirst("sub")!.Value;
        var membership = await db.CreatorAccountMemberships.AsNoTracking().Include(m => m.Creator)
            .SingleOrDefaultAsync(x => x.Subject == subject, cancellationToken);
        if (membership is null) return NotFound();
        var receipt = await db.CreatorRegistrationReceipts.AsNoTracking()
            .Where(x => x.Subject == subject && x.CreatorId == membership.CreatorId)
            .Select(x => new { x.Id, x.CreatedAt, x.RequestKey, x.ProcessingConsent, x.TermsAccepted, x.PrivacyNoticeVersion, x.TermsVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (receipt is null) return NotFound();
        var approved = auth.CreatorAuthorityVerificationRequested && membership.Status == "ACTIVE"
            && receipt.ProcessingConsent && receipt.TermsAccepted
            && receipt.PrivacyNoticeVersion == PrivacyNoticeVersion && receipt.TermsVersion == TermsVersion;
        var now = DateTime.UtcNow;
        var code = approved && membership.Creator.AuthorityStatus == "UNVERIFIED"
            ? CreatorChannelProof.Code(receipt.Id, receipt.RequestKey, membership.CreatorId, membership.SubmittedChannelUrl, now) : null;
        var profile = approved && membership.Creator.AuthorityStatus == "VERIFIED"
            ? new CreatorSelfSummaryDto(membership.CreatorId, membership.Creator.Name, membership.Creator.CountryCode,
                membership.Creator.PrimaryLanguage, "VERIFIED") : null;
        if (profile is not null && auth.CreatorInventoryReadbackRequested)
            profile = profile with { Inventory = await CreatorsController.ReadOwnInventory(db, membership.CreatorId, cancellationToken),
                CanPauseInventory = auth.CreatorInventoryPauseRequested,
                InventoryDraftContent = auth.CreatorInventoryDraftRequested
                    ? await db.ContentItems.AsNoTracking().Where(c => c.CreatorId == membership.CreatorId
                        && c.Creator.AuthorityStatus == "VERIFIED").OrderBy(c => c.Id).Take(50)
                        .Select(c => new CreatorInventoryContentSummary(c.Id, c.Title, c.ContentType)).ToArrayAsync(cancellationToken)
                    : null };
        if (profile is not null && auth.CreatorCampaignInvitationRequested)
            profile = profile with { CanViewCampaignInvitations = true };
        return Ok(new CreatorRegistrationStatusDto(membership.CreatorId, receipt.Id, membership.Status, receipt.CreatedAt,
            code, code is null ? null : membership.SubmittedChannelUrl,
            code is null ? null : now.Date.AddDays(1), profile));
    }

    // A creator may withdraw their own available slot from future matching.
    // No re-enable, existing placement cancellation, pricing or delivery authority.
    [Authorize]
    [HttpPost("inventory/pause")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> PauseOwnInventory([FromBody] CreatorInventoryPauseRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorInventoryReadbackRequested || !auth.CreatorInventoryPauseRequested
            || !auth.CreatorAuthorityVerificationRequested || !BlissAuthorization.IsPendingCreatorRegistration(User, auth))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (request.SlotId == Guid.Empty || !request.ConfirmPause) return BadRequest();
        var subject = User.FindFirst("sub")!.Value;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var creatorId = await (from m in db.CreatorAccountMemberships.AsNoTracking()
                join r in db.CreatorRegistrationReceipts.AsNoTracking()
                    on new { m.Subject, m.CreatorId } equals new { r.Subject, r.CreatorId }
                where m.Subject == subject && m.Status == "ACTIVE" && m.Creator.AuthorityStatus == "VERIFIED"
                    && r.ProcessingConsent && r.TermsAccepted && r.PrivacyNoticeVersion == PrivacyNoticeVersion
                    && r.TermsVersion == TermsVersion
                select (Guid?)m.CreatorId).SingleOrDefaultAsync(ct);
            if (creatorId is null) return StatusCode(StatusCodes.Status403Forbidden);
            var slot = await db.AdInventorySlots.SingleOrDefaultAsync(s => s.Id == request.SlotId
                && s.ContentItem.CreatorId == creatorId.Value && s.ContentItem.Creator.AuthorityStatus == "VERIFIED", ct);
            if (slot is null) return NotFound();
            slot.IsAvailable = false;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Ok(new { slotId = slot.Id, isAvailable = false, existingPlacementsChanged = false,
                bookingAuthorized = false, paymentAuthorized = false, deliveryAuthorized = false });
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure) { return Conflict(); }
        catch (DbUpdateException) { return Conflict(); }
    }

    // Drafts are deliberately unavailable: channel verification alone does not
    // establish commercial rights, fit, pricing or capacity for a new slot.
    [Authorize]
    [HttpPost("inventory/draft")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> CreateOwnInventoryDraft([FromBody] CreatorInventoryDraftRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorInventoryReadbackRequested || !auth.CreatorInventoryDraftRequested
            || !auth.CreatorAuthorityVerificationRequested || !BlissAuthorization.IsPendingCreatorRegistration(User, auth))
            return StatusCode(StatusCodes.Status403Forbidden);
        if (request.RequestId == Guid.Empty || request.ContentItemId == Guid.Empty || !request.ConfirmDraft
            || request.StartSecond is < 0 or > 86400 || request.DurationSeconds is < 1 or > 3600
            || request.SlotType is not ("PRE_ROLL" or "MID_ROLL" or "POST_ROLL" or "LOWER_THIRD"
                or "PERIMETER_OVERLAY" or "CORNER_OVERLAY" or "ROTATING_OVERLAY" or "SPONSORED_SEGMENT"))
            return BadRequest();
        var subject = User.FindFirst("sub")!.Value;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var creatorId = await (from m in db.CreatorAccountMemberships.AsNoTracking()
                join r in db.CreatorRegistrationReceipts.AsNoTracking()
                    on new { m.Subject, m.CreatorId } equals new { r.Subject, r.CreatorId }
                where m.Subject == subject && m.Status == "ACTIVE" && m.Creator.AuthorityStatus == "VERIFIED"
                    && r.ProcessingConsent && r.TermsAccepted && r.PrivacyNoticeVersion == PrivacyNoticeVersion
                    && r.TermsVersion == TermsVersion
                select (Guid?)m.CreatorId).SingleOrDefaultAsync(ct);
            if (creatorId is null) return StatusCode(StatusCodes.Status403Forbidden);
            if (!await db.ContentItems.AsNoTracking().AnyAsync(c => c.Id == request.ContentItemId
                && c.CreatorId == creatorId.Value && c.Creator.AuthorityStatus == "VERIFIED", ct)) return NotFound();
            var existing = await db.AdInventorySlots.AsNoTracking().SingleOrDefaultAsync(s => s.Id == request.RequestId, ct);
            if (existing is not null)
            {
                if (existing.ContentItemId != request.ContentItemId || existing.SlotType != request.SlotType
                    || existing.StartSecond != request.StartSecond || existing.DurationSeconds != request.DurationSeconds
                    || existing.IsAvailable) return Conflict();
            }
            else
            {
                if (await db.AdInventorySlots.CountAsync(s => s.ContentItem.CreatorId == creatorId.Value, ct) >= 50)
                    return Conflict(new { error = "The dashboard inventory limit has been reached." });
                if (await db.AdInventorySlots.AnyAsync(s => s.ContentItemId == request.ContentItemId
                    && s.SlotType == request.SlotType && s.StartSecond == request.StartSecond
                    && s.DurationSeconds == request.DurationSeconds, ct)) return Conflict(new { error = "An identical slot already exists." });
                db.AdInventorySlots.Add(new AdInventorySlot { Id = request.RequestId, ContentItemId = request.ContentItemId,
                    SlotType = request.SlotType, StartSecond = request.StartSecond, DurationSeconds = request.DurationSeconds,
                    IsAvailable = false });
                await db.SaveChangesAsync(ct);
                // Check actual persisted availability rather than trusting an EF default.
                if (!await db.AdInventorySlots.AsNoTracking().AnyAsync(s => s.Id == request.RequestId && !s.IsAvailable, ct))
                    return Conflict();
            }
            await tx.CommitAsync(ct);
            return StatusCode(existing is null ? 201 : 200, new { slotId = request.RequestId,
                contentItemId = request.ContentItemId, isAvailable = false, reviewRequired = true,
                bookingAuthorized = false, paymentAuthorized = false, deliveryAuthorized = false });
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure) { return Conflict(); }
        catch (DbUpdateException) { return Conflict(); }
    }

    private Task<Guid?> CurrentInvitationCreator(string subject, CancellationToken ct) =>
        (from m in db.CreatorAccountMemberships.AsNoTracking()
            join r in db.CreatorRegistrationReceipts.AsNoTracking() on new { m.Subject,m.CreatorId } equals new { r.Subject,r.CreatorId }
            where m.Subject == subject && m.Status == "ACTIVE" && m.Creator.AuthorityStatus == "VERIFIED"
                && r.ProcessingConsent && r.TermsAccepted && r.PrivacyNoticeVersion == PrivacyNoticeVersion && r.TermsVersion == TermsVersion
            select (Guid?)m.CreatorId).SingleOrDefaultAsync(ct);

    [Authorize]
    [HttpGet("campaign-invitations")]
    public async Task<IActionResult> MyInvitations(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorCampaignInvitationRequested || !auth.CreatorAuthorityVerificationRequested
            || !BlissAuthorization.IsPendingCreatorRegistration(User,auth)) return StatusCode(403);
        var subject = User.FindFirst("sub")!.Value;
        var creator = await CurrentInvitationCreator(subject,ct);
        if (creator is null) return StatusCode(403);
        var key = creator.Value.ToString("N");
        var audits = await db.WeddingPlannerAuditEvents.AsNoTracking()
            .Where(a => a.Action == CreatorCampaignInvitation.Action && a.ActorType == "OPERATOR" && a.Outcome == "INVITED" && a.RequestId == key)
            .OrderByDescending(a => a.OccurredAt).ThenBy(a => a.Id).Take(25).ToArrayAsync(ct);
        var items = new List<object>();
        foreach (var audit in audits)
        {
            if (audit.Detail is null || !Guid.TryParseExact(audit.Detail.Split(':')[0],"N",out var placementId)) continue;
            var row = await CreatorCampaignInvitation.Current(db,placementId,ct);
            if (row is null || row.CreatorId != creator.Value || !CreatorCampaignInvitation.Matches(audit,row)) continue;
            var responseId = CreatorCampaignInvitation.ResponseId(audit.Id);
            var response = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a => a.Id == responseId,ct);
            if (response is not null && (response.Action != CreatorCampaignInvitation.ResponseAction || response.ActorType != "CREATOR"
                || response.ActorLabel != subject || response.AdvertiserId != row.AdvertiserId || response.WorkspaceId != row.WorkspaceId
                || response.Detail != audit.Id.ToString("N") || response.Outcome is not ("INTERESTED" or "DECLINED"))) continue;
            items.Add(new { invitationId = audit.Id, row.PlacementId, row.CampaignName, row.ContentTitle, row.SlotType,
                row.StartSecond,row.DurationSeconds, digest = CreatorCampaignInvitation.Digest(row),
                response = response?.Outcome ?? "AWAITING_RESPONSE" });
        }
        return Ok(new { items, bookingAuthorized = false, paymentAuthorized = false, deliveryAuthorized = false });
    }

    [Authorize]
    [HttpPost("campaign-invitations")]
    [EnableRateLimiting(BlissRateLimitPolicies.Writes)]
    [RequestSizeLimit(1024)]
    public async Task<IActionResult> RespondToInvitation([FromBody] CreatorInvitationResponse request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!auth.CreatorCampaignInvitationRequested || !auth.CreatorAuthorityVerificationRequested
            || !BlissAuthorization.IsPendingCreatorRegistration(User,auth)) return StatusCode(403);
        if (request.InvitationId == Guid.Empty || request.PlacementId == Guid.Empty || request.Digest is not { Length: 64 }
            || request.Decision is not ("INTERESTED" or "DECLINED") || !request.ConfirmNonBinding) return BadRequest();
        var subject = User.FindFirst("sub")!.Value;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            var creator = await CurrentInvitationCreator(subject,ct);
            if (creator is null) return StatusCode(403);
            var row = await CreatorCampaignInvitation.Current(db,request.PlacementId,ct);
            if (row is null || row.CreatorId != creator.Value) return NotFound();
            var audit = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a => a.Id == request.InvitationId,ct);
            if (audit is null || !CreatorCampaignInvitation.Matches(audit,row) || audit.ActorLabel == subject
                || CreatorCampaignInvitation.Digest(row) != request.Digest) return Conflict();
            var id = CreatorCampaignInvitation.ResponseId(audit.Id);
            var prior = await db.WeddingPlannerAuditEvents.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id,ct);
            if (prior is not null && (prior.Action != CreatorCampaignInvitation.ResponseAction || prior.ActorType != "CREATOR"
                || prior.ActorLabel != subject || prior.AdvertiserId != row.AdvertiserId || prior.WorkspaceId != row.WorkspaceId
                || prior.Detail != audit.Id.ToString("N") || prior.Outcome != request.Decision)) return Conflict();
            if (prior is null)
            {
                db.WeddingPlannerAuditEvents.Add(new WeddingPlannerAuditEvent { Id = id, AdvertiserId = row.AdvertiserId,
                    WorkspaceId = row.WorkspaceId, Action = CreatorCampaignInvitation.ResponseAction, ActorType = "CREATOR",
                    ActorLabel = subject, Outcome = request.Decision, Detail = audit.Id.ToString("N"), OccurredAt = DateTime.UtcNow });
                await db.SaveChangesAsync(ct);
            }
            await tx.CommitAsync(ct);
            return Ok(new { invitationId = audit.Id, response = request.Decision, bookingAuthorized = false, paymentAuthorized = false, deliveryAuthorized = false });
        }
        catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.SerializationFailure) { return Conflict(); }
        catch (DbUpdateException) { return Conflict(); }
    }

    private IActionResult ReplayOrConflict(CreatorRegistrationReceipt prior, string subject, string digest) =>
        prior.Subject == subject && prior.RequestSha256 == digest
            ? Ok(new CreatorRegistrationReceiptDto(prior.Id, prior.CreatorId, "PENDING_REVIEW", prior.CreatedAt, prior.MarketingOptIn))
            : Conflict(new { error = "Registration key was already used with different or unauthorized content." });
}

public sealed record CreatorRegistrationRequest(
    Guid RequestKey,
    string DisplayName,
    string CountryCode,
    string ChannelUrl,
    string? PrimaryLanguage,
    bool ProcessingConsent,
    bool TermsAccepted,
    bool MarketingOptIn,
    string PrivacyNoticeVersion,
    string TermsVersion);

public sealed record CreatorRegistrationReceiptDto(
    Guid ReceiptId, Guid CreatorId, string Status, DateTime CreatedAt, bool MarketingOptIn);

public sealed record CreatorRegistrationStatusDto(
    Guid CreatorId, Guid ReceiptId, string Status, DateTime CreatedAt,
    string? ChannelProofCode = null, string? SubmittedChannelUrl = null, DateTime? ProofExpiresAt = null,
    CreatorSelfSummaryDto? VerifiedProfile = null);

public sealed record CreatorInventoryPauseRequest(Guid SlotId, bool ConfirmPause);

public sealed record CreatorInventoryDraftRequest(Guid RequestId, Guid ContentItemId, string SlotType, int StartSecond, int DurationSeconds, bool ConfirmDraft);

public sealed record CreatorInvitationResponse(Guid InvitationId, Guid PlacementId, string Digest, string Decision, bool ConfirmNonBinding);
