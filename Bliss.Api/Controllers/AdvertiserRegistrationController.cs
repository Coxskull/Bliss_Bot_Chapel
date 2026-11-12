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
[Route("api/advertisers")]
public sealed class AdvertiserRegistrationController(BlissDbContext db, BlissAuthenticationOptions auth) : ControllerBase
{
    public const string PrivacyNoticeVersion = "advertiser-privacy-2026-11-07-v1";
    public const string TermsVersion = "advertiser-terms-2026-11-07-v1";

    [Authorize]
    [HttpPost("register")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<IActionResult> Register([FromBody] AdvertiserRegistrationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!BlissAuthorization.IsPendingAdvertiserRegistration(User, auth))
            return StatusCode(StatusCodes.Status403Forbidden);

        var subject = User.FindFirst("sub")!.Value;
        if (request.RequestKey == Guid.Empty || !request.ProcessingConsent || !request.TermsAccepted
            || request.PrivacyNoticeVersion != PrivacyNoticeVersion || request.TermsVersion != TermsVersion)
            return BadRequest(new { error = "Privacy notice, terms, and processing consent must be accepted explicitly." });

        var name = request.BusinessName?.Trim();
        var country = request.CountryCode?.Trim().ToUpperInvariant();
        var website = request.Website?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 256
            || string.IsNullOrWhiteSpace(country) || country.Length != 2 || country.Any(c => c is < 'A' or > 'Z')
            || (!string.IsNullOrEmpty(website) && (!Uri.TryCreate(website, UriKind.Absolute, out var parsed)
                || parsed.Scheme != Uri.UriSchemeHttps || website.Length > 1024)))
            return BadRequest(new { error = "Business name, two-letter country code, or HTTPS website is invalid." });

        var canonical = JsonSerializer.Serialize(new
        {
            businessName = name,
            countryCode = country,
            website,
            processingConsent = request.ProcessingConsent,
            termsAccepted = request.TermsAccepted,
            marketingOptIn = request.MarketingOptIn,
            privacyNoticeVersion = request.PrivacyNoticeVersion,
            termsVersion = request.TermsVersion
        });
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        var priorRequest = await db.AdvertiserRegistrationReceipts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, cancellationToken);
        if (priorRequest is not null)
            return ReplayOrConflict(priorRequest, subject, digest);

        if (await db.AdvertiserAccountMemberships.AnyAsync(x => x.Subject == subject, cancellationToken))
            return Conflict(new { error = "This identity already has an advertiser registration." });

        var advertiser = new Advertiser
        {
            Id = Guid.NewGuid(), Name = name, CountryCode = country,
            Website = string.IsNullOrEmpty(website) ? null : website, CreatedAt = DateTime.UtcNow
        };
        var membership = new AdvertiserAccountMembership
        {
            Id = Guid.NewGuid(), Subject = subject, AdvertiserId = advertiser.Id,
            Status = "PENDING_REVIEW", CreatedAt = DateTime.UtcNow
        };
        var receipt = new AdvertiserRegistrationReceipt
        {
            Id = Guid.NewGuid(), RequestKey = request.RequestKey, Subject = subject,
            AdvertiserId = advertiser.Id, RequestSha256 = digest,
            PrivacyNoticeVersion = PrivacyNoticeVersion, TermsVersion = TermsVersion,
            ProcessingConsent = true, TermsAccepted = true, MarketingOptIn = request.MarketingOptIn,
            CreatedAt = DateTime.UtcNow
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Advertisers.Add(advertiser);
        db.AdvertiserAccountMemberships.Add(membership);
        db.AdvertiserRegistrationReceipts.Add(receipt);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var concurrentReplay = await db.AdvertiserRegistrationReceipts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, cancellationToken);
            if (concurrentReplay is not null) return ReplayOrConflict(concurrentReplay, subject, digest);
            return Conflict(new { error = "This identity or business is already registered." });
        }

        return StatusCode(StatusCodes.Status201Created, new AdvertiserRegistrationReceiptDto(
            receipt.Id, advertiser.Id, "PENDING_REVIEW", receipt.CreatedAt, receipt.MarketingOptIn));
    }

    private IActionResult ReplayOrConflict(AdvertiserRegistrationReceipt prior, string subject, string digest) =>
        prior.Subject == subject && prior.RequestSha256 == digest
            ? Ok(new AdvertiserRegistrationReceiptDto(prior.Id, prior.AdvertiserId, "PENDING_REVIEW", prior.CreatedAt, prior.MarketingOptIn))
            : Conflict(new { error = "Registration key was already used with different or unauthorized content." });
}

public sealed record AdvertiserRegistrationRequest(
    Guid RequestKey,
    string BusinessName,
    string CountryCode,
    string? Website,
    bool ProcessingConsent,
    bool TermsAccepted,
    bool MarketingOptIn,
    string PrivacyNoticeVersion,
    string TermsVersion);

public sealed record AdvertiserRegistrationReceiptDto(
    Guid ReceiptId, Guid AdvertiserId, string Status, DateTime CreatedAt, bool MarketingOptIn);
