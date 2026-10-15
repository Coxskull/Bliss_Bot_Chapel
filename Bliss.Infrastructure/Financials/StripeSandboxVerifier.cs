using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Financials;
namespace Bliss.Infrastructure.Financials;

public sealed record ExpectedSandboxPayment(string PaymentIntentId, FinancialScope Scope, string Currency, long AmountMinor);
public sealed record VerifiedSandboxPaymentStatus(string EventId,string PaymentIntentId,string State);
public sealed record VerifiedSandboxPayment(string EventId, string PaymentIntentId, long AmountMinor, string Currency);

// Raw-body verifier only. Endpoint authentication/quote binding/event persistence belong to the adapter integration.
public static class StripeSandboxVerifier
{
 public static VerifiedSandboxPayment VerifyPayment(byte[] body, string signature, string webhookSecret,
     DateTimeOffset now, ExpectedSandboxPayment expected)
 {
  using var document=VerifyEnvelope(body,signature,webhookSecret,now);var e=document.RootElement;
  if(e.GetProperty("type").GetString()!="payment_intent.succeeded") throw new ArgumentException("Unsupported financial event.");
  var id=e.GetProperty("id").GetString();var o=e.GetProperty("data").GetProperty("object");var pi=o.GetProperty("id").GetString();
  if(string.IsNullOrWhiteSpace(id)||!id.StartsWith("evt_",StringComparison.Ordinal)||o.GetProperty("object").GetString()!="payment_intent" ||o.GetProperty("livemode").GetBoolean()||o.GetProperty("status").GetString()!="succeeded"||pi!=expected.PaymentIntentId) throw new ArgumentException("Event/payment identity mismatch.");
  var currency=o.GetProperty("currency").GetString();var amount=o.GetProperty("amount_received").GetInt64();var metadata=o.GetProperty("metadata");
  if(expected.AmountMinor<=0 || amount!=expected.AmountMinor ||o.GetProperty("amount").GetInt64()!=expected.AmountMinor||currency!=expected.Currency.ToLowerInvariant() ||
    metadata.GetProperty("merchant_id").GetString()!=expected.Scope.MerchantId.ToString()||metadata.GetProperty("campaign_id").GetString()!=expected.Scope.CampaignId.ToString()||metadata.GetProperty("module").GetString()!=expected.Scope.Module.ToString()) throw new ArgumentException("Authoritative amount/currency/scope mismatch.");
  return new(id!,pi!,amount,expected.Currency);
 }
 public static VerifiedSandboxPaymentStatus VerifyStatus(byte[] body,string signature,string secret,DateTimeOffset now,ExpectedSandboxPayment expected)
 {
  using var document=VerifyEnvelope(body,signature,secret,now);var e=document.RootElement;var o=e.GetProperty("data").GetProperty("object");
  var state=e.GetProperty("type").GetString() switch {"payment_intent.payment_failed"=>"FAILED","payment_intent.canceled"=>"CANCELED","payment_intent.processing"=>"PROCESSING",_=>throw new ArgumentException("Unsupported mock status event.")};
  var expectedStatus=state switch {"FAILED"=>"requires_payment_method","CANCELED"=>"canceled",_=>"processing"};var id=e.GetProperty("id").GetString();var metadata=o.GetProperty("metadata");
  if(string.IsNullOrWhiteSpace(id)||!id.StartsWith("evt_",StringComparison.Ordinal)||o.GetProperty("id").GetString()!=expected.PaymentIntentId||o.GetProperty("object").GetString()!="payment_intent"||o.GetProperty("livemode").GetBoolean()||o.GetProperty("status").GetString()!=expectedStatus||expected.AmountMinor<=0||o.GetProperty("amount").GetInt64()!=expected.AmountMinor||o.GetProperty("amount_received").GetInt64()!=0||o.GetProperty("currency").GetString()!=expected.Currency.ToLowerInvariant()||metadata.GetProperty("merchant_id").GetString()!=expected.Scope.MerchantId.ToString()||metadata.GetProperty("campaign_id").GetString()!=expected.Scope.CampaignId.ToString()||metadata.GetProperty("module").GetString()!=expected.Scope.Module.ToString())throw new ArgumentException("Mock status does not match funding authority.");
  return new(id!,expected.PaymentIntentId,state);
 }
 private static JsonDocument VerifyEnvelope(byte[] body,string signature,string webhookSecret,DateTimeOffset now)
 {
  if (body is null || body.Length > 1_000_000 || string.IsNullOrWhiteSpace(signature) || signature.Length > 4096 || string.IsNullOrWhiteSpace(webhookSecret)) throw new ArgumentException("Bounded body and signing secret required.");
  var parts=signature.Split(',').Select(x=>x.Trim().Split('=',2)).ToArray();
  if(parts.Any(x=>x.Length!=2 || string.IsNullOrWhiteSpace(x[0]) || string.IsNullOrWhiteSpace(x[1])))throw new ArgumentException("Malformed webhook signature header.");
  var timestamps=parts.Where(x=>x[0]=="t").Select(x=>x[1]).ToArray();
  if(timestamps.Length!=1 || parts.Count(x=>x[0]=="v1") is < 1 or > 8 || !long.TryParse(timestamps[0],NumberStyles.None,CultureInfo.InvariantCulture,out var timestamp)) throw new ArgumentException("Invalid signature timestamp.");
  var age=(decimal)now.ToUnixTimeSeconds()-timestamp;
  if(age< -300 || age>300) throw new ArgumentException("Expired signature.");
  var prefix=Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture)+".");
  var content=new byte[prefix.Length+body.Length];prefix.CopyTo(content,0);body.CopyTo(content,prefix.Length);
  var actual=HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret),content);
  var valid=false;
  foreach(var h in parts.Where(x=>x[0]=="v1"))
  {try {var candidate=Convert.FromHexString(h[1]);valid|=candidate.Length==actual.Length&&CryptographicOperations.FixedTimeEquals(candidate,actual);}catch(FormatException){}}
  if(!valid) throw new ArgumentException("Invalid webhook signature.");
  var document=JsonDocument.Parse(body);var e=document.RootElement;
  try {if(e.GetProperty("livemode").GetBoolean() || e.TryGetProperty("account",out _))throw new ArgumentException("Only platform sandbox events are supported; Connect is not authorized.");return document;}
  catch {document.Dispose();throw;}
 }

}
