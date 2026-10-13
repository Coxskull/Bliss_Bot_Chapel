using System.Net;
using System.Net.Http.Json;
using Bliss.Api.Financials;
using Bliss.Domain.Entities;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Bliss.Infrastructure.Persistence;
using Bliss.Tests.Financials;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Bliss.Tests.Api;
public sealed class FinancialPreparationPostgresApiTests:IClassFixture<WeddingPlannerOidcFactory>
{
 private readonly WeddingPlannerOidcFactory factory;public FinancialPreparationPostgresApiTests(WeddingPlannerOidcFactory f)=>factory=f;
 private sealed record Response(PreparedPaymentOperation Operation,string Status,bool PaymentCompleted,bool ProviderCalled);
 [FinancialPostgresFact]public async Task Real_http_and_postgres_preparation_imports_exact_quote_replays_and_refuses_revocation()
 {
  var ownerName="FinanceOwner-"+Guid.NewGuid().ToString("N");var q=Guid.NewGuid();var v=Guid.NewGuid();var advertiser=Guid.NewGuid();var binding=new FundingBinding(q,v,new(Guid.NewGuid(),FinancialModule.BlissBot,Guid.NewGuid()),"test-"+ownerName,advertiser,Guid.NewGuid());
  await using var app=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<FinancialPreparationOptions>();s.AddSingleton(new FinancialPreparationOptions{Enabled=true,Mode="Mock",FixtureConnection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!,Bindings=[binding]});}));
  using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<BlissDbContext>();db.AddRange(new Quote{Id=q,Status="ACCEPTED",CurrencyCode="USD",CurrentVersionNumber=1},new QuoteVersion{Id=v,QuoteId=q,VersionNumber=1,TotalAmount=42.25m,CurrencyCode="USD"},new QuoteApprovalDecision{Id=Guid.NewGuid(),QuoteId=q,QuoteVersionId=v,Decision="APPROVED",CreatedAt=DateTime.UtcNow},new QuoteOutcome{Id=Guid.NewGuid(),QuoteId=q,QuoteVersionId=v,Response="ACCEPTED",Amount=42.25m,CurrencyCode="USD",CreatedAt=DateTime.UtcNow});await db.SaveChangesAsync();}
  var client=app.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),HandleCookies=true});client.DefaultRequestHeaders.Add("X-Test-Name",ownerName);client.DefaultRequestHeaders.Add("X-Test-Roles","bliss.advertiser");client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id",advertiser.ToString());var session=await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session!.CsrfToken);
  var first=await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=q,idempotencyKey="http"});Assert.Equal(HttpStatusCode.OK,first.StatusCode);var body=await first.Content.ReadFromJsonAsync<Response>();Assert.Equal(4225,body!.Operation.AmountMinor);Assert.Equal(binding.RecipientAccountId,body.Operation.RecipientAccountId);Assert.False(body.PaymentCompleted);Assert.False(body.ProviderCalled);
  var replay=await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=q,idempotencyKey="http"});Assert.Equal(body.Operation.Id,(await replay.Content.ReadFromJsonAsync<Response>())!.Operation.Id);
  using(var scope=app.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<BlissDbContext>();(await db.Quotes.FindAsync(q))!.Status="REJECTED";await db.SaveChangesAsync();}Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=q,idempotencyKey="after-revoke"})).StatusCode);
 }
}
