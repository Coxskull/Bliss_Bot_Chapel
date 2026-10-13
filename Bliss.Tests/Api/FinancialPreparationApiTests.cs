using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Bliss.Api.Financials;
using Bliss.Infrastructure.Financials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Bliss.Tests.Api;
public sealed class FinancialPreparationApiTests:IClassFixture<WeddingPlannerOidcFactory>
{
 private readonly WeddingPlannerOidcFactory factory;
 public FinancialPreparationApiTests(WeddingPlannerOidcFactory f)=>factory=f;
 private sealed class Preparation:IFinancialPreparation
 {
  public int Calls;public Task<PreparedPaymentOperation> Prepare(ClaimsPrincipal user,Guid quote,string key,CancellationToken ct){Calls++;throw new UnauthorizedAccessException();}
 }
 [Fact]public async Task Authentication_role_csrf_and_unknown_amount_payee_fields_are_enforced()
 {
  var fake=new Preparation();await using var app=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<IFinancialPreparation>();s.AddSingleton<IFinancialPreparation>(fake);}));
  var anonymous=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=Guid.NewGuid(),idempotencyKey="a"})).StatusCode);
  var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),HandleCookies=true});client.DefaultRequestHeaders.Add("X-Test-Name","Finance");client.DefaultRequestHeaders.Add("X-Test-Roles","bliss.advertiser");client.DefaultRequestHeaders.Add("X-Test-Advertiser-Id",Guid.NewGuid().ToString());
  Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=Guid.NewGuid(),idempotencyKey="a"})).StatusCode);
  var session=await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session!.CsrfToken);
  Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=Guid.NewGuid(),idempotencyKey="a",amount=1,recipient="attacker"})).StatusCode);Assert.Equal(0,fake.Calls);
  Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=Guid.NewGuid(),idempotencyKey="a"})).StatusCode);Assert.Equal(1,fake.Calls);
  client.DefaultRequestHeaders.Remove("X-Test-Roles");client.DefaultRequestHeaders.Add("X-Test-Roles","bliss.viewer");Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=Guid.NewGuid(),idempotencyKey="a"})).StatusCode);Assert.Equal(1,fake.Calls);
 }
 [Fact]public async Task Default_disabled_financials_do_not_query_or_dispatch()
 {
  var client=factory.CreateSecureClient();client.DefaultRequestHeaders.Add("X-Test-Name","FinanceDisabled");client.DefaultRequestHeaders.Add("X-Test-Roles","bliss.advertiser");var session=await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session!.CsrfToken);Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/financials/payment-operations",new{quoteId=Guid.NewGuid(),idempotencyKey="a"})).StatusCode);
 }
}
