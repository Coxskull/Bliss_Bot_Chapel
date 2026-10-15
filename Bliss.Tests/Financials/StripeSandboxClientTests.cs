using System.Net;
using System.Text;
using System.Text.Json;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
namespace Bliss.Tests.Financials;
public sealed class StripeSandboxClientTests
{
 private static readonly SandboxChargeRequest Charge=new(new(Guid.NewGuid(),FinancialModule.CreatorAcademy,Guid.NewGuid()),"quote-v1","USD",100);
 private sealed class Handler(bool live=false,long amount=100,HttpStatusCode code=HttpStatusCode.OK):HttpMessageHandler
 {
  public string? Body;public string? Key;public Uri? Uri;public int Calls;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
  {Calls++;Body=await request.Content!.ReadAsStringAsync(token);Key=request.Headers.GetValues("Idempotency-Key").Single();Uri=request.RequestUri;return new(code){Content=new StringContent(JsonSerializer.Serialize(new{id="pi_fixture",@object="payment_intent",livemode=live,status="requires_payment_method",amount,currency="usd",metadata=new{merchant_id=Charge.Scope.MerchantId,campaign_id=Charge.Scope.CampaignId,module=Charge.Scope.Module.ToString(),funding_reference=Charge.FundingReference}}),Encoding.UTF8,"application/json")};}
 }
 [Fact]public async Task Request_is_minor_unit_scoped_idempotent_and_pending(){var h=new Handler();var c=new StripeSandboxClient(new HttpClient(h),"sk_test_fixture_not_a_credential");var result=await c.CreatePaymentIntent(Charge,"operation1");Assert.Equal("requires_payment_method",result.Status);Assert.StartsWith("agm-",h.Key);var firstKey=h.Key;await c.CreatePaymentIntent(Charge,"operation1");Assert.Equal(firstKey,h.Key);Assert.Equal("https://api.stripe.com/v1/payment_intents",h.Uri!.ToString());Assert.Contains("amount=100",h.Body);Assert.Contains("currency=usd",h.Body);Assert.Contains("CreatorAcademy",h.Body);}
 [Theory][InlineData(true,100)][InlineData(false,101)]public async Task Live_or_amount_mismatch_response_is_refused(bool live,long amount){var c=new StripeSandboxClient(new HttpClient(new Handler(live,amount)),"sk_test_fixture_not_a_credential");await Assert.ThrowsAsync<InvalidOperationException>(()=>c.CreatePaymentIntent(Charge,"a"));}
 [Fact]public void Live_secret_is_refused()=>Assert.Throws<ArgumentException>(()=>new StripeSandboxClient(new HttpClient(new Handler()),"sk_live_fixture_not_a_credential"));
 [Fact]public async Task Invalid_amount_does_not_call_provider(){var h=new Handler();var c=new StripeSandboxClient(new HttpClient(h),"sk_test_fixture_not_a_credential");await Assert.ThrowsAsync<ArgumentException>(()=>c.CreatePaymentIntent(Charge with{AmountMinor=0},"a"));Assert.Equal(0,h.Calls);}
 [Fact]public async Task Provider_error_does_not_expose_response_body(){var c=new StripeSandboxClient(new HttpClient(new Handler(code:HttpStatusCode.BadRequest)),"sk_test_fixture_not_a_credential");var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>c.CreatePaymentIntent(Charge,"a"));Assert.DoesNotContain("pi_fixture",error.Message);}
 [Fact]public async Task Changed_amount_reuses_operation_key_but_refuses_old_provider_result(){var h=new Handler();var c=new StripeSandboxClient(new HttpClient(h),"sk_test_fixture_not_a_credential");await c.CreatePaymentIntent(Charge,"a");var key=h.Key;await Assert.ThrowsAsync<InvalidOperationException>(()=>c.CreatePaymentIntent(Charge with{AmountMinor=101},"a"));Assert.Equal(key,h.Key);}
 [Theory][InlineData("sk_test_")][InlineData("sk_test_bad\\nvalue")][InlineData("sk_test_bad value")]public void Malformed_test_secret_is_refused(string secret)=>Assert.Throws<ArgumentException>(()=>new StripeSandboxClient(new HttpClient(new Handler()),secret.Replace("\\n", "\n")));
 [Fact]public async Task Control_characters_in_funding_reference_are_refused_before_provider(){var h=new Handler();var c=new StripeSandboxClient(new HttpClient(h),"sk_test_fixture_not_a_credential");await Assert.ThrowsAsync<ArgumentException>(()=>c.CreatePaymentIntent(Charge with{FundingReference="quote" + (char)10 + "unsafe"},"operation"));Assert.Equal(0,h.Calls);}
 [Theory][InlineData("quote with space","operation")][InlineData("quote-v1","operation with space")]public async Task Whitespace_in_funding_reference_or_operation_key_is_refused(string reference,string key){var h=new Handler();var c=new StripeSandboxClient(new HttpClient(h),"sk_test_fixture_not_a_credential");await Assert.ThrowsAsync<ArgumentException>(()=>c.CreatePaymentIntent(Charge with{FundingReference=reference},key));Assert.Equal(0,h.Calls);}
 private sealed class CanceledHandler:HttpMessageHandler
 {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true));
 }
 [Fact]public async Task Canceled_provider_call_never_returns_a_payment_intent(){var c=new StripeSandboxClient(new HttpClient(new CanceledHandler()),"sk_test_fixture_not_a_credential");await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>c.CreatePaymentIntent(Charge,"canceled"));}
 [Fact]public async Task Initial_non_usd_payment_is_refused_before_provider(){var h=new Handler();var c=new StripeSandboxClient(new HttpClient(h),"sk_test_fixture_not_a_credential");await Assert.ThrowsAsync<ArgumentException>(()=>c.CreatePaymentIntent(Charge with{Currency="PHP"},"a"));Assert.Equal(0,h.Calls);}
}
