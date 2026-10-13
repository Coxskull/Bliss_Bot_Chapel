using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Financials;
namespace Bliss.Infrastructure.Financials;

public sealed record ExpectedSandboxPayment(string PaymentIntentId, FinancialScope Scope, string Currency, long AmountMinor);
public sealed record VerifiedSandboxPayment(string EventId, string PaymentIntentId, long AmountMinor, string Currency);

// Raw-body verifier only. Endpoint authentication/quote binding/event persistence belong to the adapter integration.
public static class StripeSandboxVerifier
{
 public static VerifiedSandboxPayment VerifyPayment(byte[] body, string signature, string webhookSecret,
     DateTimeOffset now, ExpectedSandboxPayment expected)
 {
  if (body.Length > 1_000_000 || string.IsNullOrWhiteSpace(webhookSecret)) throw new ArgumentException("Bounded body and signing secret required.");
  var parts=signature.Split(',').Select(x=>x.Split('=',2)).Where(x=>x.Length==2).ToArray();
  var timestamps=parts.Where(x=>x[0]=="t").Select(x=>x[1]).ToArray();
  if(timestamps.Length!=1 || !long.TryParse(timestamps[0],NumberStyles.None,CultureInfo.InvariantCulture,out var timestamp)) throw new ArgumentException("Invalid signature timestamp.");
  var age=(decimal)now.ToUnixTimeSeconds()-timestamp;
  if(age< -300 || age>300) throw new ArgumentException("Expired signature.");
  var prefix=Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture)+".");
  var content=new byte[prefix.Length+body.Length];prefix.CopyTo(content,0);body.CopyTo(content,prefix.Length);
  var actual=HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret),content);
  var valid=false;
  foreach(var h in parts.Where(x=>x[0]=="v1"))
  {try {var candidate=Convert.FromHexString(h[1]);valid|=candidate.Length==actual.Length&&CryptographicOperations.FixedTimeEquals(candidate,actual);}catch(FormatException){}}
  if(!valid) throw new ArgumentException("Invalid webhook signature.");
  using var document=JsonDocument.Parse(body);var e=document.RootElement;
  if(e.GetProperty("livemode").GetBoolean() || e.TryGetProperty("account",out _)) throw new ArgumentException("Only platform sandbox events are supported; Connect is not authorized.");
  if(e.GetProperty("type").GetString()!="payment_intent.succeeded") throw new ArgumentException("Unsupported financial event.");
  var id=e.GetProperty("id").GetString();var o=e.GetProperty("data").GetProperty("object");var pi=o.GetProperty("id").GetString();
  if(string.IsNullOrWhiteSpace(id)||!id.StartsWith("evt_",StringComparison.Ordinal)||o.GetProperty("object").GetString()!="payment_intent" ||o.GetProperty("livemode").GetBoolean()||o.GetProperty("status").GetString()!="succeeded"||pi!=expected.PaymentIntentId) throw new ArgumentException("Event/payment identity mismatch.");
  var currency=o.GetProperty("currency").GetString();var amount=o.GetProperty("amount_received").GetInt64();var metadata=o.GetProperty("metadata");
  if(expected.AmountMinor<=0 || amount!=expected.AmountMinor ||o.GetProperty("amount").GetInt64()!=expected.AmountMinor||currency!=expected.Currency.ToLowerInvariant() ||
    metadata.GetProperty("merchant_id").GetString()!=expected.Scope.MerchantId.ToString()||metadata.GetProperty("campaign_id").GetString()!=expected.Scope.CampaignId.ToString()||metadata.GetProperty("module").GetString()!=expected.Scope.Module.ToString()) throw new ArgumentException("Authoritative amount/currency/scope mismatch.");
  return new(id!,pi!,amount,expected.Currency);
 }
}
