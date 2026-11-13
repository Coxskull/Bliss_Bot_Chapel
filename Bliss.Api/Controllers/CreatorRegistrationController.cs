using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Security;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Controllers;

[ApiController]
[Route("api/creators")]
public sealed class CreatorRegistrationController(BlissDbContext db, BlissAuthenticationOptions auth) : ControllerBase
{
    public const string PrivacyNoticeVersion = "creator-privacy-2026-11-07-v1";
    public const string TermsVersion = "creator-terms-2026-11-07-v1";

    [Authorize]
    [HttpPost("register")]
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
        catch (DbUpdateException)
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
        var membership = await db.CreatorAccountMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Subject == subject, cancellationToken);
        if (membership is null) return NotFound();
        var receipt = await db.CreatorRegistrationReceipts.AsNoTracking()
            .Where(x => x.Subject == subject && x.CreatorId == membership.CreatorId)
            .Select(x => new { x.Id, x.CreatedAt })
            .SingleOrDefaultAsync(cancellationToken);
        return receipt is null ? NotFound() : Ok(new CreatorRegistrationStatusDto(
            membership.CreatorId, receipt.Id, membership.Status, receipt.CreatedAt));
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
    Guid CreatorId, Guid ReceiptId, string Status, DateTime CreatedAt);
