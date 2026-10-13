using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
namespace Bliss.Tests.Financials;
public sealed class StripeSandboxVerifierTests
{
 private static readonly DateTimeOffset Now=DateTimeOffset.FromUnixTimeSeconds(1000000);
 private static readonly ExpectedSandboxPayment Expected=new("pi_fixture",new(Guid.NewGuid(),FinancialModule.BlissBot,Guid.NewGuid()),"USD",100);
 private static byte[] Body(bool live=false,long amount=100,string module="BlissBot")=>JsonSerializer.SerializeToUtf8Bytes(new{id="evt_fixture",livemode=live,type="payment_intent.succeeded",data=new{@object=new{id="pi_fixture",@object="payment_intent",livemode=live,status="succeeded",amount,amount_received=amount,currency="usd",metadata=new{merchant_id=Expected.Scope.MerchantId,campaign_id=Expected.Scope.CampaignId,module}}}});
 private static string Sign(byte[] body,long timestamp=1000000){var text=timestamp+"."+Encoding.UTF8.GetString(body);return "t="+timestamp+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("fixture-webhook-secret"),Encoding.UTF8.GetBytes(text)));}
 [Fact] public void Signed_test_payment_matches_authoritative_scope(){var b=Body();Assert.Equal("evt_fixture",StripeSandboxVerifier.VerifyPayment(b,Sign(b),"fixture-webhook-secret",Now,Expected).EventId);}
 [Theory][InlineData(true,100,"BlissBot")][InlineData(false,101,"BlissBot")][InlineData(false,100,"WeddingPlanner")]
 public void Live_amount_or_module_mismatch_is_refused(bool live,long amount,string module){var b=Body(live,amount,module);Assert.Throws<ArgumentException>(()=>StripeSandboxVerifier.VerifyPayment(b,Sign(b),"fixture-webhook-secret",Now,Expected));}
 [Fact] public void Altered_raw_body_is_refused(){var b=Body();Assert.Throws<ArgumentException>(()=>StripeSandboxVerifier.VerifyPayment(Body(amount:99),Sign(b),"fixture-webhook-secret",Now,Expected));}
 [Fact] public void Stale_and_future_signatures_are_refused(){var b=Body();foreach(var t in new[]{999699L,1000301L})Assert.Throws<ArgumentException>(()=>StripeSandboxVerifier.VerifyPayment(b,Sign(b,t),"fixture-webhook-secret",Now,Expected));}
 [Fact] public void Different_campaign_or_payment_id_is_refused(){var b=Body();Assert.Throws<ArgumentException>(()=>StripeSandboxVerifier.VerifyPayment(b,Sign(b),"fixture-webhook-secret",Now,Expected with{PaymentIntentId="pi_other"}));Assert.Throws<ArgumentException>(()=>StripeSandboxVerifier.VerifyPayment(b,Sign(b),"fixture-webhook-secret",Now,Expected with{Scope=Expected.Scope with{CampaignId=Guid.NewGuid()}}));}
}
