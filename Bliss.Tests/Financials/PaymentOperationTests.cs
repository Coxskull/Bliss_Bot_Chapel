using System.Security.Claims;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Tests.Financials;
public sealed class PaymentOperationTests
{
 [FinancialPostgresFact]public async Task Server_quote_amount_recipient_and_authenticated_owner_control_concurrent_operations()
 {
  var connection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;var builder=new NpgsqlConnectionStringBuilder(connection);Assert.Equal("127.0.0.1",builder.Host);Assert.Equal("financial_test",builder.Database);
  await using var data=NpgsqlDataSource.Create(connection);var store=new PaymentOperationStore(data);
  var subject="fixture-"+Guid.NewGuid();var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,subject)],"fixture"));
  var quote=new AuthorizedQuoteSnapshot(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),FinancialModule.WeddingPlanner,Guid.NewGuid()),subject,Guid.NewGuid(),12345,"USD");await store.FreezeQuote(quote);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Prepare(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,subject)])),quote.Id,"a"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Prepare(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"other")],"fixture")),quote.Id,"a"));
  var operations=await Task.WhenAll(Enumerable.Range(0,10).Select(i=>store.Prepare(user,quote.Id,"retry-"+i)));Assert.All(operations,x=>{Assert.Equal(operations[0].Id,x.Id);Assert.Equal(12345,x.AmountMinor);Assert.Equal(quote.RecipientAccountId,x.RecipientAccountId);Assert.Equal("USD",x.Currency);});
  var second=quote with{Id=Guid.NewGuid(),SourceQuoteVersionId=Guid.NewGuid()};await store.FreezeQuote(second);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Prepare(user,second.Id,"retry-0"));
  await Assert.ThrowsAsync<PostgresException>(()=>store.FreezeQuote(quote with{Id=Guid.NewGuid(),Scope=quote.Scope with{Module=FinancialModule.BlissBot}}));
  await using var mutation=data.CreateCommand("UPDATE financials.authorized_quotes SET amount_minor=1");await Assert.ThrowsAsync<PostgresException>(async()=>await mutation.ExecuteNonQueryAsync());
 }
}
