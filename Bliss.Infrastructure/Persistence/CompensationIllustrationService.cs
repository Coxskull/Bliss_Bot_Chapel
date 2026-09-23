using System.Text.Json;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record GenerateCompensationIllustrationCommand(
    Guid QuoteId,
    Guid QuoteVersionId,
    Guid CompensationRuleVersionId,
    string SourceSystem,
    string IdempotencyKey);

public sealed record CompensationIllustrationResult(
    CompensationIllustration Illustration,
    bool IsReplay);

/// <summary>
/// Produces immutable allocation illustrations. It does not create settlement,
/// payable, payout, transfer, invoice, or ledger records.
/// </summary>
public sealed class CompensationIllustrationService
{
    private static readonly HashSet<string> AllowedRoles =
    [
        CompensationParticipantRoles.Alpha,
        CompensationParticipantRoles.Creator,
        CompensationParticipantRoles.OtherAuthorized
    ];

    private readonly BlissDbContext _db;

    public CompensationIllustrationService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<CompensationIllustrationResult> GenerateAsync(
        GenerateCompensationIllustrationCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64)
            .ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await IllustrationGraph()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            return new(replay, true);
        }

        var quote = await _db.Quotes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == command.QuoteId, cancellationToken)
            ?? throw new InvalidOperationException("QuoteId does not reference a quote.");
        if (quote.Status != QuoteStatuses.Accepted)
        {
            throw new InvalidOperationException(
                "Compensation illustrations require an ACCEPTED quote.");
        }

        var version = await _db.QuoteVersions.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == command.QuoteVersionId && x.QuoteId == quote.Id,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "QuoteVersionId does not reference a version of the selected quote.");
        if (version.VersionNumber != quote.CurrentVersionNumber)
        {
            throw new InvalidOperationException(
                "Compensation illustrations require the accepted current quote version.");
        }

        var outcome = await _db.QuoteOutcomes.AsNoTracking()
            .Where(x => x.QuoteId == quote.Id
                && x.QuoteVersionId == version.Id
                && x.Response == QuoteOutcomeResponses.Accepted)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "The selected quote version has no ACCEPTED commercial outcome.");
        if (!outcome.Amount.HasValue || outcome.Amount <= 0)
        {
            throw new InvalidOperationException(
                "The accepted commercial outcome has no positive amount.");
        }
        if (!string.Equals(
            quote.CurrencyCode, outcome.CurrencyCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Quote and accepted outcome currencies do not match.");
        }

        var rule = await _db.CompensationRuleVersions.AsNoTracking()
            .Include(x => x.Allocations)
            .SingleOrDefaultAsync(
                x => x.Id == command.CompensationRuleVersionId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "CompensationRuleVersionId does not reference a policy.");
        if (!rule.IsActive || rule.EffectiveAt > DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                "The compensation policy is not active and effective.");
        }
        var allocations = rule.Allocations.OrderBy(x => x.SortOrder).ToList();
        ValidatePolicy(allocations);

        var gross = Money(outcome.Amount.Value);
        var illustration = new CompensationIllustration
        {
            Id = Guid.NewGuid(),
            QuoteId = quote.Id,
            QuoteVersionId = version.Id,
            QuoteOutcomeId = outcome.Id,
            CompensationRuleVersionId = rule.Id,
            GrossAmount = gross,
            CurrencyCode = outcome.CurrencyCode,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = DateTime.UtcNow,
            InputSnapshotJson = JsonSerializer.Serialize(new
            {
                quoteId = quote.Id,
                quoteVersionId = version.Id,
                quoteVersionNumber = version.VersionNumber,
                quoteOutcomeId = outcome.Id,
                acceptedAmount = gross,
                currencyCode = outcome.CurrencyCode,
                compensationRuleVersionId = rule.Id,
                compensationRuleVersion = rule.Version,
                allocationIds = allocations.Select(x => x.Id).ToArray()
            })
        };

        decimal allocated = 0m;
        for (var index = 0; index < allocations.Count; index++)
        {
            var allocation = allocations[index];
            var amount = index == allocations.Count - 1
                ? gross - allocated
                : Money(gross * allocation.Percentage / 100m);
            allocated += amount;
            illustration.Lines.Add(new CompensationIllustrationLine
            {
                Id = Guid.NewGuid(),
                CompensationIllustrationId = illustration.Id,
                CompensationRuleAllocationId = allocation.Id,
                ParticipantRole = allocation.ParticipantRole,
                ParticipantLabel = allocation.ParticipantLabel,
                Percentage = allocation.Percentage,
                Amount = amount,
                SortOrder = allocation.SortOrder
            });
        }

        _db.CompensationIllustrations.Add(illustration);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await IllustrationGraph().SingleAsync(
            x => x.Id == illustration.Id, cancellationToken), false);
    }

    public IQueryable<CompensationIllustration> IllustrationGraph() =>
        _db.CompensationIllustrations
            .Include(x => x.Quote)
            .Include(x => x.QuoteVersion)
            .Include(x => x.QuoteOutcome)
            .Include(x => x.CompensationRuleVersion)
            .Include(x => x.Lines)
            .AsSplitQuery();

    private static void ValidatePolicy(
        IReadOnlyCollection<CompensationRuleAllocation> allocations)
    {
        if (allocations.Count == 0)
        {
            throw new InvalidOperationException(
                "The compensation policy has no participant allocations.");
        }
        if (allocations.Any(x => !AllowedRoles.Contains(x.ParticipantRole)))
        {
            throw new InvalidOperationException(
                "The compensation policy contains an unknown participant role.");
        }
        if (allocations.Any(x => x.Percentage <= 0m))
        {
            throw new InvalidOperationException(
                "Every compensation percentage must be positive.");
        }
        if (allocations.Sum(x => x.Percentage) != 100m)
        {
            throw new InvalidOperationException(
                "Compensation policy percentages must total exactly 100%.");
        }
    }

    private static decimal Money(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
}
