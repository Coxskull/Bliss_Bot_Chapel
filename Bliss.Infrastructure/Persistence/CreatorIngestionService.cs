using System.Text.Json;
using System.Text.Json.Serialization;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record CreatorIngestionCommand(
    string SourceSystem,
    string IdempotencyKey,
    string Platform,
    string ExternalProfileId,
    string CreatorName,
    string? ProfileUrl = null,
    string? CountryCode = null,
    string? PrimaryLanguage = null,
    int? AudienceSize = null,
    int? Followers = null,
    decimal? FemalePercentage = null,
    decimal? MalePercentage = null,
    string? PrimaryAgeRange = null,
    string? PrimaryGeography = null,
    string? EngagementLevel = null,
    string? SourceUrl = null,
    string? ConfidenceLevel = null,
    DateTime? CollectedAt = null);

public sealed record CreatorIngestionResult(
    Guid RunId,
    Guid CreatorId,
    Guid CreatorPlatformId,
    string IdentityKey,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string Outcome,
    DateTime CompletedAt,
    bool IsReplay);

/// <summary>
/// Provider-neutral deterministic creator upsert. It makes no external calls and stores no credentials.
/// </summary>
public sealed class CreatorIngestionService
{
    private const string SourceType = "PROVIDER_OBSERVATION";
    private readonly BlissDbContext _db;

    public CreatorIngestionService(BlissDbContext db)
    {
        _db = db;
    }

    public static string BuildIdentityKey(string platform, string externalProfileId)
    {
        var normalizedPlatform = Required(platform, nameof(platform), 64).ToUpperInvariant();
        var normalizedExternalId = Required(externalProfileId, nameof(externalProfileId), 256);
        return $"{normalizedPlatform}::{normalizedExternalId}";
    }

    public async Task<CreatorIngestionResult> IngestAsync(
        CreatorIngestionCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAndValidate(command);

        var replay = await _db.CreatorIngestionRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == normalized.SourceSystem
                    && x.IdempotencyKey == normalized.IdempotencyKey,
                cancellationToken);

        if (replay is not null)
        {
            return ToResult(replay, true);
        }

        var startedAt = DateTime.UtcNow;
        var collectedAt = normalized.CollectedAt ?? startedAt;
        var identityKey = BuildIdentityKey(normalized.Platform, normalized.ExternalProfileId);
        var platform = await _db.CreatorPlatforms
            .Include(x => x.Creator)
            .SingleOrDefaultAsync(x => x.IdentityKey == identityKey, cancellationToken);

        var isNew = platform is null;
        var changed = false;
        Creator creator;

        if (isNew)
        {
            creator = new Creator
            {
                Id = Guid.NewGuid(),
                Name = normalized.CreatorName,
                CountryCode = normalized.CountryCode,
                PrimaryLanguage = normalized.PrimaryLanguage,
                AudienceSize = normalized.AudienceSize,
                FemalePercentage = normalized.FemalePercentage,
                MalePercentage = normalized.MalePercentage,
                PrimaryAgeRange = normalized.PrimaryAgeRange,
                PrimaryGeography = normalized.PrimaryGeography,
                EngagementLevel = normalized.EngagementLevel,
                CreatedAt = collectedAt,
                UpdatedAt = collectedAt
            };
            platform = new CreatorPlatform
            {
                Id = Guid.NewGuid(),
                CreatorId = creator.Id,
                Platform = normalized.Platform,
                ExternalProfileId = normalized.ExternalProfileId,
                IdentityKey = identityKey,
                ProfileUrl = normalized.ProfileUrl,
                Followers = normalized.Followers,
                LastCollectedAt = collectedAt
            };
            _db.Creators.Add(creator);
            _db.CreatorPlatforms.Add(platform);
        }
        else
        {
            creator = platform!.Creator;
            changed |= Assign(creator.Name, normalized.CreatorName, value => creator.Name = value);
            changed |= AssignIfPresent(creator.CountryCode, normalized.CountryCode, value => creator.CountryCode = value);
            changed |= AssignIfPresent(creator.PrimaryLanguage, normalized.PrimaryLanguage, value => creator.PrimaryLanguage = value);
            changed |= AssignIfPresent(creator.AudienceSize, normalized.AudienceSize, value => creator.AudienceSize = value);
            changed |= AssignIfPresent(creator.FemalePercentage, normalized.FemalePercentage, value => creator.FemalePercentage = value);
            changed |= AssignIfPresent(creator.MalePercentage, normalized.MalePercentage, value => creator.MalePercentage = value);
            changed |= AssignIfPresent(creator.PrimaryAgeRange, normalized.PrimaryAgeRange, value => creator.PrimaryAgeRange = value);
            changed |= AssignIfPresent(creator.PrimaryGeography, normalized.PrimaryGeography, value => creator.PrimaryGeography = value);
            changed |= AssignIfPresent(creator.EngagementLevel, normalized.EngagementLevel, value => creator.EngagementLevel = value);
            changed |= AssignIfPresent(platform.ProfileUrl, normalized.ProfileUrl, value => platform.ProfileUrl = value);
            changed |= AssignIfPresent(platform.Followers, normalized.Followers, value => platform.Followers = value);
            platform.LastCollectedAt = collectedAt;
            creator.UpdatedAt = collectedAt;
        }

        var run = new CreatorIngestionRun
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            CreatorPlatformId = platform.Id,
            SourceSystem = normalized.SourceSystem,
            IdempotencyKey = normalized.IdempotencyKey,
            IdentityKey = identityKey,
            Status = EntityStatuses.Completed,
            Outcome = isNew ? "CREATED" : changed ? "UPDATED" : "OBSERVED",
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            InputSnapshot = JsonSerializer.Serialize(normalized, SnapshotOptions)
        };
        _db.CreatorIngestionRuns.Add(run);
        _db.DataProvenances.AddRange(BuildProvenance(normalized, creator.Id, platform.Id, run.Id, collectedAt));
        await _db.SaveChangesAsync(cancellationToken);

        return ToResult(run, false);
    }

    private static CreatorIngestionCommand NormalizeAndValidate(CreatorIngestionCommand command)
    {
        var sourceSystem = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var idempotencyKey = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var platform = Required(command.Platform, nameof(command.Platform), 64).ToUpperInvariant();
        var externalProfileId = Required(command.ExternalProfileId, nameof(command.ExternalProfileId), 256);
        var creatorName = Required(command.CreatorName, nameof(command.CreatorName), 256);
        var countryCode = Optional(command.CountryCode, 8)?.ToUpperInvariant();
        var confidence = Optional(command.ConfidenceLevel, 64)?.ToUpperInvariant() ?? ConfidenceLevels.Unknown;

        if (command.AudienceSize < 0 || command.Followers < 0)
        {
            throw new InvalidOperationException("AudienceSize and Followers cannot be negative.");
        }

        ValidatePercentage(command.FemalePercentage, nameof(command.FemalePercentage));
        ValidatePercentage(command.MalePercentage, nameof(command.MalePercentage));
        ValidateHttpUrl(command.ProfileUrl, nameof(command.ProfileUrl));
        ValidateHttpUrl(command.SourceUrl, nameof(command.SourceUrl));

        if (command.CollectedAt is { } collectedAt && collectedAt > DateTime.UtcNow.AddMinutes(5))
        {
            throw new InvalidOperationException("CollectedAt cannot be in the future.");
        }

        return command with
        {
            SourceSystem = sourceSystem,
            IdempotencyKey = idempotencyKey,
            Platform = platform,
            ExternalProfileId = externalProfileId,
            CreatorName = creatorName,
            ProfileUrl = Optional(command.ProfileUrl, 1024),
            CountryCode = countryCode,
            PrimaryLanguage = Optional(command.PrimaryLanguage, 128),
            PrimaryAgeRange = Optional(command.PrimaryAgeRange, 64),
            PrimaryGeography = Optional(command.PrimaryGeography, 256),
            EngagementLevel = Optional(command.EngagementLevel, 64)?.ToUpperInvariant(),
            SourceUrl = Optional(command.SourceUrl, 1024),
            ConfidenceLevel = confidence,
            CollectedAt = command.CollectedAt?.ToUniversalTime()
        };
    }

    private static IEnumerable<DataProvenance> BuildProvenance(
        CreatorIngestionCommand command,
        Guid creatorId,
        Guid platformId,
        Guid runId,
        DateTime collectedAt)
    {
        var sourceUrl = command.SourceUrl ?? command.ProfileUrl;
        var notes = $"Creator ingestion run {runId}.";

        yield return Provenance(nameof(Creator), creatorId, nameof(Creator.Name));
        yield return Provenance(nameof(CreatorPlatform), platformId, nameof(CreatorPlatform.ExternalProfileId));
        yield return Provenance(nameof(CreatorPlatform), platformId, nameof(CreatorPlatform.IdentityKey));

        if (command.CountryCode is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.CountryCode));
        if (command.PrimaryLanguage is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.PrimaryLanguage));
        if (command.AudienceSize is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.AudienceSize));
        if (command.FemalePercentage is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.FemalePercentage));
        if (command.MalePercentage is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.MalePercentage));
        if (command.PrimaryAgeRange is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.PrimaryAgeRange));
        if (command.PrimaryGeography is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.PrimaryGeography));
        if (command.EngagementLevel is not null) yield return Provenance(nameof(Creator), creatorId, nameof(Creator.EngagementLevel));
        if (command.ProfileUrl is not null) yield return Provenance(nameof(CreatorPlatform), platformId, nameof(CreatorPlatform.ProfileUrl));
        if (command.Followers is not null) yield return Provenance(nameof(CreatorPlatform), platformId, nameof(CreatorPlatform.Followers));
        yield return Provenance(nameof(CreatorPlatform), platformId, nameof(CreatorPlatform.LastCollectedAt));

        DataProvenance Provenance(string entityType, Guid entityId, string fieldName) => new()
        {
            Id = Guid.NewGuid(),
            EntityType = entityType,
            EntityId = entityId,
            FieldName = fieldName,
            SourceType = SourceType,
            SourceName = command.SourceSystem,
            SourceUrl = sourceUrl,
            ConfidenceLevel = command.ConfidenceLevel ?? ConfidenceLevels.Unknown,
            CollectedAt = collectedAt,
            Notes = notes
        };
    }

    private static CreatorIngestionResult ToResult(
        CreatorIngestionRun run,
        bool isReplay) => new(
            run.Id,
            run.CreatorId,
            run.CreatorPlatformId,
            run.IdentityKey,
            run.SourceSystem,
            run.IdempotencyKey,
            run.Status,
            run.Outcome,
            run.CompletedAt,
            isReplay);

    private static string Required(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static string? Optional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"Value cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }

    private static void ValidatePercentage(decimal? value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new InvalidOperationException($"{name} must be between 0 and 100.");
        }
    }

    private static void ValidateHttpUrl(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{name} must be an absolute HTTP or HTTPS URL.");
        }
    }

    private static bool Assign<T>(T current, T next, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, next))
        {
            return false;
        }

        assign(next);
        return true;
    }

    private static bool AssignIfPresent<T>(T? current, T? next, Action<T> assign)
        where T : struct
    {
        if (!next.HasValue || EqualityComparer<T>.Default.Equals(current.GetValueOrDefault(), next.Value) && current.HasValue)
        {
            return false;
        }

        assign(next.Value);
        return true;
    }

    private static bool AssignIfPresent(string? current, string? next, Action<string> assign)
    {
        if (next is null || string.Equals(current, next, StringComparison.Ordinal))
        {
            return false;
        }

        assign(next);
        return true;
    }

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
