using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record QueueEconomicsResearchCommand(
    Guid GeographicMarketId,
    string Metric,
    string? IndustryCategory,
    string? Platform,
    string? InventorySlotType,
    string ResearchQuestion,
    string RequestedBy,
    string SourceSystem,
    string IdempotencyKey);

public sealed record StageEconomicsResearchCandidateCommand(
    Guid EconomicsResearchRunId,
    decimal? NumericValue,
    decimal? RangeLow,
    decimal? RangeHigh,
    string? CurrencyCode,
    string SourceName,
    string SourceUrl,
    string SourceType,
    DateOnly? PublicationDate,
    DateTime RetrievedAt,
    string ConfidenceLevel,
    string VerificationStatus,
    string ExtractionModel,
    string RawPayloadJson,
    string SourceSystem,
    string IdempotencyKey);

public sealed record ReviewEconomicsResearchCandidateCommand(
    Guid EconomicsResearchCandidateId,
    string Decision,
    string ReviewerLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey);

public sealed record EconomicsResearchRunResult(
    EconomicsResearchRun Run,
    Guid ActionId,
    bool IsReplay);

/// <summary>
/// Validates staged research and gates promotion into append-only observations.
/// n8n and AI are callers, never durable truth or pricing authority.
/// </summary>
public sealed class EconomicsResearchService
{
    private readonly BlissDbContext _db;

    public EconomicsResearchService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<EconomicsResearchRunResult> QueueAsync(
        QueueEconomicsResearchCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64)
            .ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await _db.EconomicsResearchRuns.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            return new(await LoadRunAsync(replay.Id, cancellationToken), replay.Id, true);
        }
        if (!await _db.GeographicMarkets.AsNoTracking()
            .AnyAsync(x => x.Id == command.GeographicMarketId, cancellationToken))
        {
            throw new InvalidOperationException(
                "GeographicMarketId does not reference a market.");
        }

        var now = DateTime.UtcNow;
        var run = new EconomicsResearchRun
        {
            Id = Guid.NewGuid(),
            GeographicMarketId = command.GeographicMarketId,
            Metric = Required(command.Metric, nameof(command.Metric), 64).ToUpperInvariant(),
            IndustryCategory = Optional(command.IndustryCategory, 128),
            Platform = Optional(command.Platform, 64),
            InventorySlotType = Optional(command.InventorySlotType, 64),
            ResearchQuestion = Required(
                command.ResearchQuestion, nameof(command.ResearchQuestion), 2000),
            RequestedBy = Required(command.RequestedBy, nameof(command.RequestedBy), 128),
            Status = EconomicsResearchRunStatuses.Queued,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.EconomicsResearchRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await LoadRunAsync(run.Id, cancellationToken), run.Id, false);
    }

    public async Task<EconomicsResearchRunResult> StageCandidateAsync(
        StageEconomicsResearchCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64)
            .ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await _db.EconomicsResearchCandidates.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            return new(
                await LoadRunAsync(replay.EconomicsResearchRunId, cancellationToken),
                replay.Id,
                true);
        }

        var run = await _db.EconomicsResearchRuns
            .SingleOrDefaultAsync(
                x => x.Id == command.EconomicsResearchRunId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "EconomicsResearchRunId does not reference a research run.");
        if (run.Status is not EconomicsResearchRunStatuses.Queued
            and not EconomicsResearchRunStatuses.AwaitingReview)
        {
            throw new InvalidOperationException(
                "Only QUEUED or AWAITING_REVIEW runs can accept candidates.");
        }

        ValidateValueShape(command.NumericValue, command.RangeLow, command.RangeHigh);
        var url = ValidateHttpsUrl(command.SourceUrl);
        var confidence = Required(
            command.ConfidenceLevel, nameof(command.ConfidenceLevel), 32).ToUpperInvariant();
        if (confidence is not EconomicsConfidenceLevels.Low
            and not EconomicsConfidenceLevels.Medium
            and not EconomicsConfidenceLevels.Unknown)
        {
            throw new InvalidOperationException(
                "AI research candidate confidence must be LOW, MEDIUM, or UNKNOWN.");
        }
        var verification = Required(
            command.VerificationStatus, nameof(command.VerificationStatus), 32)
            .ToUpperInvariant();
        if (verification is not ObservationVerificationStatuses.Estimated
            and not ObservationVerificationStatuses.Inferred
            and not ObservationVerificationStatuses.Unknown)
        {
            throw new InvalidOperationException(
                "AI research candidates cannot be VERIFIED.");
        }
        ValidateDates(command.PublicationDate, command.RetrievedAt);
        var rawPayload = ValidateJson(command.RawPayloadJson);

        var now = DateTime.UtcNow;
        var candidate = new EconomicsResearchCandidate
        {
            Id = Guid.NewGuid(),
            EconomicsResearchRunId = run.Id,
            GeographicMarketId = run.GeographicMarketId,
            IndustryCategory = run.IndustryCategory,
            Platform = run.Platform,
            InventorySlotType = run.InventorySlotType,
            Metric = run.Metric,
            NumericValue = command.NumericValue,
            RangeLow = command.RangeLow,
            RangeHigh = command.RangeHigh,
            CurrencyCode = Optional(command.CurrencyCode, 8)?.ToUpperInvariant(),
            SourceName = Required(command.SourceName, nameof(command.SourceName), 256),
            SourceUrl = url,
            SourceType = Required(command.SourceType, nameof(command.SourceType), 64)
                .ToUpperInvariant(),
            PublicationDate = command.PublicationDate,
            RetrievedAt = command.RetrievedAt.ToUniversalTime(),
            ConfidenceLevel = confidence,
            VerificationStatus = verification,
            ExtractionModel = Required(
                command.ExtractionModel, nameof(command.ExtractionModel), 128),
            RawPayloadJson = rawPayload,
            Status = EconomicsResearchCandidateStatuses.Staged,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now,
            UpdatedAt = now
        };
        run.Status = EconomicsResearchRunStatuses.AwaitingReview;
        run.UpdatedAt = now;
        _db.EconomicsResearchCandidates.Add(candidate);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await LoadRunAsync(run.Id, cancellationToken), candidate.Id, false);
    }

    public async Task<EconomicsResearchRunResult> ReviewCandidateAsync(
        ReviewEconomicsResearchCandidateCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64)
            .ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await _db.EconomicsResearchReviewDecisions.AsNoTracking()
            .Include(x => x.EconomicsResearchCandidate)
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            return new(
                await LoadRunAsync(
                    replay.EconomicsResearchCandidate.EconomicsResearchRunId,
                    cancellationToken),
                replay.Id,
                true);
        }

        var candidate = await _db.EconomicsResearchCandidates
            .Include(x => x.EconomicsResearchRun)
            .SingleOrDefaultAsync(
                x => x.Id == command.EconomicsResearchCandidateId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "EconomicsResearchCandidateId does not reference a candidate.");
        if (candidate.Status != EconomicsResearchCandidateStatuses.Staged)
        {
            throw new InvalidOperationException("Only STAGED candidates can be reviewed.");
        }
        var decisionCode = Required(command.Decision, nameof(command.Decision), 32)
            .ToUpperInvariant();
        if (decisionCode is not EconomicsResearchReviewDecisions.Accept
            and not EconomicsResearchReviewDecisions.Reject)
        {
            throw new InvalidOperationException("Decision must be ACCEPT or REJECT.");
        }

        MarketBenchmarkObservation? observation = null;
        if (decisionCode == EconomicsResearchReviewDecisions.Accept)
        {
            var researchSource = await _db.ResearchSources
                .SingleOrDefaultAsync(
                    x => x.SourceUrl == candidate.SourceUrl,
                    cancellationToken);
            if (researchSource is null)
            {
                researchSource = new ResearchSource
                {
                    Id = Guid.NewGuid(),
                    Name = candidate.SourceName,
                    SourceUrl = candidate.SourceUrl,
                    SourceType = candidate.SourceType,
                    IsApproved = false,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ResearchSources.Add(researchSource);
            }
            observation = new MarketBenchmarkObservation
            {
                Id = Guid.NewGuid(),
                ResearchSourceId = researchSource.Id,
                GeographicMarketId = candidate.GeographicMarketId,
                IndustryCategory = candidate.IndustryCategory,
                Platform = candidate.Platform,
                InventorySlotType = candidate.InventorySlotType,
                Metric = candidate.Metric,
                NumericValue = candidate.NumericValue,
                RangeLow = candidate.RangeLow,
                RangeHigh = candidate.RangeHigh,
                CurrencyCode = candidate.CurrencyCode,
                PublicationDate = candidate.PublicationDate,
                RetrievedAt = candidate.RetrievedAt,
                ConfidenceLevel = candidate.ConfidenceLevel,
                VerificationStatus = candidate.VerificationStatus,
                Notes = Truncate(
                    $"Promoted from research run {candidate.EconomicsResearchRunId}; "
                    + $"candidate {candidate.Id}; extraction model {candidate.ExtractionModel}; "
                    + $"human rationale: {Required(command.Rationale, nameof(command.Rationale), 2000)}",
                    4000),
                CreatedAt = DateTime.UtcNow
            };
            _db.MarketBenchmarkObservations.Add(observation);
            candidate.Status = EconomicsResearchCandidateStatuses.Promoted;
            candidate.PromotedObservationId = observation.Id;
        }
        else
        {
            candidate.Status = EconomicsResearchCandidateStatuses.Rejected;
        }

        var now = DateTime.UtcNow;
        candidate.UpdatedAt = now;
        var decision = new EconomicsResearchReviewDecision
        {
            Id = Guid.NewGuid(),
            EconomicsResearchCandidateId = candidate.Id,
            MarketBenchmarkObservationId = observation?.Id,
            Decision = decisionCode,
            ReviewerLabel = Required(
                command.ReviewerLabel, nameof(command.ReviewerLabel), 128),
            Rationale = Required(command.Rationale, nameof(command.Rationale), 2000),
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now
        };
        var hasOtherStaged = await _db.EconomicsResearchCandidates.AsNoTracking()
            .AnyAsync(
                x => x.EconomicsResearchRunId == candidate.EconomicsResearchRunId
                    && x.Id != candidate.Id
                    && x.Status == EconomicsResearchCandidateStatuses.Staged,
                cancellationToken);
        candidate.EconomicsResearchRun.Status = hasOtherStaged
            ? EconomicsResearchRunStatuses.AwaitingReview
            : EconomicsResearchRunStatuses.Completed;
        candidate.EconomicsResearchRun.UpdatedAt = now;
        _db.EconomicsResearchReviewDecisions.Add(decision);
        await _db.SaveChangesAsync(cancellationToken);
        return new(
            await LoadRunAsync(candidate.EconomicsResearchRunId, cancellationToken),
            decision.Id,
            false);
    }

    public IQueryable<EconomicsResearchRun> ResearchGraph() =>
        _db.EconomicsResearchRuns
            .Include(x => x.GeographicMarket)
            .Include(x => x.Candidates).ThenInclude(x => x.ReviewDecisions)
            .Include(x => x.Candidates).ThenInclude(x => x.PromotedObservation)
            .AsSplitQuery();

    private async Task<EconomicsResearchRun> LoadRunAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await ResearchGraph().SingleAsync(x => x.Id == id, cancellationToken);

    private static void ValidateValueShape(
        decimal? numeric,
        decimal? low,
        decimal? high)
    {
        var hasNumeric = numeric.HasValue;
        var hasAnyRange = low.HasValue || high.HasValue;
        if (hasNumeric == hasAnyRange)
        {
            throw new InvalidOperationException(
                "Provide either NumericValue or a complete range, but not both.");
        }
        if (hasAnyRange && (!low.HasValue || !high.HasValue || low > high))
        {
            throw new InvalidOperationException(
                "RangeLow and RangeHigh are both required and low cannot exceed high.");
        }
    }

    private static string ValidateHttpsUrl(string value)
    {
        var result = Required(value, nameof(value), 2048);
        if (!Uri.TryCreate(result, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException(
                "SourceUrl must be an absolute HTTPS URL.");
        }
        return uri.AbsoluteUri;
    }

    private static void ValidateDates(DateOnly? publicationDate, DateTime retrievedAt)
    {
        if (retrievedAt == default || retrievedAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            throw new InvalidOperationException(
                "RetrievedAt is required and cannot be in the future.");
        }
        if (publicationDate.HasValue
            && publicationDate > DateOnly.FromDateTime(retrievedAt.ToUniversalTime()))
        {
            throw new InvalidOperationException(
                "PublicationDate cannot be later than RetrievedAt.");
        }
    }

    private static string ValidateJson(string value)
    {
        var result = Required(value, nameof(value), 65_536);
        try
        {
            using var _ = JsonDocument.Parse(result);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("RawPayloadJson must be valid JSON.");
        }
        return result;
    }

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
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException(
                $"Value cannot exceed {maxLength} characters.");
        }
        return trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
