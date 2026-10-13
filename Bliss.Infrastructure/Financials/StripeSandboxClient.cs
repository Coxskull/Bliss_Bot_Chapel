using System.Net.Http.Headers;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Bliss.Domain.Financials;
namespace Bliss.Infrastructure.Financials;

public sealed record SandboxChargeRequest(FinancialScope Scope, string FundingReference, string Currency, long AmountMinor);
public sealed record SandboxPaymentIntent(string Id, string Status, long AmountMinor, string Currency);

// Inactive adapter primitive. Inputs must come from an authorized persisted quote/operation, not a browser DTO.
public sealed class StripeSandboxClient
{
 private readonly HttpClient http;
 private readonly string secret;
 public StripeSandboxClient(HttpClient http, string testSecret)
 {
  this.http=http ?? throw new ArgumentNullException(nameof(http));
  if(string.IsNullOrWhiteSpace(testSecret)||!testSecret.StartsWith("sk_test_",StringComparison.Ordinal)) throw new ArgumentException("Test-mode secret required.");
  secret=testSecret;
 }
 public async Task<SandboxPaymentIntent> CreatePaymentIntent(SandboxChargeRequest charge, string operationKey, CancellationToken token=default)
 {
  if(charge.Scope.MerchantId==Guid.Empty||charge.Scope.CampaignId==Guid.Empty||!Enum.IsDefined(charge.Scope.Module)||charge.AmountMinor<=0||charge.Currency != "USD"||string.IsNullOrWhiteSpace(charge.FundingReference)||charge.FundingReference.Length>120||string.IsNullOrWhiteSpace(operationKey)||operationKey.Length>120||operationKey.Any(char.IsControl))throw new ArgumentException("Validated quote scope, amount, currency and operation key required.");
  using var request=new HttpRequestMessage(HttpMethod.Post,"https://api.stripe.com/v1/payment_intents");
  request.Headers.Authorization=new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(secret+":")));
  var scopedKey="agm-"+Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{charge.Scope,charge.FundingReference,operationKey})));
  request.Headers.Add("Idempotency-Key",scopedKey);
  request.Headers.Add("Stripe-Version","2024-06-20");
  request.Content=new FormUrlEncodedContent(new Dictionary<string,string>{["amount"]=charge.AmountMinor.ToString(System.Globalization.CultureInfo.InvariantCulture),["currency"]=charge.Currency.ToLowerInvariant(),["automatic_payment_methods[enabled]"]="true",["metadata[merchant_id]"]=charge.Scope.MerchantId.ToString(),["metadata[campaign_id]"]=charge.Scope.CampaignId.ToString(),["metadata[module]"]=charge.Scope.Module.ToString(),["metadata[funding_reference]"]=charge.FundingReference});
  using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Sandbox provider refused request; reconcile operation before retry.");
  await using var stream=await response.Content.ReadAsStreamAsync(token);
  // Bounded response prevents a provider/proxy response from consuming unbounded memory.
  var buffer=new byte[65537];var length=0;
  while(length<buffer.Length){var n=await stream.ReadAsync(buffer.AsMemory(length),token);if(n==0)break;length+=n;}
  if(length>65536)throw new InvalidOperationException("Sandbox provider response exceeds limit.");
  using var document=JsonDocument.Parse(buffer.AsMemory(0,length));var o=document.RootElement;
  var id=o.GetProperty("id").GetString();var status=o.GetProperty("status").GetString();var metadata=o.GetProperty("metadata");
  if(o.GetProperty("object").GetString()!="payment_intent"||o.GetProperty("livemode").GetBoolean()||string.IsNullOrWhiteSpace(id)||!id.StartsWith("pi_",StringComparison.Ordinal)||
   status is not ("requires_payment_method" or "requires_confirmation" or "requires_action" or "processing" or "requires_capture" or "canceled" or "succeeded")||o.GetProperty("amount").GetInt64()!=charge.AmountMinor||o.GetProperty("currency").GetString()!=charge.Currency.ToLowerInvariant()||
   metadata.GetProperty("merchant_id").GetString()!=charge.Scope.MerchantId.ToString()||metadata.GetProperty("campaign_id").GetString()!=charge.Scope.CampaignId.ToString()||metadata.GetProperty("module").GetString()!=charge.Scope.Module.ToString()||metadata.GetProperty("funding_reference").GetString()!=charge.FundingReference)
   throw new InvalidOperationException("Sandbox payment response does not match authorized operation.");
  // Even succeeded response does not book cash; signed authoritative events/reconciliation must do so.
  return new(id!,status!,charge.AmountMinor,charge.Currency);
 }
}
