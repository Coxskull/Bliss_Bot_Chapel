using System.Text.Json;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Bliss.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record MatchFormationCommand(
    string SourceSystem,
    string IdempotencyKey,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid RuleVersionId,
    bool EvaluateOnCreate);

public sealed record MatchFormationResult(
    Guid RunId,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid AdvertiserOpportunityId,
    Guid RuleVersionId,
    string SourceSystem,
    string IdempotencyKey,
    string Status,
    string Outcome,
    string MatchStatus,
    bool EvaluateOnCreate,
    DateTime CompletedAt,
    bool IsReplay);

/// <summary>
/// Forms an explicitly requested match certificate. It does not discover,
/// rank, or select creators or opportunities.
/// </summary>
public sealed class MatchFormationService
{
    private readonly BlissDbContext _db;
    private readonly MatchRuleEvaluationService _evaluator;

    public MatchFormationService(BlissDbContext db, MatchRuleEvaluationService evaluator)
    {
        _db = db;
        _evaluator = evaluator;
    }

    public async Task<MatchFormationResult> FormAsync(
        MatchFormationCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(command);
        var replay = await _db.MatchFormationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == normalized.SourceSystem
                    && x.IdempotencyKey == normalized.IdempotencyKey,
                cancellationToken);

        if (replay is not null)
        {
            var replayStatus = await _db.BlissMatches
                .AsNoTracking()
                .Where(x => x.Id == replay.BlissMatchId)
                .Select(x => x.Status)
                .SingleAsync(cancellationToken);
            return ToResult(replay, replayStatus, true);
        }

        var creatorExists = await _db.Creators
            .AsNoTracking()
            .AnyAsync(x => x.Id == normalized.CreatorId, cancellationToken);
        if (!creatorExists)
        {
            throw new InvalidOperationException("CreatorId does not reference an existing creator.");
        }

        var opportunity = await _db.AdvertiserOpportunities
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == normalized.AdvertiserOpportunityId, cancellationToken);
        if (opportunity is null)
        {
            throw new InvalidOperationException("AdvertiserOpportunityId does not reference an existing opportunity.");
        }
        if (!string.Equals(opportunity.Status, EntityStatuses.Active, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("AdvertiserOpportunityId must reference an active opportunity.");
        }

        var ruleVersion = await _db.RuleVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == normalized.RuleVersionId, cancellationToken);
        if (ruleVersion is null)
        {
            throw new InvalidOperationException("RuleVersionId does not reference an existing rule version.");
        }
        if (!ruleVersion.IsActive)
        {
            throw new InvalidOperationException("RuleVersionId must reference an active rule version.");
        }
        if (normalized.EvaluateOnCreate)
        {
            if (string.IsNullOrWhiteSpace(ruleVersion.DocumentJson))
            {
                throw new InvalidOperationException(
                    "RuleVersionId must reference a rule version with a rule document when EvaluateOnCreate is true.");
            }
            _ = DeterministicRuleEvaluator.ParseDocument(ruleVersion.DocumentJson);
        }

        var startedAt = DateTime.UtcNow;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        var match = new BlissMatch
        {
            Id = Guid.NewGuid(),
            CreatorId = normalized.CreatorId,
            AdvertiserOpportunityId = normalized.AdvertiserOpportunityId,
            RuleVersionId = normalized.RuleVersionId,
            Status = EntityStatuses.Created,
            CreatedAt = startedAt
        };
        _db.BlissMatches.Add(match);

        if (normalized.EvaluateOnCreate)
        {
            await _db.SaveChangesAsync(cancellationToken);
            await _evaluator.EvaluateAsync(match.Id, evaluationRunId: null, cancellationToken);
        }

        var run = new MatchFormationRun
        {
            Id = Guid.NewGuid(),
            BlissMatchId = match.Id,
            CreatorId = normalized.CreatorId,
            AdvertiserOpportunityId = normalized.AdvertiserOpportunityId,
            RuleVersionId = normalized.RuleVersionId,
            SourceSystem = normalized.SourceSystem,
            IdempotencyKey = normalized.IdempotencyKey,
            Status = EntityStatuses.Completed,
            Outcome = EntityStatuses.Created,
            EvaluateOnCreate = normalized.EvaluateOnCreate,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            InputSnapshot = JsonSerializer.Serialize(normalized, SnapshotOptions)
        };
        _db.MatchFormationRuns.Add(run);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return ToResult(run, match.Status, false);
    }

    private static MatchFormationCommand Normalize(MatchFormationCommand command)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var idempotencyKey = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        if (command.CreatorId == Guid.Empty) throw new InvalidOperationException("CreatorId is required.");
        if (command.AdvertiserOpportunityId == Guid.Empty) throw new InvalidOperationException("AdvertiserOpportunityId is required.");
        if (command.RuleVersionId == Guid.Empty) throw new InvalidOperationException("RuleVersionId is required.");
        return command with { SourceSystem = source, IdempotencyKey = idempotencyKey };
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) throw new InvalidOperationException($"{name} is required.");
        if (trimmed.Length > maxLength) throw new InvalidOperationException($"{name} cannot exceed {maxLength} characters.");
        return trimmed;
    }

    private static MatchFormationResult ToResult(
        MatchFormationRun run,
        string matchStatus,
        bool isReplay) => new(
            run.Id,
            run.BlissMatchId,
            run.CreatorId,
            run.AdvertiserOpportunityId,
            run.RuleVersionId,
            run.SourceSystem,
            run.IdempotencyKey,
            run.Status,
            run.Outcome,
            matchStatus,
            run.EvaluateOnCreate,
            run.CompletedAt,
            isReplay);

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
