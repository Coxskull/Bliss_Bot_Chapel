using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Tests.Financials;
public sealed class FinancialPostgresFactAttribute : FactAttribute
{
 public FinancialPostgresFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES"))) Skip="Requires explicit isolated financial PostgreSQL fixture.";}
}
public sealed class PostgresJournalTests
{
 [FinancialPostgresFact] public async Task Concurrent_retries_conflicts_reversals_and_raw_mutation_are_enforced()
 {
  var connection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES") ?? throw new InvalidOperationException("Explicit PostgreSQL fixture required.");
  var fixture=new NpgsqlConnectionStringBuilder(connection);Assert.Equal("127.0.0.1",fixture.Host);Assert.Equal("financial_test",fixture.Database);
  await using var data=NpgsqlDataSource.Create(connection);
  var scope=new FinancialScope(Guid.NewGuid(),FinancialModule.BlissBot,Guid.NewGuid());
  JournalLine[] lines=[new(Guid.NewGuid(),Guid.NewGuid(),"advertiser",JournalSide.Debit,100),new(Guid.NewGuid(),Guid.NewGuid(),"creator",JournalSide.Credit,100)];
  var journal=new FinancialJournal(scope,"USD","retry","quote",lines);var store=new PostgresJournalStore(data);
  var ids=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>store.Append(journal)));Assert.All(ids,id=>Assert.Equal(journal.Id,id));
  var different=new FinancialJournal(scope,"USD","retry","different quote",lines);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Append(different));
  var academy=new FinancialJournal(scope with {Module=FinancialModule.CreatorAcademy},"USD","retry","quote",lines);Assert.NotEqual(journal.Id,await store.Append(academy));
  var planner=new FinancialJournal(scope with {Module=FinancialModule.WeddingPlanner},"USD","retry","quote",lines);Assert.NotEqual(journal.Id,await store.Append(planner));
  var php=new FinancialJournal(scope,"PHP","retry","quote",lines);Assert.NotEqual(journal.Id,await store.Append(php));
  var merchant=new FinancialJournal(scope with {MerchantId=Guid.NewGuid()},"USD","retry","quote",lines);Assert.NotEqual(journal.Id,await store.Append(merchant));
  var eventJournal = new FinancialJournal(scope,"USD","stripe-operation","stripe-event",lines);
  var payment = new VerifiedSandboxPayment("evt_"+Guid.NewGuid().ToString("N"),"pi_"+Guid.NewGuid().ToString("N"),100,"USD");
  var eventIds=await Task.WhenAll(Enumerable.Range(0,10).Select(_=>store.AppendVerifiedPayment(eventJournal,payment)));
  Assert.All(eventIds,id=>Assert.Equal(eventJournal.Id,id));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.AppendVerifiedPayment(new FinancialJournal(scope with{Module=FinancialModule.WeddingPlanner},"USD","stripe-other","stripe-event",lines),payment));
  await Assert.ThrowsAsync<ArgumentException>(()=>store.AppendVerifiedPayment(eventJournal,payment with{AmountMinor=99}));
  await Assert.ThrowsAsync<PostgresException>(()=>store.AppendVerifiedPayment(new FinancialJournal(scope,"USD","duplicate-pi","stripe-event",lines),payment with{EventId="evt_"+Guid.NewGuid().ToString("N")}));
  await using(var count=data.CreateCommand("SELECT count(*) FROM financials.stripe_receipts WHERE event_id=@e")){count.Parameters.AddWithValue("e",payment.EventId);Assert.Equal(1L,await count.ExecuteScalarAsync());}
  await using(var mutation=data.CreateCommand("UPDATE financials.stripe_receipts SET fingerprint='tamper'")){await Assert.ThrowsAsync<PostgresException>(async()=>await mutation.ExecuteNonQueryAsync());}
  await store.Append(journal.Reverse("refund","refund1"));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Append(journal.Reverse("refund2","refund2")));
  foreach(var sql in new[]{"UPDATE financials.journals SET evidence='changed'","DELETE FROM financials.lines","TRUNCATE financials.lines","INSERT INTO financials.lines SELECT journal_id,99,account_id,owner_id,role,side,minor_units FROM financials.lines LIMIT 1","INSERT INTO financials.journals(id,merchant_id,module,campaign_id,currency,retry_key,evidence,fingerprint) VALUES(gen_random_uuid(),gen_random_uuid(),'BlissBot',gen_random_uuid(),'USD','empty','e','f')"})
  {await using var cmd=data.CreateCommand(sql);await Assert.ThrowsAsync<PostgresException>(async()=>await cmd.ExecuteNonQueryAsync());}
 }
}
