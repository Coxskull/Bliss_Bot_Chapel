using System.Security.Claims;
using Bliss.Domain.Financials;
using Npgsql;
namespace Bliss.Infrastructure.Financials;

// Created by a trusted accepted-quote adapter only; never bind this record from a payment request.
public sealed record AuthorizedQuoteSnapshot(Guid Id, Guid SourceQuoteVersionId, FinancialScope Scope,
 string AdvertiserSubject, Guid RecipientAccountId, long AmountMinor, string Currency);
public sealed record PreparedPaymentOperation(Guid Id, Guid QuoteId, FinancialScope Scope,
 Guid RecipientAccountId, long AmountMinor, string Currency, string ProviderKey);
public sealed class PaymentOperationStore(NpgsqlDataSource data)
{
 public static string AuthenticatedSubject(ClaimsPrincipal principal)
 {
  var subjects=principal.Identities.Where(x=>x.IsAuthenticated).SelectMany(x=>x.Claims).Where(x=>x.Type==ClaimTypes.NameIdentifier||x.Type=="sub").Select(x=>x.Value).Distinct().ToArray();
  if(subjects.Length!=1||string.IsNullOrWhiteSpace(subjects[0]))throw new UnauthorizedAccessException("Authenticated stable subject required.");return subjects[0];
 }
 public async Task FreezeQuote(AuthorizedQuoteSnapshot quote, CancellationToken ct=default)
 {
  if(quote.Id==Guid.Empty||quote.SourceQuoteVersionId==Guid.Empty||quote.Scope.MerchantId==Guid.Empty||quote.Scope.CampaignId==Guid.Empty||!Enum.IsDefined(quote.Scope.Module)||quote.RecipientAccountId==Guid.Empty||quote.AmountMinor<=0||quote.Currency!="USD"||string.IsNullOrWhiteSpace(quote.AdvertiserSubject)||quote.AdvertiserSubject.Length>200)throw new ArgumentException("Authorized USD quote and recipient binding required.");
  await using var c=data.CreateCommand("""
   INSERT INTO financials.authorized_quotes(id,source_version,merchant_id,module,campaign_id,advertiser_subject,recipient_account,amount_minor,currency)
   VALUES(@i,@v,@m,@o,@c,@s,@r,@a,@u) ON CONFLICT DO NOTHING RETURNING id
   """);
  c.Parameters.AddWithValue("i",quote.Id);c.Parameters.AddWithValue("v",quote.SourceQuoteVersionId);c.Parameters.AddWithValue("m",quote.Scope.MerchantId);c.Parameters.AddWithValue("o",quote.Scope.Module.ToString());c.Parameters.AddWithValue("c",quote.Scope.CampaignId);c.Parameters.AddWithValue("s",quote.AdvertiserSubject);c.Parameters.AddWithValue("r",quote.RecipientAccountId);c.Parameters.AddWithValue("a",quote.AmountMinor);c.Parameters.AddWithValue("u",quote.Currency);if(await c.ExecuteScalarAsync(ct) is null)
  {
   await using var existing=data.CreateCommand("SELECT source_version,merchant_id,module,campaign_id,advertiser_subject,recipient_account,amount_minor,currency FROM financials.authorized_quotes WHERE id=@i");existing.Parameters.AddWithValue("i",quote.Id);
   await using var reader=await existing.ExecuteReaderAsync(ct);
   if(!await reader.ReadAsync(ct)||reader.GetGuid(0)!=quote.SourceQuoteVersionId||reader.GetGuid(1)!=quote.Scope.MerchantId||reader.GetString(2)!=quote.Scope.Module.ToString()||reader.GetGuid(3)!=quote.Scope.CampaignId||reader.GetString(4)!=quote.AdvertiserSubject||reader.GetGuid(5)!=quote.RecipientAccountId||reader.GetInt64(6)!=quote.AmountMinor||reader.GetString(7)!=quote.Currency)throw new InvalidOperationException("Frozen quote conflicts with original evidence.");
  }
 }
 public async Task<PreparedPaymentOperation> Prepare(ClaimsPrincipal principal, Guid quoteId, string retryKey, CancellationToken ct=default)
 {
  var subject=AuthenticatedSubject(principal);
  if(quoteId==Guid.Empty||string.IsNullOrWhiteSpace(retryKey)||retryKey.Length>120||retryKey.Any(char.IsControl))throw new ArgumentException("Quote reference and bounded retry key required.");
  await using var connection=await data.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(ct);
  await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@k,0))",connection,tx)){gate.Parameters.AddWithValue("k",subject+":"+retryKey);await gate.ExecuteNonQueryAsync(ct);}
  await using(var alias=new NpgsqlCommand("SELECT o.quote_id FROM financials.payment_retry_keys k JOIN financials.payment_operations o ON o.id=k.operation_id WHERE k.advertiser_subject=@s AND k.retry_key=@k",connection,tx)){alias.Parameters.AddWithValue("s",subject);alias.Parameters.AddWithValue("k",retryKey);var prior=await alias.ExecuteScalarAsync(ct);if(prior is Guid id && id!=quoteId)throw new InvalidOperationException("Retry key conflicts with original quote.");}
  // One frozen quote => one funding operation across all client retries, never a client-controlled amount/payee.
  await using var read=new NpgsqlCommand("SELECT merchant_id,module,campaign_id,recipient_account,amount_minor,currency FROM financials.authorized_quotes WHERE id=@i AND advertiser_subject=@s FOR UPDATE",connection,tx);
  read.Parameters.AddWithValue("i",quoteId);read.Parameters.AddWithValue("s",subject);
  PreparedPaymentOperation snapshot;
  await using(var reader=await read.ExecuteReaderAsync(ct))
  {
   if(!await reader.ReadAsync(ct))throw new UnauthorizedAccessException("Quote unavailable to authenticated principal.");
   var id=Guid.NewGuid();snapshot=new(id,quoteId,new(reader.GetGuid(0),Enum.Parse<FinancialModule>(reader.GetString(1)),reader.GetGuid(2)),reader.GetGuid(3),reader.GetInt64(4),reader.GetString(5),"agm-payment-"+id.ToString("N"));
  }
  await using var insert=new NpgsqlCommand("INSERT INTO financials.payment_operations(id,quote_id,advertiser_subject,retry_key,provider_key) VALUES(@i,@q,@s,@k,@p) ON CONFLICT DO NOTHING RETURNING id",connection,tx);
  insert.Parameters.AddWithValue("i",snapshot.Id);insert.Parameters.AddWithValue("q",quoteId);insert.Parameters.AddWithValue("s",subject);insert.Parameters.AddWithValue("k",retryKey);insert.Parameters.AddWithValue("p",snapshot.ProviderKey);
  if(await insert.ExecuteScalarAsync(ct) is null)
  {
   await using var existing=new NpgsqlCommand("SELECT id,quote_id,provider_key FROM financials.payment_operations WHERE advertiser_subject=@s AND quote_id=@q",connection,tx);existing.Parameters.AddWithValue("s",subject);existing.Parameters.AddWithValue("k",retryKey);existing.Parameters.AddWithValue("q",quoteId);
   await using var reader=await existing.ExecuteReaderAsync(ct);if(!await reader.ReadAsync(ct)||reader.GetGuid(1)!=quoteId)throw new InvalidOperationException("Retry key conflicts with original quote.");snapshot=snapshot with{Id=reader.GetGuid(0),ProviderKey=reader.GetString(2)};
   // A key bound to another quote must not be silently hidden by a second matching quote row.
   if(await reader.ReadAsync(ct))throw new InvalidOperationException("Retry key conflicts with another operation.");
  }
  await using(var alias=new NpgsqlCommand("INSERT INTO financials.payment_retry_keys(advertiser_subject,retry_key,operation_id) VALUES(@s,@k,@i) ON CONFLICT DO NOTHING",connection,tx)){alias.Parameters.AddWithValue("s",subject);alias.Parameters.AddWithValue("k",retryKey);alias.Parameters.AddWithValue("i",snapshot.Id);await alias.ExecuteNonQueryAsync(ct);}
  await tx.CommitAsync(ct);return snapshot;
 }
}
