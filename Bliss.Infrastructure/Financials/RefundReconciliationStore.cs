using System.Security.Claims;
using Bliss.Domain.Financials;
using Npgsql;
namespace Bliss.Infrastructure.Financials;
public sealed record ReservedRefund(Guid Id,Guid OriginalJournalId,string PaymentIntentId,long AmountMinor);
// Trusted internal/test gateway only. A real signed refund-event adapter is required before external activation.
public sealed class RefundReconciliationStore(NpgsqlDataSource data)
{
 private static string Operator(ClaimsPrincipal actor)
 {var subject=PaymentOperationStore.AuthenticatedSubject(actor);if(!actor.IsInRole("bliss.operator")&&!actor.IsInRole("bliss.admin"))throw new UnauthorizedAccessException("Financial operator required.");return subject;}
 public async Task<ReservedRefund> Reserve(ClaimsPrincipal actor,FinancialScope scope,string paymentIntent,long amount,string key,CancellationToken ct=default)
 {
  var subject=Operator(actor);if(amount<=0||string.IsNullOrWhiteSpace(key)||key.Length>120)throw new ArgumentException("Positive amount and bounded key required.");
  await using var connection=await data.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(ct);
  await using var source=new NpgsqlCommand("SELECT j.id FROM financials.stripe_receipts r JOIN financials.journals j ON j.id=r.journal_id WHERE r.payment_intent_id=@p AND j.merchant_id=@m AND j.module=@o AND j.campaign_id=@c AND j.currency='USD' FOR UPDATE OF r",connection,tx);source.Parameters.AddWithValue("p",paymentIntent);source.Parameters.AddWithValue("m",scope.MerchantId);source.Parameters.AddWithValue("o",scope.Module.ToString());source.Parameters.AddWithValue("c",scope.CampaignId);
  var original=await source.ExecuteScalarAsync(ct);if(original is not Guid journal)throw new UnauthorizedAccessException("Payment unavailable within trusted scope.");
  await using(var replay=new NpgsqlCommand("SELECT id,amount_minor FROM financials.refund_requests WHERE payment_intent_id=@p AND retry_key=@k",connection,tx)){replay.Parameters.AddWithValue("p",paymentIntent);replay.Parameters.AddWithValue("k",key);await using var reader=await replay.ExecuteReaderAsync(ct);if(await reader.ReadAsync(ct)){if(reader.GetInt64(1)!=amount)throw new InvalidOperationException("Refund retry conflicts with reserved amount.");var id=reader.GetGuid(0);return new(id,journal,paymentIntent,amount);}}
  var lines=await Lines(connection,tx,journal,ct);if(lines.Length!=2||lines.Count(x=>x.Side==JournalSide.Debit)!=1||lines.Count(x=>x.Side==JournalSide.Credit)!=1)throw new InvalidOperationException("Multi-beneficiary refunds require an approved allocation policy.");
  var gross=lines.Single(x=>x.Side==JournalSide.Debit).MinorUnits;
  await using var used=new NpgsqlCommand("SELECT coalesce(sum(amount_minor),0) FROM financials.refund_requests q WHERE payment_intent_id=@p AND NOT EXISTS(SELECT 1 FROM financials.refund_results r WHERE r.request_id=q.id AND r.status='FAILED')",connection,tx);used.Parameters.AddWithValue("p",paymentIntent);var reserved=Convert.ToDecimal(await used.ExecuteScalarAsync(ct));if(reserved+amount>gross)throw new InvalidOperationException("Refund exceeds unreserved captured amount.");
  var refund=Guid.NewGuid();await using var insert=new NpgsqlCommand("INSERT INTO financials.refund_requests(id,payment_intent_id,journal_id,amount_minor,retry_key,actor_subject) VALUES(@i,@p,@j,@a,@k,@s)",connection,tx);insert.Parameters.AddWithValue("i",refund);insert.Parameters.AddWithValue("p",paymentIntent);insert.Parameters.AddWithValue("j",journal);insert.Parameters.AddWithValue("a",amount);insert.Parameters.AddWithValue("k",key);insert.Parameters.AddWithValue("s",subject);await insert.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return new(refund,journal,paymentIntent,amount);
 }
 public async Task<Guid?> Reconcile(ClaimsPrincipal actor,FinancialScope expectedScope,Guid refundId,string providerRefundId,string status,CancellationToken ct=default)
 {
  Operator(actor);if(status is not ("PENDING" or "SUCCEEDED" or "FAILED")||!providerRefundId.StartsWith("re_",StringComparison.Ordinal)||providerRefundId.Length>120)throw new ArgumentException("Verified refund identity and status required.");
  await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
  await using var lookup=new NpgsqlCommand("SELECT q.journal_id,q.amount_minor,j.merchant_id,j.module,j.campaign_id FROM financials.refund_requests q JOIN financials.journals j ON j.id=q.journal_id WHERE q.id=@i AND j.merchant_id=@m AND j.module=@o AND j.campaign_id=@c FOR UPDATE OF q",c,tx);lookup.Parameters.AddWithValue("i",refundId);lookup.Parameters.AddWithValue("m",expectedScope.MerchantId);lookup.Parameters.AddWithValue("o",expectedScope.Module.ToString());lookup.Parameters.AddWithValue("c",expectedScope.CampaignId);Guid original;long amount;FinancialScope scope;
  await using(var reader=await lookup.ExecuteReaderAsync(ct)){if(!await reader.ReadAsync(ct))throw new InvalidOperationException("Unknown reserved refund.");original=reader.GetGuid(0);amount=reader.GetInt64(1);scope=new(reader.GetGuid(2),Enum.Parse<FinancialModule>(reader.GetString(3)),reader.GetGuid(4));}
  await using(var bind=new NpgsqlCommand("INSERT INTO financials.refund_provider_ids(provider_refund_id,request_id) VALUES(@p,@i) ON CONFLICT DO NOTHING",c,tx)){bind.Parameters.AddWithValue("p",providerRefundId);bind.Parameters.AddWithValue("i",refundId);await bind.ExecuteNonQueryAsync(ct);}
  await using(var binding=new NpgsqlCommand("SELECT request_id FROM financials.refund_provider_ids WHERE provider_refund_id=@p",c,tx)){binding.Parameters.AddWithValue("p",providerRefundId);if(await binding.ExecuteScalarAsync(ct) is not Guid bound||bound!=refundId)throw new InvalidOperationException("Provider refund is bound to another reservation.");}
  await using(var prior=new NpgsqlCommand("SELECT provider_refund_id,status,refund_journal_id FROM financials.refund_results WHERE request_id=@i",c,tx)){prior.Parameters.AddWithValue("i",refundId);await using var reader=await prior.ExecuteReaderAsync(ct);while(await reader.ReadAsync(ct)){if(reader.GetString(0)!=providerRefundId)throw new InvalidOperationException("Refund provider identity changed.");if(reader.GetString(1)==status)return reader.IsDBNull(2)?null:reader.GetGuid(2);if(reader.GetString(1) is "SUCCEEDED" or "FAILED")throw new InvalidOperationException("Terminal refund cannot be overwritten.");}}
  Guid? reversal=null;if(status=="SUCCEEDED"){
   var lines=await Lines(c,tx,original,ct);var journal=new FinancialJournal(scope,"USD","refund-"+refundId.ToString("N"),"TEST_REFUND:"+refundId+":"+original,lines.Select(x=>x with{Side=x.Side==JournalSide.Debit?JournalSide.Credit:JournalSide.Debit,MinorUnits=amount}));
   reversal=await new PostgresJournalStore(data).AppendWithin(c,tx,journal,null,ct);
  }
  await using var insert=new NpgsqlCommand("INSERT INTO financials.refund_results(request_id,provider_refund_id,status,refund_journal_id) VALUES(@i,@p,@s,@j)",c,tx);insert.Parameters.AddWithValue("i",refundId);insert.Parameters.AddWithValue("p",providerRefundId);insert.Parameters.AddWithValue("s",status);insert.Parameters.AddWithValue("j",NpgsqlTypes.NpgsqlDbType.Uuid,(object?)reversal??DBNull.Value);await insert.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return reversal;
 }
 private static async Task<JournalLine[]> Lines(NpgsqlConnection c,NpgsqlTransaction tx,Guid journal,CancellationToken ct)
 {await using var q=new NpgsqlCommand("SELECT account_id,owner_id,role,side,minor_units FROM financials.lines WHERE journal_id=@j ORDER BY ordinal",c,tx);q.Parameters.AddWithValue("j",journal);await using var r=await q.ExecuteReaderAsync(ct);var lines=new List<JournalLine>();while(await r.ReadAsync(ct))lines.Add(new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),Enum.Parse<JournalSide>(r.GetString(3)),r.GetInt64(4)));return lines.ToArray();}
}
