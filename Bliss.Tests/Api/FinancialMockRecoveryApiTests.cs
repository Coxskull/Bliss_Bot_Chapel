using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Api.Financials;
using Bliss.Domain.Entities;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Financials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
namespace Bliss.Tests.Api;
public sealed class FinancialMockRecoveryApiTests(WeddingPlannerOidcFactory factory):IClassFixture<WeddingPlannerOidcFactory>
{
 private sealed class NoProvider:HttpMessageHandler
 {public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;throw new InvalidOperationException("Provider must not be called by recovery.");}}
 [FinancialPostgresFact]public async Task Three_modules_recover_aged_mock_identity_without_recreation_and_preserve_cash_on_late_status()
 {
  var name="RecoveryOwner-"+Guid.NewGuid().ToString("N");var advertiser=Guid.NewGuid();var bindings=Enum.GetValues<FinancialModule>().Select(m=>new FundingBinding(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),m,Guid.NewGuid()),"test-"+name,advertiser,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"mock-recovery")).ToList();
  var options=new FinancialPreparationOptions{Enabled=true,Mode="Mock",FixtureConnection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!,Bindings=bindings,MockAccountProfiles=[new("mock-recovery","whsec_fixture_recovery")]};
  await using var app=factory.WithWebHostBuilder(builder=>builder.ConfigureServices(services=>{services.RemoveAll<FinancialPreparationOptions>();services.AddSingleton(options);}));
  using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<BlissDbContext>();foreach(var b in bindings)db.AddRange(new Quote{Id=b.SourceQuoteId,Status="ACCEPTED",CurrencyCode="USD",CurrentVersionNumber=1},new QuoteVersion{Id=b.SourceVersionId,QuoteId=b.SourceQuoteId,VersionNumber=1,TotalAmount=10,CurrencyCode="USD"},new QuoteApprovalDecision{Id=Guid.NewGuid(),QuoteId=b.SourceQuoteId,QuoteVersionId=b.SourceVersionId,Decision="APPROVED",CreatedAt=DateTime.UtcNow},new QuoteOutcome{Id=Guid.NewGuid(),QuoteId=b.SourceQuoteId,QuoteVersionId=b.SourceVersionId,Response="ACCEPTED",Amount=10,CurrencyCode="USD",CreatedAt=DateTime.UtcNow});await db.SaveChangesAsync();}
  async Task<HttpClient> Client(string subject,string role){var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new("https://localhost"),HandleCookies=true});client.DefaultRequestHeaders.Add("X-Test-Name",subject);client.DefaultRequestHeaders.Add("X-Test-Roles",role);client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id",advertiser.ToString());var session=await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session!.CsrfToken);return client;}
  using var owner=await Client(name,"bliss.advertiser");await using var data=NpgsqlDataSource.Create(options.FixtureConnection);var principal=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","test-"+name)],"fixture"));
  foreach(var b in bindings)
  {
   using var staff=await Client("RecoveryQA-"+b.Scope.Module,"bliss.operator");var quotes=new PaymentOperationStore(data);await quotes.FreezeQuote(new(b.SourceVersionId,b.SourceVersionId,b.Scope,"test-"+name,b.RecipientAccountId,1000,"USD"));var op=await quotes.Prepare(principal,b.SourceVersionId,"recover-"+b.Scope.Module);
   await using(var aged=data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '21 hours',clock_timestamp()-interval '20 hours')")){aged.Parameters.AddWithValue("a",Guid.NewGuid());aged.Parameters.AddWithValue("i",op.Id);await aged.ExecuteNonQueryAsync();}
   var pi="pi_fixture_"+op.Id.ToString("N");options.MockRecoveryFixtures.Add(new(op,"mock-recovery",pi,"processing"));
   Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsJsonAsync("/api/financials/mock-payments/reconcile",new{operationId=op.Id})).StatusCode);
   Assert.Equal(HttpStatusCode.BadRequest,(await staff.PostAsJsonAsync("/api/financials/mock-payments/reconcile",new{operationId=op.Id,paymentIntentId=pi})).StatusCode);
   var recoveries=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>staff.PostAsJsonAsync("/api/financials/mock-payments/reconcile",new{operationId=op.Id})));Guid recovered=Guid.Empty;
   foreach(var response in recoveries){Assert.Equal(HttpStatusCode.OK,response.StatusCode);var json=await response.Content.ReadFromJsonAsync<JsonElement>();var id=json.GetProperty("recoveryId").GetGuid();if(recovered==Guid.Empty)recovered=id;Assert.Equal(recovered,id);Assert.False(json.GetProperty("paymentCompleted").GetBoolean());Assert.False(json.GetProperty("providerCalled").GetBoolean());Assert.Equal(FinancialAccountPolicy.StripeAccountStatus,json.GetProperty("stripeAccountStatus").GetString());response.Dispose();}
   var handler=new NoProvider();Assert.Equal("BOUND",await new StripePaymentDispatcher(new(data),new(new HttpClient(handler),"sk_test_fixture_not_a_credential"),(_,_,_)=>Task.CompletedTask).Dispatch(principal,op.Id));Assert.Equal(0,handler.Calls);Assert.Null(await new MockPaymentCaptureStore(data).Captured(op.Id));
   string Raw(string eventId,string type,string status,long received)=>JsonSerializer.Serialize(new{id=eventId,type,livemode=false,data=new{@object=new{id=pi,@object="payment_intent",livemode=false,status,amount=1000,amount_received=received,currency="usd",metadata=new{merchant_id=op.Scope.MerchantId,campaign_id=op.Scope.CampaignId,module=op.Scope.Module.ToString(),funding_reference=op.QuoteId.ToString("N"),account_profile="mock-recovery"}}}});
   object Request(string raw){var t=DateTimeOffset.UtcNow.ToUnixTimeSeconds();return new{operationId=op.Id,rawJson=raw,signature="t="+t+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("whsec_fixture_recovery"),Encoding.UTF8.GetBytes(t+"."+raw)))};}
   var failedId="evt_"+Guid.NewGuid().ToString("N");var failed=Raw(failedId,"payment_intent.payment_failed","requires_payment_method",0);Assert.Equal(HttpStatusCode.BadRequest,(await staff.PostAsJsonAsync("/api/financials/mock-status-webhooks",Request(Raw(failedId,"payment_intent.payment_failed","requires_payment_method",1)))).StatusCode);
   Assert.Equal(HttpStatusCode.OK,(await staff.PostAsJsonAsync("/api/financials/mock-status-webhooks",Request(failed))).StatusCode);Assert.Equal(HttpStatusCode.OK,(await staff.PostAsJsonAsync("/api/financials/mock-status-webhooks",Request(failed))).StatusCode);
   Assert.Equal(HttpStatusCode.Conflict,(await staff.PostAsJsonAsync("/api/financials/mock-status-webhooks",Request(failed+" "))).StatusCode);
   var cancel=Raw("evt_"+Guid.NewGuid().ToString("N"),"payment_intent.canceled","canceled",0);
   if(b.Scope.Module==FinancialModule.WeddingPlanner)Assert.Equal(HttpStatusCode.OK,(await staff.PostAsJsonAsync("/api/financials/mock-status-webhooks",Request(cancel))).StatusCode);
   var succeeded=Raw("evt_"+Guid.NewGuid().ToString("N"),"payment_intent.succeeded","succeeded",1000);var capture=await staff.PostAsJsonAsync("/api/financials/mock-bound-webhooks",Request(succeeded));Assert.Equal(b.Scope.Module==FinancialModule.WeddingPlanner?HttpStatusCode.Conflict:HttpStatusCode.OK,capture.StatusCode);
   if(b.Scope.Module!=FinancialModule.WeddingPlanner)
   {
    using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<BlissDbContext>();(await db.Quotes.FindAsync(b.SourceQuoteId))!.Status="REJECTED";await db.SaveChangesAsync();}
    Assert.Equal(HttpStatusCode.OK,(await staff.PostAsJsonAsync("/api/financials/mock-status-webhooks",Request(cancel))).StatusCode);
   }
   var reading=await owner.GetFromJsonAsync<JsonElement>("/api/financials/modules/"+b.Scope.Module+"/operations");var item=reading.GetProperty("items")[0];Assert.Equal(b.Scope.Module!=FinancialModule.WeddingPlanner,item.GetProperty("simulatedCaptureRecorded").GetBoolean());Assert.True(item.GetProperty("mockReconciliation").GetProperty("requiresReview").GetBoolean());Assert.Equal(b.Scope.Module==FinancialModule.WeddingPlanner?"CANCELED_REVIEW_REQUIRED":"CAPTURED_TEST_ONLY",item.GetProperty("mockReconciliation").GetProperty("state").GetString());Assert.False(reading.GetProperty("paymentCompleted").GetBoolean());
  }
 }
}
