using Bliss.Domain.Demonstrations;
using Bliss.Domain.Economics;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Bliss.Api.Demonstrations;

public sealed class EconomicsAcceptedPriceReader(BlissDbContext database)
{
    public async Task<AcceptedEconomicsPrice?> ReadAcceptedAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        try
        {
            var quote = await database.Quotes.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == quoteId, cancellationToken);
            if (quote is null || quote.Status != QuoteStatuses.Accepted)
            {
                return null;
            }

            var outcome = await database.Set<Bliss.Domain.Entities.QuoteOutcome>().AsNoTracking()
                .Where(item => item.QuoteId == quoteId
                    && item.Response == QuoteOutcomeResponses.Accepted
                    && item.Amount != null)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (outcome?.Amount is null)
            {
                return null;
            }

            var currency = string.IsNullOrWhiteSpace(outcome.CurrencyCode)
                ? quote.CurrencyCode
                : outcome.CurrencyCode;
            var amount = EconomicsPriceSpeech.FormatAmount(outcome.Amount.Value);
            if (EconomicsPriceSpeech.Speak(amount, currency) is null)
            {
                return null;
            }

            return new AcceptedEconomicsPrice(amount, currency.Trim().ToUpperInvariant());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
