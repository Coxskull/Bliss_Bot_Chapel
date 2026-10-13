using System.Security.Claims;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Tests.Financials;
public sealed class RefundReconciliationTests
{
 [FinancialPostgresFact]public async Task Partial_refunds_reserve_serially_preserve_attribution_and_commit_results_with_journals()
 {
  var connection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!;var fixture=new NpgsqlConnectionStringBuilder(connection);Assert.Equal("127.0.0.1",fixture.Host);Assert.Equal("financial_test",fixture.Database);await using var data=NpgsqlDataSource.Create(connection);
  var scope=new FinancialScope(Guid.NewGuid(),FinancialModule.CreatorAcademy,Guid.NewGuid());var payee=Guid.NewGuid();JournalLine[] lines=[new(Guid.NewGuid(),scope.MerchantId,"TEST_CASH",JournalSide.Debit,100),new(Guid.NewGuid(),payee,"TEST_RECIPIENT",JournalSide.Credit,100)];var journal=new FinancialJournal(scope,"USD","payment","TEST_CAPTURE",lines);var payment=new VerifiedSandboxPayment("evt_"+Guid.NewGuid().ToString("N"),"pi_"+Guid.NewGuid().ToString("N"),100,"USD");await new PostgresJournalStore(data).AppendVerifiedPayment(journal,payment);
  var actor=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","refund-fixture"),new Claim(ClaimTypes.Role,"bliss.operator")],"fixture"));var store=new RefundReconciliationStore(data);
  var first=await store.Reserve(actor,scope,payment.PaymentIntentId,40,"first");Assert.Equal(first.Id,(await store.Reserve(actor,scope,payment.PaymentIntentId,40,"first")).Id);
  var attempts=await Task.WhenAll(Enumerable.Range(0,5).Select(async i=>{try{return await store.Reserve(actor,scope,payment.PaymentIntentId,60,"parallel-"+i);}catch(InvalidOperationException){return null;}}));Assert.Single(attempts,x=>x is not null);
  var provider="re_"+Guid.NewGuid().ToString("N");Assert.Null(await store.Reconcile(actor,scope,first.Id,provider,"PENDING"));var reversal=await store.Reconcile(actor,scope,first.Id,provider,"SUCCEEDED");Assert.NotNull(reversal);Assert.Equal(reversal,await store.Reconcile(actor,scope,first.Id,provider,"SUCCEEDED"));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Reconcile(actor,scope,first.Id,provider,"FAILED"));await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Reconcile(actor,scope with{CampaignId=Guid.NewGuid()},first.Id,provider,"SUCCEEDED"));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Reserve(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","viewer")],"fixture")),scope,payment.PaymentIntentId,1,"bad"));
  await using var rows=data.CreateCommand("SELECT owner_id,side,minor_units FROM financials.lines WHERE journal_id=@j AND owner_id=@o");rows.Parameters.AddWithValue("j",reversal!.Value);rows.Parameters.AddWithValue("o",payee);await using var reader=await rows.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal("Debit",reader.GetString(1));Assert.Equal(40,reader.GetInt64(2));
 }
}
