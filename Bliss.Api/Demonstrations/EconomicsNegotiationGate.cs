using Bliss.Domain.Demonstrations;
using Bliss.Domain.Economics;
using Bliss.Domain.Entities;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Demonstrations;

/// <summary>
/// Reads the approved Economics line and, only inside that envelope,
/// records a NEGOTIATED draft through the existing quote ledger.
/// </summary>
public sealed class EconomicsNegotiationGate(BlissDbContext database, QuoteService quotes)
{
    public async Task<PreparedNegotiation> ResolveAsync(
        string? economicsQuoteId,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            var read = await ReadAsync(economicsQuoteId, cancellationToken);
            if (read.MultipleLines)
            {
                return NegotiationEnvelope.Present(
                    NegotiationEnvelope.Decide(message, null, multipleLines: true));
            }

            var decision = NegotiationEnvelope.Decide(message, read.Line);
            if (decision.Kind != NegotiationKind.Inside || read.Envelope is null || decision.Proposal is null)
            {
                return NegotiationEnvelope.Present(decision);
            }

            if (read.Envelope.Quantity != 1m)
            {
                return new PreparedNegotiation(
                    "Ask Alpha will not resize the quoted quantity. I cannot invent a price. This is not a win. Delivery remains NOT_SENT.",
                    true,
                    "handoff");
            }

            var proposal = decision.Proposal.Value;
            var result = await quotes.RecordOutcomeAsync(
                new RecordQuoteOutcomeCommand(
                    read.Envelope.QuoteId,
                    read.Envelope.VersionId,
                    QuoteOutcomeResponses.Negotiated,
                    "Ask Alpha",
                    "A visitor proposal inside the Economics envelope. It is not an acceptance.",
                    [
                        new QuoteLineCommand(
                            read.Envelope.RateRecommendationId,
                            read.Envelope.Description,
                            1m,
                            proposal)
                    ],
                    "ASK_ALPHA",
                    "ask-alpha-" + read.Envelope.QuoteId.ToString("N") + "-" + EconomicsPriceSpeech.FormatAmount(proposal)),
                cancellationToken);
            var version = result.Quote.Versions.Single(item => item.Id == result.NewQuoteVersionId);
            var ledger = EconomicsPriceSpeech.FormatAmount(version.TotalAmount);
            if (EconomicsPriceSpeech.Speak(ledger, read.Envelope.Currency) is null)
            {
                return Refused();
            }

            return NegotiationEnvelope.Present(
                decision,
                ledger,
                read.Envelope.Currency);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Refused();
        }
    }

    private async Task<EnvelopeRead> ReadAsync(string? economicsQuoteId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(economicsQuoteId, out var quoteId) || quoteId == Guid.Empty)
        {
            return EnvelopeRead.None;
        }

        var quote = await database.Quotes.AsNoTracking()
            .Include(item => item.Versions)
            .ThenInclude(item => item.LineItems)
            .FirstOrDefaultAsync(item => item.Id == quoteId, cancellationToken);
        if (quote is null || quote.Status != QuoteStatuses.Approved)
        {
            return EnvelopeRead.None;
        }

        var current = quote.Versions.SingleOrDefault(item => item.VersionNumber == quote.CurrentVersionNumber);
        if (current is null)
        {
            return EnvelopeRead.None;
        }

        if (current.LineItems.Count != 1)
        {
            return EnvelopeRead.Split;
        }

        var line = current.LineItems.Single();
        var currency = (string.IsNullOrWhiteSpace(line.CurrencyCode) ? quote.CurrencyCode : line.CurrencyCode)
            .Trim()
            .ToUpperInvariant();
        if (line.RecommendationLow <= 0
            || line.RecommendationHigh < line.RecommendationLow
            || line.RateRecommendationId == Guid.Empty
            || string.IsNullOrWhiteSpace(line.Description)
            || line.Quantity <= 0
            || EconomicsPriceSpeech.Speak(EconomicsPriceSpeech.FormatAmount(line.RecommendationLow), currency) is null
            || EconomicsPriceSpeech.Speak(EconomicsPriceSpeech.FormatAmount(line.RecommendationHigh), currency) is null
            || !string.Equals(quote.CurrencyCode.Trim(), currency, StringComparison.OrdinalIgnoreCase))
        {
            return EnvelopeRead.None;
        }

        return new EnvelopeRead(
            new NegotiationLine(line.RecommendationLow, line.RecommendationHigh, currency),
            new ApprovedLine(
                quote.Id,
                current.Id,
                line.RateRecommendationId,
                line.Description.Trim(),
                line.Quantity,
                currency));
    }

    private static PreparedNegotiation Refused() =>
        NegotiationEnvelope.Present(NegotiationEnvelope.Decide("Can you negotiate?", null));

    private sealed record EnvelopeRead(NegotiationLine? Line, ApprovedLine? Envelope, bool MultipleLines = false)
    {
        public static EnvelopeRead None { get; } = new(null, null);

        public static EnvelopeRead Split { get; } = new(null, null, true);
    }

    private sealed record ApprovedLine(
        Guid QuoteId,
        Guid VersionId,
        Guid RateRecommendationId,
        string Description,
        decimal Quantity,
        string Currency);
}
