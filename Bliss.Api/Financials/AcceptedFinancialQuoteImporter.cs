using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Bliss.Api.Financials;

// Registry entries are owner/server-controlled, never supplied by payment request bodies.
public sealed record FundingBinding(Guid SourceQuoteId, Guid SourceVersionId, FinancialScope Scope,
 string AdvertiserSubject, Guid AdvertiserId, Guid RecipientAccountId, Guid? PayerAccountId=null, Guid? RecipientOwnerId=null,string MockProfileId="mock-default");
public sealed class AcceptedFinancialQuoteImporter(BlissDbContext db)
{
 public async Task<AuthorizedQuoteSnapshot> Read(FundingBinding binding, CancellationToken ct=default)
 {
  var quote=await db.Quotes.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==binding.SourceQuoteId,ct);
  var version=await db.QuoteVersions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==binding.SourceVersionId && x.QuoteId==binding.SourceQuoteId,ct);
  var approval=await db.QuoteApprovalDecisions.AsNoTracking().Where(x=>x.QuoteId==binding.SourceQuoteId).OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).FirstOrDefaultAsync(ct);
  var outcome=await db.QuoteOutcomes.AsNoTracking().Where(x=>x.QuoteId==binding.SourceQuoteId).OrderByDescending(x=>x.CreatedAt).ThenByDescending(x=>x.Id).FirstOrDefaultAsync(ct);
  if(quote?.Status!="ACCEPTED"||version is null||version.VersionNumber!=quote.CurrentVersionNumber||approval?.Decision!="APPROVED"||approval.QuoteVersionId!=version.Id||outcome?.Response!="ACCEPTED"||outcome.QuoteVersionId!=version.Id||outcome.Amount!=version.TotalAmount||quote.CurrencyCode!="USD"||version.CurrencyCode!="USD"||outcome.CurrencyCode!="USD"||version.TotalAmount<=0||version.TotalAmount>long.MaxValue/100m)throw new InvalidOperationException("Current approved and accepted USD quote version required.");
  var minor=checked(version.TotalAmount*100m);
  if(minor!=decimal.Truncate(minor)||minor>long.MaxValue)throw new InvalidOperationException("Quote amount cannot be represented as USD minor units.");
  return new(version.Id,version.Id,binding.Scope,binding.AdvertiserSubject,binding.RecipientAccountId,(long)minor,"USD");
 }
}
