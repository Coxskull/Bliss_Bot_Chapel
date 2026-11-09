using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
namespace Bliss.Tests.Financials;
public sealed class StripeMockStatusVerifierTests
{
 private static readonly ExpectedSandboxPayment Expected=new("pi_fixture_status",new(Guid.NewGuid(),FinancialModule.BlissBot,Guid.NewGuid()),"USD",500);
 private static JsonObject Body(string type,string status)=>new(){["id"]="evt_fixture_status",["type"]=type,["livemode"]=false,["data"]=new JsonObject{["object"]=new JsonObject{["id"]=Expected.PaymentIntentId,["object"]="payment_intent",["livemode"]=false,["status"]=status,["amount"]=500,["amount_received"]=0,["currency"]="usd",["metadata"]=new JsonObject{["merchant_id"]=Expected.Scope.MerchantId.ToString(),["campaign_id"]=Expected.Scope.CampaignId.ToString(),["module"]=Expected.Scope.Module.ToString()}}}};
 private static VerifiedSandboxPaymentStatus Verify(JsonObject body,bool invalidSignature=false)
 {var bytes=Encoding.UTF8.GetBytes(body.ToJsonString());var now=DateTimeOffset.UtcNow;var t=now.ToUnixTimeSeconds();var signature="t="+t+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(invalidSignature?"wrong":"whsec_fixture_status"),Encoding.UTF8.GetBytes(t+"."+body.ToJsonString())));return StripeSandboxVerifier.VerifyStatus(bytes,signature,"whsec_fixture_status",now,Expected);}
 [Theory][InlineData("payment_intent.payment_failed","requires_payment_method","FAILED")][InlineData("payment_intent.canceled","canceled","CANCELED")][InlineData("payment_intent.processing","processing","PROCESSING")]
 public void Signed_non_cash_types_have_explicit_matching_status(string type,string status,string state)=>Assert.Equal(state,Verify(Body(type,status)).State);
 [Theory][InlineData("live")][InlineData("amount")][InlineData("status")][InlineData("currency")][InlineData("signature")]
 public void Wrong_mode_amount_status_currency_or_signature_is_refused(string change)
 {var body=Body("payment_intent.payment_failed","requires_payment_method");var o=body["data"]!["object"]!;if(change=="live")body["livemode"]=true;if(change=="amount")o["amount"]=499;if(change=="status")o["status"]="succeeded";if(change=="currency")o["currency"]="php";Assert.Throws<ArgumentException>(()=>Verify(body,change=="signature"));}

 // ALPHA-472: a browser Back/Cancel or an expired hosted Checkout Session is
 // not itself proof that Stripe canceled the PaymentIntent or refunded cash.
 // Keep the authoritative PaymentIntent event types strictly separate.
 [Theory]
 [InlineData("payment_intent.payment_failed","succeeded")]
 [InlineData("payment_intent.payment_failed","canceled")]
 [InlineData("payment_intent.canceled","requires_payment_method")]
 [InlineData("payment_intent.canceled","succeeded")]
 [InlineData("payment_intent.processing","requires_payment_method")]
 [InlineData("checkout.session.expired","expired")]
 [InlineData("checkout.session.completed","complete")]
 public void Mismatched_or_checkout_session_events_cannot_be_booked_as_payment_status(string type,string status)
 {
  var body=Body(type,status);
  Assert.Throws<ArgumentException>(()=>Verify(body));
 }

 [Theory]
 [InlineData("different_intent")]
 [InlineData("different_merchant")]
 [InlineData("different_campaign")]
 [InlineData("different_module")]
 [InlineData("already_received_money")]
 [InlineData("live_inner_object")]
 [InlineData("invalid_event_id")]
 [InlineData("connect_account")]
 public void Failed_status_rejects_cross_scope_live_or_already_collected_money(string change)
 {
  var body=Body("payment_intent.payment_failed","requires_payment_method");
  var payment=body["data"]!["object"]!;
  switch(change)
  {
   case "different_intent":payment["id"]="pi_other";break;
   case "different_merchant":payment["metadata"]!["merchant_id"]=Guid.NewGuid().ToString();break;
   case "different_campaign":payment["metadata"]!["campaign_id"]=Guid.NewGuid().ToString();break;
   case "different_module":payment["metadata"]!["module"]="WeddingPlanner";break;
   case "already_received_money":payment["amount_received"]=1;break;
   case "live_inner_object":payment["livemode"]=true;break;
   case "invalid_event_id":body["id"]="not_a_stripe_event";break;
   case "connect_account":body["account"]="acct_not_authorized";break;
   default:throw new ArgumentOutOfRangeException(nameof(change));
  }
  Assert.Throws<ArgumentException>(()=>Verify(body));
 }

 [Theory]
 [InlineData("payment_intent.payment_failed","requires_payment_method")]
 [InlineData("payment_intent.canceled","canceled")]
 [InlineData("payment_intent.processing","processing")]
 public void Signed_non_successful_events_cannot_be_interpreted_as_captured_payment(string eventType,string status)
 {
  var body=Body(eventType,status);
  var bytes=Encoding.UTF8.GetBytes(body.ToJsonString());
  var now=DateTimeOffset.UtcNow;
  var t=now.ToUnixTimeSeconds();
  var signingInput=t+"."+body.ToJsonString();
  var signature="t="+t+",v1="+Convert.ToHexString(HMACSHA256.HashData(
   Encoding.UTF8.GetBytes("whsec_fixture_status"),Encoding.UTF8.GetBytes(signingInput)));
  Assert.Throws<ArgumentException>(()=>
   StripeSandboxVerifier.VerifyPayment(bytes,signature,"whsec_fixture_status",now,Expected));
 }
}
