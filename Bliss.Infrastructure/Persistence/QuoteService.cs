using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Infrastructure.Persistence;

public sealed record QuoteLineCommand(
    Guid RateRecommendationId,
    string Description,
    decimal Quantity,
    decimal UnitAmount);

public sealed record CreateQuoteCommand(
    Guid? AdvertiserOpportunityId,
    string RequestedBy,
    string RevisionReason,
    IReadOnlyList<QuoteLineCommand> LineItems,
    string SourceSystem,
    string IdempotencyKey);

public sealed record ReviseQuoteCommand(
    Guid QuoteId,
    string CreatedBy,
    string RevisionReason,
    IReadOnlyList<QuoteLineCommand> LineItems,
    string SourceSystem,
    string IdempotencyKey);

public sealed record DecideQuoteApprovalCommand(
    Guid QuoteId,
    Guid QuoteVersionId,
    string Decision,
    string ReviewerLabel,
    string Rationale,
    string SourceSystem,
    string IdempotencyKey);

public sealed record RecordQuoteOutcomeCommand(
    Guid QuoteId,
    Guid QuoteVersionId,
    string Response,
    string ActorLabel,
    string Rationale,
    IReadOnlyList<QuoteLineCommand>? NegotiatedLineItems,
    string SourceSystem,
    string IdempotencyKey);

public sealed record QuoteMutationResult(
    Quote Quote,
    Guid ActionId,
    Guid? NewQuoteVersionId,
    bool IsReplay);

/// <summary>
/// Owns the explicit quote lifecycle. It never calculates compensation,
/// reserves inventory, or changes the linked recommendation.
/// </summary>
public sealed class QuoteService
{
    private readonly BlissDbContext _db;

    public QuoteService(BlissDbContext db)
    {
        _db = db;
    }

    public async Task<QuoteMutationResult> CreateAsync(
        CreateQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await _db.Quotes.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            var loaded = await LoadAsync(replay.Id, cancellationToken);
            return new(loaded, replay.Id, CurrentVersion(loaded).Id, true);
        }

        if (command.AdvertiserOpportunityId.HasValue
            && !await _db.AdvertiserOpportunities.AsNoTracking().AnyAsync(
                x => x.Id == command.AdvertiserOpportunityId.Value, cancellationToken))
        {
            throw new InvalidOperationException(
                "AdvertiserOpportunityId does not reference an opportunity.");
        }

        var prepared = await PrepareLinesAsync(command.LineItems, cancellationToken);
        var now = DateTime.UtcNow;
        var quote = new Quote
        {
            Id = Guid.NewGuid(),
            AdvertiserOpportunityId = command.AdvertiserOpportunityId,
            CurrencyCode = prepared.CurrencyCode,
            Status = QuoteStatuses.Draft,
            CurrentVersionNumber = 1,
            RequestedBy = Required(command.RequestedBy, nameof(command.RequestedBy), 128),
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now,
            UpdatedAt = now
        };
        var version = BuildVersion(
            quote.Id,
            null,
            1,
            prepared,
            Required(command.RevisionReason, nameof(command.RevisionReason), 1000),
            quote.RequestedBy,
            source,
            key,
            now);
        quote.Versions.Add(version);

        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await LoadAsync(quote.Id, cancellationToken), quote.Id, version.Id, false);
    }

    public async Task<QuoteMutationResult> ReviseAsync(
        ReviseQuoteCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await _db.QuoteVersions.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            return new(await LoadAsync(replay.QuoteId, cancellationToken),
                replay.Id, replay.Id, true);
        }

        var quote = await CurrentQuoteAsync(command.QuoteId, cancellationToken);
        if (quote.Status is QuoteStatuses.Accepted or QuoteStatuses.Declined)
        {
            throw new InvalidOperationException("Terminal quotes cannot be revised.");
        }

        var prepared = await PrepareLinesAsync(command.LineItems, cancellationToken);
        EnsureCurrency(quote.CurrencyCode, prepared.CurrencyCode);
        var current = CurrentVersion(quote);
        var version = BuildVersion(
            quote.Id,
            current.Id,
            quote.CurrentVersionNumber + 1,
            prepared,
            Required(command.RevisionReason, nameof(command.RevisionReason), 1000),
            Required(command.CreatedBy, nameof(command.CreatedBy), 128),
            source,
            key,
            DateTime.UtcNow);
        _db.QuoteVersions.Add(version);
        quote.CurrentVersionNumber = version.VersionNumber;
        quote.Status = QuoteStatuses.Draft;
        quote.UpdatedAt = version.CreatedAt;
        await _db.SaveChangesAsync(cancellationToken);
        return new(await LoadAsync(quote.Id, cancellationToken), version.Id, version.Id, false);
    }

    public async Task<QuoteMutationResult> DecideApprovalAsync(
        DecideQuoteApprovalCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await _db.QuoteApprovalDecisions.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            return new(await LoadAsync(replay.QuoteId, cancellationToken),
                replay.Id, null, true);
        }

        var quote = await CurrentQuoteAsync(command.QuoteId, cancellationToken);
        var current = CurrentVersion(quote);
        if (current.Id != command.QuoteVersionId)
        {
            throw new InvalidOperationException("Approval must target the current quote version.");
        }
        if (quote.Status != QuoteStatuses.Draft)
        {
            throw new InvalidOperationException("Only a DRAFT quote version can be approved or rejected.");
        }
        if (await _db.QuoteApprovalDecisions.AsNoTracking()
            .AnyAsync(x => x.QuoteVersionId == current.Id, cancellationToken))
        {
            throw new InvalidOperationException("The current quote version already has an approval decision.");
        }

        var decisionCode = Required(command.Decision, nameof(command.Decision), 32).ToUpperInvariant();
        if (decisionCode is not QuoteApprovalDecisions.Approved
            and not QuoteApprovalDecisions.Rejected)
        {
            throw new InvalidOperationException("Decision must be APPROVED or REJECTED.");
        }

        var decision = new QuoteApprovalDecision
        {
            Id = Guid.NewGuid(),
            QuoteId = quote.Id,
            QuoteVersionId = current.Id,
            Decision = decisionCode,
            ReviewerLabel = Required(command.ReviewerLabel, nameof(command.ReviewerLabel), 128),
            Rationale = Required(command.Rationale, nameof(command.Rationale), 2000),
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = DateTime.UtcNow
        };
        quote.Status = decisionCode;
        quote.UpdatedAt = decision.CreatedAt;
        _db.QuoteApprovalDecisions.Add(decision);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await LoadAsync(quote.Id, cancellationToken), decision.Id, null, false);
    }

    public async Task<QuoteMutationResult> RecordOutcomeAsync(
        RecordQuoteOutcomeCommand command,
        CancellationToken cancellationToken = default)
    {
        var source = Required(command.SourceSystem, nameof(command.SourceSystem), 64).ToUpperInvariant();
        var key = Required(command.IdempotencyKey, nameof(command.IdempotencyKey), 128);
        var replay = await _db.QuoteOutcomes.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.SourceSystem == source && x.IdempotencyKey == key,
                cancellationToken);
        if (replay is not null)
        {
            return new(await LoadAsync(replay.QuoteId, cancellationToken),
                replay.Id, replay.NewQuoteVersionId, true);
        }

        var quote = await CurrentQuoteAsync(command.QuoteId, cancellationToken);
        var current = CurrentVersion(quote);
        if (current.Id != command.QuoteVersionId)
        {
            throw new InvalidOperationException("Outcome must target the current quote version.");
        }
        if (quote.Status != QuoteStatuses.Approved)
        {
            throw new InvalidOperationException("Only an APPROVED quote version can receive an outcome.");
        }

        var response = Required(command.Response, nameof(command.Response), 32).ToUpperInvariant();
        if (response is not QuoteOutcomeResponses.Accepted
            and not QuoteOutcomeResponses.Declined
            and not QuoteOutcomeResponses.Negotiated)
        {
            throw new InvalidOperationException(
                "Response must be ACCEPTED, DECLINED, or NEGOTIATED.");
        }

        var actor = Required(command.ActorLabel, nameof(command.ActorLabel), 128);
        var rationale = Required(command.Rationale, nameof(command.Rationale), 2000);
        var now = DateTime.UtcNow;
        QuoteVersion? negotiatedVersion = null;
        decimal? amount = response == QuoteOutcomeResponses.Declined
            ? null
            : current.TotalAmount;

        if (response == QuoteOutcomeResponses.Negotiated)
        {
            var prepared = await PrepareLinesAsync(
                command.NegotiatedLineItems
                    ?? throw new InvalidOperationException(
                        "NegotiatedLineItems are required for a NEGOTIATED response."),
                cancellationToken);
            EnsureCurrency(quote.CurrencyCode, prepared.CurrencyCode);
            negotiatedVersion = BuildVersion(
                quote.Id,
                current.Id,
                quote.CurrentVersionNumber + 1,
                prepared,
                $"Advertiser negotiation: {rationale}",
                actor,
                source,
                key,
                now);
            _db.QuoteVersions.Add(negotiatedVersion);
            quote.CurrentVersionNumber = negotiatedVersion.VersionNumber;
            quote.Status = QuoteStatuses.Draft;
            amount = negotiatedVersion.TotalAmount;
        }
        else
        {
            quote.Status = response == QuoteOutcomeResponses.Accepted
                ? QuoteStatuses.Accepted
                : QuoteStatuses.Declined;
        }

        var outcome = new QuoteOutcome
        {
            Id = Guid.NewGuid(),
            QuoteId = quote.Id,
            QuoteVersionId = current.Id,
            NewQuoteVersionId = negotiatedVersion?.Id,
            Response = response,
            Amount = amount,
            CurrencyCode = quote.CurrencyCode,
            ActorLabel = actor,
            Rationale = rationale,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = now
        };
        quote.UpdatedAt = now;
        _db.QuoteOutcomes.Add(outcome);
        await _db.SaveChangesAsync(cancellationToken);
        return new(await LoadAsync(quote.Id, cancellationToken),
            outcome.Id, negotiatedVersion?.Id, false);
    }

    public IQueryable<Quote> QuoteGraph() =>
        _db.Quotes
            .Include(x => x.AdvertiserOpportunity)
            .Include(x => x.Versions).ThenInclude(x => x.LineItems)
                .ThenInclude(x => x.RateRecommendation)
            .Include(x => x.ApprovalDecisions)
            .Include(x => x.Outcomes)
            .AsSplitQuery();

    private async Task<Quote> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await QuoteGraph().SingleAsync(x => x.Id == id, cancellationToken);

    private async Task<Quote> CurrentQuoteAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Quotes
            .Include(x => x.Versions).ThenInclude(x => x.LineItems)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new InvalidOperationException("QuoteId does not reference a quote.");

    private static QuoteVersion CurrentVersion(Quote quote) =>
        quote.Versions.Single(x => x.VersionNumber == quote.CurrentVersionNumber);

    private async Task<PreparedLines> PrepareLinesAsync(
        IReadOnlyList<QuoteLineCommand> commands,
        CancellationToken cancellationToken)
    {
        if (commands.Count == 0)
        {
            throw new InvalidOperationException("At least one quote line item is required.");
        }
        if (commands.Count > 50)
        {
            throw new InvalidOperationException("A quote cannot exceed 50 line items.");
        }

        var ids = commands.Select(x => x.RateRecommendationId).Distinct().ToList();
        var recommendations = await _db.RateRecommendations.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (recommendations.Count != ids.Count)
        {
            throw new InvalidOperationException(
                "Every quote line must reference an existing rate recommendation.");
        }
        var currencies = recommendations.Values.Select(x => x.CurrencyCode)
            .Distinct(StringComparer.Ordinal).ToList();
        if (currencies.Count != 1)
        {
            throw new InvalidOperationException("All quote lines must use one currency.");
        }

        var lines = new List<PreparedLine>(commands.Count);
        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            if (command.Quantity <= 0 || command.UnitAmount <= 0)
            {
                throw new InvalidOperationException(
                    "Quote line quantity and unit amount must be positive.");
            }
            var recommendation = recommendations[command.RateRecommendationId];
            lines.Add(new PreparedLine(
                recommendation.Id,
                Required(command.Description, nameof(command.Description), 500),
                command.Quantity,
                Money(command.UnitAmount),
                Money(command.Quantity * command.UnitAmount),
                recommendation.CurrencyCode,
                recommendation.RangeLow,
                recommendation.RangeTarget,
                recommendation.RangeHigh,
                index + 1));
        }
        return new(currencies.Single(), lines);
    }

    private static QuoteVersion BuildVersion(
        Guid quoteId,
        Guid? parentVersionId,
        int versionNumber,
        PreparedLines prepared,
        string reason,
        string createdBy,
        string source,
        string key,
        DateTime createdAt)
    {
        var total = prepared.Lines.Sum(x => x.LineAmount);
        var version = new QuoteVersion
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            ParentVersionId = parentVersionId,
            VersionNumber = versionNumber,
            CurrencyCode = prepared.CurrencyCode,
            SubtotalAmount = total,
            TotalAmount = total,
            RevisionReason = reason,
            CreatedBy = createdBy,
            SourceSystem = source,
            IdempotencyKey = key,
            CreatedAt = createdAt
        };
        foreach (var line in prepared.Lines)
        {
            version.LineItems.Add(new QuoteLineItem
            {
                Id = Guid.NewGuid(),
                QuoteVersionId = version.Id,
                RateRecommendationId = line.RateRecommendationId,
                SortOrder = line.SortOrder,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitAmount = line.UnitAmount,
                LineAmount = line.LineAmount,
                CurrencyCode = line.CurrencyCode,
                RecommendationLow = line.RecommendationLow,
                RecommendationTarget = line.RecommendationTarget,
                RecommendationHigh = line.RecommendationHigh
            });
        }
        return version;
    }

    private static void EnsureCurrency(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Quote revisions must preserve the quote currency.");
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

    private sealed record PreparedLine(
        Guid RateRecommendationId,
        string Description,
        decimal Quantity,
        decimal UnitAmount,
        decimal LineAmount,
        string CurrencyCode,
        decimal RecommendationLow,
        decimal RecommendationTarget,
        decimal RecommendationHigh,
        int SortOrder);

    private sealed record PreparedLines(
        string CurrencyCode,
        IReadOnlyList<PreparedLine> Lines);
}
