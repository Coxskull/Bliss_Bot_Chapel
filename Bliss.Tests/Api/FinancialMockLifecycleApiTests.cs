using System.Net;
using System.Net.Http.Json;
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
namespace Bliss.Tests.Api;
public sealed class FinancialMockLifecycleApiTests:IClassFixture<WeddingPlannerOidcFactory>
{
 private readonly WeddingPlannerOidcFactory factory;public FinancialMockLifecycleApiTests(WeddingPlannerOidcFactory f)=>factory=f;
 private sealed record Prepared(PreparedPaymentOperation Operation);
 private sealed record Journal(Guid? JournalId);
 private sealed record Refund(ReservedRefund RefundReservation);
 [FinancialPostgresFact]public async Task Three_modules_share_owned_preparation_signed_mock_capture_and_attributed_refund_lifecycle()
 {
  var advertiser=Guid.NewGuid();var bindings=Enum.GetValues<FinancialModule>().Select(m=>new FundingBinding(Guid.NewGuid(),Guid.NewGuid(),new(Guid.NewGuid(),m,Guid.NewGuid()),"test-SharedOwner",advertiser,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid())).ToList();
  await using var app=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<FinancialPreparationOptions>();s.AddSingleton(new FinancialPreparationOptions{Enabled=true,Mode="Mock",FixtureConnection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!,Bindings=bindings,MockSigningSecret="whsec_fixture_only"});}));
  using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<BlissDbContext>();foreach(var b in bindings)db.AddRange(new Quote{Id=b.SourceQuoteId,Status="ACCEPTED",CurrentVersionNumber=1,CurrencyCode="USD"},new QuoteVersion{Id=b.SourceVersionId,QuoteId=b.SourceQuoteId,VersionNumber=1,TotalAmount=10,CurrencyCode="USD"},new QuoteApprovalDecision{Id=Guid.NewGuid(),QuoteId=b.SourceQuoteId,QuoteVersionId=b.SourceVersionId,Decision="APPROVED",CreatedAt=DateTime.UtcNow},new QuoteOutcome{Id=Guid.NewGuid(),QuoteId=b.SourceQuoteId,QuoteVersionId=b.SourceVersionId,Response="ACCEPTED",Amount=10,CurrencyCode="USD",CreatedAt=DateTime.UtcNow});await db.SaveChangesAsync();}
  async Task<HttpClient> Client(string name,string role){var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),HandleCookies=true});client.DefaultRequestHeaders.Add("X-Test-Name",name);client.DefaultRequestHeaders.Add("X-Test-Roles",role);client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id",advertiser.ToString());var session=await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session!.CsrfToken);return client;}
  using var owner=await Client("SharedOwner","bliss.advertiser");using var staff=await Client("FinancialQA","bliss.operator");using var other=await Client("UnrelatedOwner","bliss.advertiser");
  foreach(var b in bindings)
  {
   var preparation=await owner.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=b.SourceQuoteId,idempotencyKey="prepare-"+b.Scope.Module});Assert.Equal(HttpStatusCode.OK,preparation.StatusCode);var op=(await preparation.Content.ReadFromJsonAsync<Prepared>())!.Operation;
   var raw=JsonSerializer.Serialize(new{id="evt_"+Guid.NewGuid().ToString("N"),livemode=false,type="payment_intent.succeeded",data=new{@object=new{id="pi_mock_"+op.Id.ToString("N"),@object="payment_intent",livemode=false,status="succeeded",amount=1000,amount_received=1000,currency="usd",metadata=new{merchant_id=op.Scope.MerchantId,campaign_id=op.Scope.CampaignId,module=op.Scope.Module.ToString()}}}});var timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds();var signature="t="+timestamp+",v1="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("whsec_fixture_only"),Encoding.UTF8.GetBytes(timestamp+"."+raw)));var request=new{operationId=op.Id,rawJson=raw,signature};
   Assert.Equal(HttpStatusCode.Forbidden,(await owner.PostAsJsonAsync("/api/financials/mock-webhooks",request)).StatusCode);
   Assert.Equal(HttpStatusCode.BadRequest,(await staff.PostAsJsonAsync("/api/financials/mock-webhooks",new{operationId=op.Id,rawJson=raw,signature="invalid"})).StatusCode);
   var captured=await staff.PostAsJsonAsync("/api/financials/mock-webhooks",request);Assert.Equal(HttpStatusCode.OK,captured.StatusCode);var journal=(await captured.Content.ReadFromJsonAsync<Journal>())!.JournalId;var replay=await staff.PostAsJsonAsync("/api/financials/mock-webhooks",request);Assert.Equal(journal,(await replay.Content.ReadFromJsonAsync<Journal>())!.JournalId);
   var owned=await owner.GetFromJsonAsync<JsonElement>("/api/financials/modules/"+b.Scope.Module+"/operations");Assert.True(owned.GetProperty("items")[0].GetProperty("simulatedCaptureRecorded").GetBoolean());Assert.False(owned.GetProperty("paymentCompleted").GetBoolean());var unrelated=await other.GetFromJsonAsync<JsonElement>("/api/financials/modules/"+b.Scope.Module+"/operations");Assert.Equal(0,unrelated.GetProperty("items").GetArrayLength());
   var reserved=await staff.PostAsJsonAsync("/api/financials/mock-refunds",new{operationId=op.Id,amountMinor=400,idempotencyKey="partial"});Assert.Equal(HttpStatusCode.OK,reserved.StatusCode);var refund=JsonDocument.Parse(await reserved.Content.ReadAsStringAsync()).RootElement.GetProperty("refund").GetProperty("id").GetGuid();
   var pending=await staff.PostAsJsonAsync("/api/financials/mock-refunds/reconcile",new{refundId=refund,status="PENDING"});Assert.Equal(HttpStatusCode.OK,pending.StatusCode);Assert.Null((await pending.Content.ReadFromJsonAsync<Journal>())!.JournalId);
   var succeeded=await staff.PostAsJsonAsync("/api/financials/mock-refunds/reconcile",new{refundId=refund,status="SUCCEEDED"});Assert.Equal(HttpStatusCode.OK,succeeded.StatusCode);Assert.NotNull((await succeeded.Content.ReadFromJsonAsync<Journal>())!.JournalId);Assert.Equal(HttpStatusCode.Conflict,(await staff.PostAsJsonAsync("/api/financials/mock-refunds",new{operationId=op.Id,amountMinor=700,idempotencyKey="over"})).StatusCode);
  }
 }
}
