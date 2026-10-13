using System.Security.Claims;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Tests.Financials;
public sealed class MockPaymentReconciliationTests
{
 private static NpgsqlDataSource Data()=>NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!);
 private static async Task<(ClaimsPrincipal User,PreparedPaymentOperation Operation)> Prepare(NpgsqlDataSource data,bool aged)
 {
  var subject="reconcile-"+Guid.NewGuid();var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub",subject)],"fixture"));var quotes=new PaymentOperationStore(data);var q=new AuthorizedQuoteSnapshot(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),FinancialModule.CreatorAcademy,Guid.NewGuid()),subject,Guid.NewGuid(),500,"USD");await quotes.FreezeQuote(q);var op=await quotes.Prepare(user,q.Id,"mock");
  if(aged){await using var seed=data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '21 hours',clock_timestamp()-interval '20 hours')");seed.Parameters.AddWithValue("a",Guid.NewGuid());seed.Parameters.AddWithValue("i",op.Id);await seed.ExecuteNonQueryAsync();}return(user,op);
 }
 [FinancialPostgresFact]public async Task Recovery_requires_aged_exact_authority_and_prevents_identity_reuse_or_mutation()
 {
  await using var data=Data();var store=new MockPaymentReconciliationStore(data);var first=await Prepare(data,true);var pi="pi_fixture_"+first.Operation.Id.ToString("N");
  Assert.Equal("MANUAL_REVIEW_TEST_ONLY",(await store.Read(first.Operation.Id)).State);
  var active=await Prepare(data,true);await using(var lease=data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id) VALUES(@a,@i)")){lease.Parameters.AddWithValue("a",Guid.NewGuid());lease.Parameters.AddWithValue("i",active.Operation.Id);await lease.ExecuteNonQueryAsync();}
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Recover(active.Operation,"mock-test","pi_fixture_"+active.Operation.Id.ToString("N"),"processing","qa"));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Recover(first.Operation with{AmountMinor=501},"mock-test",pi,"processing","qa"));
  var id=await store.Recover(first.Operation,"mock-test",pi,"processing","qa");Assert.Equal(id,await store.Recover(first.Operation,"mock-test",pi,"processing","qa-other"));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Recover(first.Operation,"mock-other",pi,"processing","qa"));
  var young=await Prepare(data,false);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Recover(young.Operation,"mock-test","pi_fixture_"+young.Operation.Id.ToString("N"),"processing","qa"));
  var other=await Prepare(data,true);await Assert.ThrowsAsync<PostgresException>(()=>store.Recover(other.Operation,"mock-test",pi,"processing","qa"));
  var claim=await new PaymentDispatchStore(data).Claim(young.User,young.Operation.Id);Assert.False(await new PaymentDispatchStore(data).Complete(claim,new(pi,"processing",500,"USD")));
  Assert.Equal("BOUND",(await new PaymentDispatchStore(data).Claim(first.User,first.Operation.Id)).State);Assert.Null(await new MockPaymentCaptureStore(data).Captured(first.Operation.Id));
  await using var update=data.CreateCommand("UPDATE financials.mock_payment_recoveries SET actor_subject='changed'");await Assert.ThrowsAsync<PostgresException>(async()=>await update.ExecuteNonQueryAsync());
  await using var truncate=data.CreateCommand("TRUNCATE financials.mock_payment_recoveries");await Assert.ThrowsAsync<PostgresException>(async()=>await truncate.ExecuteNonQueryAsync());
 }
 [FinancialPostgresFact]public async Task Status_is_append_only_globally_deduplicated_and_cancellation_never_reverses_cash()
 {
  await using var data=Data();var first=await Prepare(data,true);var pi="pi_fixture_"+first.Operation.Id.ToString("N");var store=new MockPaymentReconciliationStore(data);await store.Recover(first.Operation,"mock-test",pi,"processing","qa");
  var eventId="evt_"+Guid.NewGuid().ToString("N");var fingerprint=new string('A',64);await store.Observe(first.Operation,"mock-test",eventId,pi,"FAILED",fingerprint);await store.Observe(first.Operation,"mock-test",eventId,pi,"FAILED",fingerprint);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Observe(first.Operation,"mock-test",eventId,pi,"PROCESSING",fingerprint));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Observe(first.Operation,"mock-other","evt_"+Guid.NewGuid().ToString("N"),pi,"FAILED",fingerprint));
  var capture=new MockPaymentCaptureStore(data);await Assert.ThrowsAsync<InvalidOperationException>(()=>capture.CaptureBound(first.Operation,new(eventId,pi,500,"USD"),Guid.NewGuid(),Guid.NewGuid(),"mock-test",fingerprint));
  var payer=Guid.NewGuid();var owner=Guid.NewGuid();var success=new VerifiedSandboxPayment("evt_"+Guid.NewGuid().ToString("N"),pi,500,"USD");var journal=await capture.CaptureBound(first.Operation,success,payer,owner,"mock-test",fingerprint);
  await store.Observe(first.Operation,"mock-test","evt_"+Guid.NewGuid().ToString("N"),pi,"CANCELED",fingerprint);Assert.Equal(journal,await capture.Captured(first.Operation.Id));var reading=await store.Read(first.Operation.Id);Assert.Equal("CAPTURED_TEST_ONLY",reading.State);Assert.True(reading.RequiresReview);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.Observe(first.Operation,"mock-test",success.EventId,pi,"FAILED",fingerprint));
  await using var update=data.CreateCommand("UPDATE financials.mock_payment_status_events SET state='PROCESSING'");await Assert.ThrowsAsync<PostgresException>(async()=>await update.ExecuteNonQueryAsync());
  await using var delete=data.CreateCommand("DELETE FROM financials.mock_payment_status_events");await Assert.ThrowsAsync<PostgresException>(async()=>await delete.ExecuteNonQueryAsync());
  await using var truncate=data.CreateCommand("TRUNCATE financials.mock_payment_status_events");await Assert.ThrowsAsync<PostgresException>(async()=>await truncate.ExecuteNonQueryAsync());
  var canceled=await Prepare(data,true);var canceledPi="pi_fixture_"+canceled.Operation.Id.ToString("N");await store.Recover(canceled.Operation,"mock-test",canceledPi,"canceled","qa");
  await Assert.ThrowsAsync<InvalidOperationException>(()=>capture.CaptureBound(canceled.Operation,new("evt_"+Guid.NewGuid().ToString("N"),canceledPi,500,"USD"),Guid.NewGuid(),Guid.NewGuid(),"mock-test",fingerprint));
 }
}
