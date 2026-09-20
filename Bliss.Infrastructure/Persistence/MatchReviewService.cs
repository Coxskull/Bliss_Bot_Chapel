using System.Text.Json;
using Bliss.Domain.Common;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record MatchReviewCommand(
    string SourceSystem,
    string IdempotencyKey,
    Guid BlissMatchId,
    string ReviewerLabel,
    string Decision,
    string Rationale);

public sealed record MatchReviewResult(
    Guid DecisionId,
    Guid BlissMatchId,
    Guid CreatorId,
    Guid? MatchEvaluationRunId,
    string SourceSystem,
    string IdempotencyKey,
    string ReviewerLabel,
    string Decision,
    string ResultingMatchStatus,
    string Status,
    DateTime CompletedAt,
    bool IsReplay);

/// <summary>
/// Records an explicit operator decision on a REVIEW_REQUIRED match.
/// It does not score, discover, or execute campaigns.
/// </summary>
public sealed class MatchReviewService
{
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
    public const string Hold = "HOLD";

    private readonly BlissDbContext _db;

    public MatchReviewService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<MatchReviewResult> DecideAsync(
        MatchReviewCommand command,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(command);
        var replay = await _db.MatchReviewDecisions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == normalized.SourceSystem
                    && x.IdempotencyKey == normalized.IdempotencyKey,
                cancellationToken);
        if (replay is not null)
        {
            return ToResult(replay, true);
        }

        var match = await _db.BlissMatches
            .SingleOrDefaultAsync(x => x.Id == normalized.BlissMatchId, cancellationToken);
        if (match is null)
        {
            throw new InvalidOperationException("BlissMatchId does not reference an existing match.");
        }

        if (!string.Equals(match.Status, EntityStatuses.ReviewRequired, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Only REVIEW_REQUIRED matches can enter the human review queue.");
        }

        var latestEvaluationId = await _db.MatchEvaluationRuns
            .AsNoTracking()
            .Where(x => x.BlissMatchId == match.Id)
            .OrderByDescending(x => x.CompletedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (normalized.Decision is Approve or Reject && latestEvaluationId is null)
        {
            throw new InvalidOperationException(
                "APPROVE and REJECT require an existing evaluation run for the match.");
        }

        var resultingStatus = normalized.Decision switch
        {
            Approve => EntityStatuses.Approved,
            Reject => EntityStatuses.Ineligible,
            _ => EntityStatuses.ReviewRequired
        };

        var startedAt = DateTime.UtcNow;
        await using var transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        match.Status = resultingStatus;
        var decision = new MatchReviewDecision
        {
            Id = Guid.NewGuid(),
            BlissMatchId = match.Id,
            CreatorId = match.CreatorId,
            MatchEvaluationRunId = latestEvaluationId,
            SourceSystem = normalized.SourceSystem,
            IdempotencyKey = normalized.IdempotencyKey,
            ReviewerLabel = normalized.ReviewerLabel,
            Decision = normalized.Decision,
            ResultingMatchStatus = resultingStatus,
            Rationale = normalized.Rationale,
            Status = EntityStatuses.Completed,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            InputSnapshot = JsonSerializer.Serialize(normalized, SnapshotOptions)
        };
        _db.MatchReviewDecisions.Add(decision);
        await _db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return ToResult(decision, false);
    }

    private static MatchReviewCommand Normalize(MatchReviewCommand command)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var idempotencyKey = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var reviewer = Required(command.ReviewerLabel, nameof(command.ReviewerLabel), 128);
        var decision = Required(command.Decision, nameof(command.Decision), 32).ToUpperInvariant();
        var rationale = Required(command.Rationale, nameof(command.Rationale), 2000);
        if (command.BlissMatchId == Guid.Empty)
        {
            throw new InvalidOperationException("BlissMatchId is required.");
        }

        if (decision is not Approve and not Reject and not Hold)
        {
            throw new InvalidOperationException("Decision must be APPROVE, REJECT, or HOLD.");
        }

        return command with
        {
            SourceSystem = source,
            IdempotencyKey = idempotencyKey,
            ReviewerLabel = reviewer,
            Decision = decision,
            Rationale = rationale
        };
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

    private static MatchReviewResult ToResult(MatchReviewDecision decision, bool isReplay) => new(
        decision.Id,
        decision.BlissMatchId,
        decision.CreatorId,
        decision.MatchEvaluationRunId,
        decision.SourceSystem,
        decision.IdempotencyKey,
        decision.ReviewerLabel,
        decision.Decision,
        decision.ResultingMatchStatus,
        decision.Status,
        decision.CompletedAt,
        isReplay);

    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
