using Bliss.Domain.Financials;
using Npgsql;
namespace Bliss.Infrastructure.Financials;
// No real provider dispatch. Quote authority must be revalidated by the mock application adapter.
public sealed class MockPaymentCaptureStore(NpgsqlDataSource data)
{
 public async Task<PreparedPaymentOperation> Operation(Guid operationId,CancellationToken ct=default)
 {
  await using var c=data.CreateCommand("SELECT o.id,q.id,q.merchant_id,q.module,q.campaign_id,q.recipient_account,q.amount_minor,q.currency,o.provider_key FROM financials.payment_operations o JOIN financials.authorized_quotes q ON q.id=o.quote_id WHERE o.id=@i");c.Parameters.AddWithValue("i",operationId);await using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Unknown funding operation.");return new(r.GetGuid(0),r.GetGuid(1),new(r.GetGuid(2),Enum.Parse<FinancialModule>(r.GetString(3)),r.GetGuid(4)),r.GetGuid(5),r.GetInt64(6),r.GetString(7),r.GetString(8));
 }
 public async Task<Guid> Capture(PreparedPaymentOperation operation,VerifiedSandboxPayment payment,Guid payerAccount,Guid recipientOwner,CancellationToken ct=default)
 {
  if(payerAccount==Guid.Empty||recipientOwner==Guid.Empty||payment.PaymentIntentId!="pi_mock_"+operation.Id.ToString("N")||payment.Currency!="USD"||payment.AmountMinor!=operation.AmountMinor)throw new ArgumentException("Mock capture must match trusted operation/accounts.");
  await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
  await using(var locked=new NpgsqlCommand("SELECT q.id,q.merchant_id,q.module,q.campaign_id,q.recipient_account,q.amount_minor,q.currency,o.provider_key FROM financials.payment_operations o JOIN financials.authorized_quotes q ON q.id=o.quote_id WHERE o.id=@i FOR UPDATE OF o",c,tx)){locked.Parameters.AddWithValue("i",operation.Id);await using var reader=await locked.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct)||reader.GetGuid(0)!=operation.QuoteId||reader.GetGuid(1)!=operation.Scope.MerchantId||reader.GetString(2)!=operation.Scope.Module.ToString()||reader.GetGuid(3)!=operation.Scope.CampaignId||reader.GetGuid(4)!=operation.RecipientAccountId||reader.GetInt64(5)!=operation.AmountMinor||reader.GetString(6)!=operation.Currency||reader.GetString(7)!=operation.ProviderKey)throw new InvalidOperationException("Capture does not match immutable funding operation.");}
  // The caller receives an immutable store-loaded operation, and cannot supply recipients in the public DTO.
  var journal=new FinancialJournal(operation.Scope,"USD","capture-"+operation.Id.ToString("N"),"TEST_CAPTURE:"+operation.Id,[new(payerAccount,operation.Scope.MerchantId,"TEST_CASH",JournalSide.Debit,operation.AmountMinor),new(operation.RecipientAccountId,recipientOwner,"TEST_RECIPIENT",JournalSide.Credit,operation.AmountMinor)]);
  var id=await new PostgresJournalStore(data).AppendWithin(c,tx,journal,payment,ct);
  await using var record=new NpgsqlCommand("INSERT INTO financials.payment_captures(operation_id,journal_id,payment_intent_id) VALUES(@i,@j,@p) ON CONFLICT DO NOTHING",c,tx);record.Parameters.AddWithValue("i",operation.Id);record.Parameters.AddWithValue("j",id);record.Parameters.AddWithValue("p",payment.PaymentIntentId);await record.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);return id;
 }
 public async Task<Guid?> Captured(Guid operation,CancellationToken ct=default){await using var c=data.CreateCommand("SELECT journal_id FROM financials.payment_captures WHERE operation_id=@i");c.Parameters.AddWithValue("i",operation);return await c.ExecuteScalarAsync(ct) is Guid id?id:null;}
 public async Task<Guid> OperationForRefund(Guid refund,CancellationToken ct=default){await using var c=data.CreateCommand("SELECT c.operation_id FROM financials.refund_requests r JOIN financials.payment_captures c ON c.payment_intent_id=r.payment_intent_id WHERE r.id=@i");c.Parameters.AddWithValue("i",refund);return await c.ExecuteScalarAsync(ct) is Guid id?id:throw new InvalidOperationException("Unknown mock refund.");}
 public async Task<PreparedPaymentOperation[]> Owned(string subject,FinancialModule module,CancellationToken ct=default)
 {await using var c=data.CreateCommand("SELECT o.id FROM financials.payment_operations o JOIN financials.authorized_quotes q ON q.id=o.quote_id WHERE q.advertiser_subject=@s AND q.module=@m ORDER BY o.recorded_at LIMIT 100");c.Parameters.AddWithValue("s",subject);c.Parameters.AddWithValue("m",module.ToString());var ids=new List<Guid>();await using(var r=await c.ExecuteReaderAsync(ct)){while(await r.ReadAsync(ct))ids.Add(r.GetGuid(0));}var result=new List<PreparedPaymentOperation>();foreach(var id in ids)result.Add(await Operation(id,ct));return result.ToArray();}
}
