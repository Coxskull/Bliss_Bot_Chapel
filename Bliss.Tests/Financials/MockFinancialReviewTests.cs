using System.Security.Claims;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Tests.Financials;
[CollectionDefinition("Mock financial review isolation",DisableParallelization=true)]
public sealed class MockFinancialReviewIsolation {}
[Collection("Mock financial review isolation")]
public sealed class MockFinancialReviewTests
{
 private static NpgsqlDataSource Data()=>NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!);
 private static async Task<(PreparedPaymentOperation Op,MockReviewAuthority Authority)> Prepare(NpgsqlDataSource data,long amount=500)
 {
  var subject="review-owner-"+Guid.NewGuid();var id=Guid.NewGuid();var q=new AuthorizedQuoteSnapshot(id,id,new(Guid.NewGuid(),FinancialModule.CreatorAcademy,Guid.NewGuid()),subject,Guid.NewGuid(),amount,"USD");var quotes=new PaymentOperationStore(data);await quotes.FreezeQuote(q);var user=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub",subject)],"fixture"));var op=await quotes.Prepare(user,id,"fixture");
  await using var seed=data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '21 hours',clock_timestamp()-interval '20 hours')");seed.Parameters.AddWithValue("a",Guid.NewGuid());seed.Parameters.AddWithValue("i",op.Id);await seed.ExecuteNonQueryAsync();return(op,new(id,q.Scope,q.RecipientAccountId,subject));
 }
 [FinancialPostgresFact]public async Task Note_history_pages_preserve_order_scope_and_case_bound_cursors()
 {
  await using var data=Data();var prepared=await Prepare(data);var store=new MockFinancialReviewStore(data);MockReviewAuthority[] authorities=[prepared.Authority];var evidence=await store.Read(prepared.Op.Id,authorities);
  var created=new List<MockReviewNote>();for(var i=0;i<5;i++)created.Add(await store.AddNote(prepared.Op.Id,authorities,"history-reader",evidence.EvidenceFingerprint,"Note "+i,"history-"+i));
  var all=new List<MockReviewNote>();Guid? cursor=null;do{var page=await store.Notes(prepared.Op.Id,authorities,2,cursor);Assert.InRange(page.Items.Count,1,2);all.AddRange(page.Items);cursor=page.NextAfter;}while(cursor is not null);
  Assert.Equal(created.Select(n=>n.Id),all.Select(n=>n.Id));Assert.Equal(5,all.Select(n=>n.Id).Distinct().Count());Assert.Empty((await store.Notes(prepared.Op.Id,authorities,2,all[^1].Id)).Items);
  await Assert.ThrowsAsync<ArgumentException>(()=>store.Notes(prepared.Op.Id,authorities,0,null));await Assert.ThrowsAsync<ArgumentException>(()=>store.Notes(prepared.Op.Id,authorities,101,null));await Assert.ThrowsAsync<ArgumentException>(()=>store.Notes(prepared.Op.Id,authorities,2,Guid.NewGuid()));
  var other=await Prepare(data);await Assert.ThrowsAsync<ArgumentException>(()=>store.Notes(other.Op.Id,[other.Authority],2,created[0].Id));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Notes(prepared.Op.Id,[other.Authority],2,null));
  Assert.True((await store.Read(prepared.Op.Id,authorities)).RequiresReview);Assert.Null(await new MockPaymentCaptureStore(data).Captured(prepared.Op.Id));
 }
 [FinancialPostgresFact]public async Task Scoped_evidence_notes_are_concurrent_idempotent_immutable_and_never_clear_review()
 {
  await using var data=Data();var prepared=await Prepare(data,9007199254740993);var store=new MockFinancialReviewStore(data);MockReviewAuthority[] authorities=[prepared.Authority];var snapshot=await store.Read(prepared.Op.Id,authorities);Assert.True(snapshot.RequiresReview);Assert.Contains("\"amount_minor\": \"9007199254740993\"",snapshot.EvidenceJson);
  var page=await store.Queue(authorities,null,1,null);Assert.Single(page.Items);Assert.Equal(prepared.Op.Id,page.Items[0].OperationId);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.Read(prepared.Op.Id,[prepared.Authority with{Scope=prepared.Authority.Scope with{MerchantId=Guid.NewGuid()}}]));
  var notes=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>store.AddNote(prepared.Op.Id,authorities,"qa",snapshot.EvidenceFingerprint,"Observe unresolved fixture; no release.","same")));Assert.All(notes,n=>Assert.Equal(notes[0].Id,n.Id));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.AddNote(prepared.Op.Id,authorities,"qa",snapshot.EvidenceFingerprint,"changed","same"));
  var other=await Prepare(data);var otherSnapshot=await store.Read(other.Op.Id,[other.Authority]);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.AddNote(other.Op.Id,[other.Authority],"qa",otherSnapshot.EvidenceFingerprint,"Observe unresolved fixture; no release.","same"));
  var historical=await store.ReadNote(notes[0].Id,authorities);Assert.Equal(snapshot.EvidenceJson,historical.EvidenceJson);Assert.True((await store.Read(prepared.Op.Id,authorities)).RequiresReview);Assert.Null(await new MockPaymentCaptureStore(data).Captured(prepared.Op.Id));
  await using var update=data.CreateCommand("UPDATE financials.mock_review_notes SET note_text='changed'");await Assert.ThrowsAsync<PostgresException>(async()=>await update.ExecuteNonQueryAsync());
  await using var delete=data.CreateCommand("DELETE FROM financials.mock_review_notes");await Assert.ThrowsAsync<PostgresException>(async()=>await delete.ExecuteNonQueryAsync());
  await using var truncate=data.CreateCommand("TRUNCATE financials.mock_review_notes");await Assert.ThrowsAsync<PostgresException>(async()=>await truncate.ExecuteNonQueryAsync());
 }
 [FinancialPostgresFact]public async Task Changed_or_busy_evidence_refuses_new_notes_and_preserves_historical_snapshots()
 {
  await using var data=Data();var prepared=await Prepare(data);var store=new MockFinancialReviewStore(data);MockReviewAuthority[] authorities=[prepared.Authority];var snapshot=await store.Read(prepared.Op.Id,authorities);var note=await store.AddNote(prepared.Op.Id,authorities,"qa-stale",snapshot.EvidenceFingerprint,"Original evidence observed.","first");
  await using(var c=await data.OpenConnectionAsync()){await using var tx=await c.BeginTransactionAsync();await using var busy=new NpgsqlCommand("LOCK TABLE financials.payment_dispatch_results IN ROW EXCLUSIVE MODE",c,tx);await busy.ExecuteNonQueryAsync();var failure=await Assert.ThrowsAsync<PostgresException>(()=>store.AddNote(prepared.Op.Id,authorities,"qa-stale",snapshot.EvidenceFingerprint,"Concurrent writer active.","busy"));Assert.Equal("55P03",failure.SqlState);await tx.RollbackAsync();}
  await using(var newAttempt=data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '3 minutes',clock_timestamp()-interval '1 minute')")){newAttempt.Parameters.AddWithValue("a",Guid.NewGuid());newAttempt.Parameters.AddWithValue("i",prepared.Op.Id);await newAttempt.ExecuteNonQueryAsync();}
  var changed=await store.Read(prepared.Op.Id,authorities);Assert.NotEqual(snapshot.EvidenceFingerprint,changed.EvidenceFingerprint);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.AddNote(prepared.Op.Id,authorities,"qa-stale",snapshot.EvidenceFingerprint,"Old snapshot new request.","stale"));
  Assert.Equal(note.Id,(await store.AddNote(prepared.Op.Id,authorities,"qa-stale",snapshot.EvidenceFingerprint,"Original evidence observed.","first")).Id);
  Assert.Equal(snapshot.EvidenceJson,(await store.ReadNote(note.Id,authorities)).EvidenceJson);Assert.Single(changed.Notes);
  // Capture/refund facts must also change the evidence version while prior snapshots remain intact.
  var pi="pi_fixture_"+prepared.Op.Id.ToString("N");await new MockPaymentReconciliationStore(data).Recover(prepared.Op,"mock-review",pi,"processing","qa");
  var capture=new MockPaymentCaptureStore(data);var journal=await capture.CaptureBound(prepared.Op,new("evt_"+Guid.NewGuid().ToString("N"),pi,500,"USD"),Guid.NewGuid(),Guid.NewGuid(),"mock-review",new string('A',64));
  await new MockPaymentReconciliationStore(data).Observe(prepared.Op,"mock-review","evt_"+Guid.NewGuid().ToString("N"),pi,"CANCELED",new string('B',64));
  var paidSnapshot=await store.Read(prepared.Op.Id,authorities);var paidNote=await store.AddNote(prepared.Op.Id,authorities,"qa-stale",paidSnapshot.EvidenceFingerprint,"Captured fixture requires review.","paid");
  var actor=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","review-refund"),new Claim(ClaimTypes.Role,"bliss.operator")],"fixture"));var refunds=new RefundReconciliationStore(data);var refund=await refunds.Reserve(actor,prepared.Op.Scope,pi,100,"review-partial");await refunds.Reconcile(actor,prepared.Op.Scope,refund.Id,"re_fixture_"+refund.Id.ToString("N"),"SUCCEEDED");
  var refundedSnapshot=await store.Read(prepared.Op.Id,authorities);Assert.NotEqual(paidSnapshot.EvidenceFingerprint,refundedSnapshot.EvidenceFingerprint);using var document=System.Text.Json.JsonDocument.Parse(refundedSnapshot.EvidenceJson);Assert.Equal(2,document.RootElement.GetProperty("journals").GetArrayLength());Assert.Equal("100",document.RootElement.GetProperty("refund_requests")[0].GetProperty("amount_minor").GetString());
  await Assert.ThrowsAsync<InvalidOperationException>(()=>store.AddNote(prepared.Op.Id,authorities,"qa-stale",paidSnapshot.EvidenceFingerprint,"Refund changed evidence.","paid-stale"));Assert.Equal(paidSnapshot.EvidenceJson,(await store.ReadNote(paidNote.Id,authorities)).EvidenceJson);Assert.Equal(journal,await capture.Captured(prepared.Op.Id));Assert.True(refundedSnapshot.RequiresReview);
 }
}
