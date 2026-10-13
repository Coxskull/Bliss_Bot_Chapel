using Bliss.Api.Financials;
using Bliss.Domain.Entities;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Bliss.Tests.Financials;
public sealed class AcceptedFinancialQuoteTests
{
 [Theory][InlineData("BlissBot")][InlineData("CreatorAcademy")][InlineData("WeddingPlanner")]
 public async Task Exact_current_accepted_quote_imports_server_minor_amount_for_each_module(string module)
 {
  await using var db=new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  var quote=new Quote{Id=Guid.NewGuid(),Status="ACCEPTED",CurrentVersionNumber=1,CurrencyCode="USD"};var version=new QuoteVersion{Id=Guid.NewGuid(),QuoteId=quote.Id,VersionNumber=1,TotalAmount=123.45m,CurrencyCode="USD"};var approval=new QuoteApprovalDecision{Id=Guid.NewGuid(),QuoteId=quote.Id,QuoteVersionId=version.Id,Decision="APPROVED",CreatedAt=DateTime.UtcNow};var outcome=new QuoteOutcome{Id=Guid.NewGuid(),QuoteId=quote.Id,QuoteVersionId=version.Id,Response="ACCEPTED",Amount=123.45m,CurrencyCode="USD",CreatedAt=DateTime.UtcNow};db.AddRange(quote,version,approval,outcome);await db.SaveChangesAsync();
  var binding=new FundingBinding(quote.Id,version.Id,new(Guid.NewGuid(),Enum.Parse<FinancialModule>(module),Guid.NewGuid()),"fixture",Guid.NewGuid(),Guid.NewGuid());var importer=new AcceptedFinancialQuoteImporter(db);var snapshot=await importer.Read(binding);Assert.Equal(12345,snapshot.AmountMinor);Assert.Equal(binding.RecipientAccountId,snapshot.RecipientAccountId);
  quote.Status="REJECTED";await db.SaveChangesAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>importer.Read(binding));
 }
 [Theory][InlineData("amount")][InlineData("currency")][InlineData("version")][InlineData("revoked")][InlineData("fraction")]
 public async Task Inconsistent_or_revoked_source_cannot_authorize_payment(string defect)
 {
  await using var db=new BlissDbContext(new DbContextOptionsBuilder<BlissDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);var q=Guid.NewGuid();var v=Guid.NewGuid();db.AddRange(new Quote{Id=q,Status="ACCEPTED",CurrentVersionNumber=1,CurrencyCode="USD"},new QuoteVersion{Id=v,QuoteId=q,VersionNumber=1,TotalAmount=defect=="fraction"?1.001m:10m,CurrencyCode="USD"},new QuoteApprovalDecision{Id=Guid.NewGuid(),QuoteId=q,QuoteVersionId=v,Decision=defect=="revoked"?"REJECTED":"APPROVED",CreatedAt=DateTime.UtcNow},new QuoteOutcome{Id=Guid.NewGuid(),QuoteId=q,QuoteVersionId=defect=="version"?Guid.NewGuid():v,Response="ACCEPTED",Amount=defect=="amount"?9m:defect=="fraction"?1.001m:10m,CurrencyCode=defect=="currency"?"PHP":"USD",CreatedAt=DateTime.UtcNow});await db.SaveChangesAsync();var binding=new FundingBinding(q,v,new(Guid.NewGuid(),FinancialModule.BlissBot,Guid.NewGuid()),"fixture",Guid.NewGuid(),Guid.NewGuid());await Assert.ThrowsAsync<InvalidOperationException>(()=>new AcceptedFinancialQuoteImporter(db).Read(binding));
 }
}
