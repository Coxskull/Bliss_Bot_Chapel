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
}
