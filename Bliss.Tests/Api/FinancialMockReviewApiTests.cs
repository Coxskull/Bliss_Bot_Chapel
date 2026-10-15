using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Bliss.Api.Financials;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Bliss.Tests.Financials;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
namespace Bliss.Tests.Api;
[Collection("Mock financial review isolation")]
public sealed class FinancialMockReviewApiTests(WeddingPlannerOidcFactory factory):IClassFixture<WeddingPlannerOidcFactory>
{
 [FinancialPostgresFact]public async Task Three_module_queue_pages_and_versioned_annotations_enforce_reviewer_scope_without_approving_or_releasing()
 {
  var scopes=Enum.GetValues<FinancialModule>().ToDictionary(m=>m,m=>new FinancialScope(Guid.NewGuid(),m,Guid.NewGuid()));
  var bindings=scopes.Values.SelectMany(scope=>Enumerable.Range(0,2).Select(_=>new FundingBinding(Guid.NewGuid(),Guid.NewGuid(),scope,"review-fixture-owner",Guid.NewGuid(),Guid.NewGuid()))).ToList();
  var options=new FinancialPreparationOptions{Enabled=true,Mode="Mock",FixtureConnection=Environment.GetEnvironmentVariable("BLISS_FINANCIAL_TEST_POSTGRES")!,Bindings=bindings,MockReviewers=scopes.Values.Select(s=>new MockReviewerScope("test-ReviewQA",s)).Append(new("test-RestrictedQA",scopes[FinancialModule.BlissBot])).ToList()};
  await using var app=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<FinancialPreparationOptions>();s.AddSingleton(options);}));
  await using var data=NpgsqlDataSource.Create(options.FixtureConnection);var quotes=new PaymentOperationStore(data);var ownerPrincipal=new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","review-fixture-owner")],"fixture"));var operations=new List<PreparedPaymentOperation>();
  foreach(var binding in bindings)
  {
   await quotes.FreezeQuote(new(binding.SourceVersionId,binding.SourceVersionId,binding.Scope,binding.AdvertiserSubject,binding.RecipientAccountId,1000,"USD"));var op=await quotes.Prepare(ownerPrincipal,binding.SourceVersionId,"review-"+binding.SourceVersionId);operations.Add(op);
   await using var seed=data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '21 hours',clock_timestamp()-interval '20 hours')");seed.Parameters.AddWithValue("a",Guid.NewGuid());seed.Parameters.AddWithValue("i",op.Id);await seed.ExecuteNonQueryAsync();
  }
  // A different merchant using the same quote-version is excluded by the full scoped predicate.
  var hiddenVersion=bindings[0].SourceVersionId;var hiddenQuote=new AuthorizedQuoteSnapshot(hiddenVersion,hiddenVersion,new(Guid.NewGuid(),FinancialModule.BlissBot,Guid.NewGuid()),"hidden-owner",Guid.NewGuid(),1000,"USD");
  // A distinct quote ID is required by the primary key; its source version intentionally collides.
  hiddenQuote=hiddenQuote with{Id=Guid.NewGuid()};await quotes.FreezeQuote(hiddenQuote);var hidden=await quotes.Prepare(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub","hidden-owner")],"fixture")),hiddenQuote.Id,"hidden");
  await using(var seed=data.CreateCommand("INSERT INTO financials.payment_dispatch_attempts(id,operation_id,started_at,lease_until) VALUES(@a,@i,clock_timestamp()-interval '21 hours',clock_timestamp()-interval '20 hours')")){seed.Parameters.AddWithValue("a",Guid.NewGuid());seed.Parameters.AddWithValue("i",hidden.Id);await seed.ExecuteNonQueryAsync();}
  async Task<HttpClient> Client(string name,string role){var c=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new("https://localhost"),HandleCookies=true});c.DefaultRequestHeaders.Add("X-Test-Name",name);c.DefaultRequestHeaders.Add("X-Test-Roles",role);var session=await c.GetFromJsonAsync<SessionResponse>("/api/auth/session");c.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session!.CsrfToken);return c;}
  using var reviewer=await Client("ReviewQA","bliss.operator");using var restricted=await Client("RestrictedQA","bliss.operator");using var unknownAdmin=await Client("UnknownAdmin","bliss.admin");using var advertiser=await Client("AdvertiserQA","bliss.advertiser");
  Assert.Equal(HttpStatusCode.Forbidden,(await unknownAdmin.GetAsync("/api/financials/mock-review/queue")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await advertiser.GetAsync("/api/financials/mock-review/queue")).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await reviewer.GetAsync("/api/financials/mock-review/queue?limit=101")).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await reviewer.GetAsync("/api/financials/mock-review/queue?module=unknown")).StatusCode);
  var recovery=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/recovery-context");var actorKey=recovery.GetProperty("result").GetProperty("actorKey").GetString();Assert.Matches("^[0-9A-F]{64}$",actorKey!);Assert.DoesNotContain("test-ReviewQA",recovery.ToString());var otherRecovery=await restricted.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/recovery-context");Assert.NotEqual(actorKey,otherRecovery.GetProperty("result").GetProperty("actorKey").GetString());Assert.Equal(HttpStatusCode.Forbidden,(await unknownAdmin.GetAsync("/api/financials/mock-review/recovery-context")).StatusCode);
  var seen=new HashSet<Guid>();Guid? after=null;
  do{var page=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/queue?limit=2"+(after is null?"":"&after="+after));Assert.Equal(FinancialAccountPolicy.StripeAccountStatus,page.GetProperty("stripeAccountStatus").GetString());var result=page.GetProperty("result");foreach(var item in result.GetProperty("items").EnumerateArray()){Assert.Equal("1000",item.GetProperty("amountMinor").GetString());Assert.True(seen.Add(item.GetProperty("operationId").GetGuid()));}after=result.GetProperty("nextAfter").ValueKind==JsonValueKind.Null?null:result.GetProperty("nextAfter").GetGuid();}while(after is not null);
  Assert.Equal(operations.Select(x=>x.Id).OrderBy(x=>x),seen.OrderBy(x=>x));Assert.DoesNotContain(hidden.Id,seen);
  var narrow=await restricted.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/queue");Assert.Equal(2,narrow.GetProperty("result").GetProperty("items").GetArrayLength());
  var foreign=operations.First(x=>x.Scope.Module==FinancialModule.CreatorAcademy);Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/financials/mock-review/cases/"+foreign.Id)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await reviewer.GetAsync("/api/financials/mock-review/cases/"+hidden.Id)).StatusCode);
  foreach(var op in operations.GroupBy(x=>x.Scope.Module).Select(g=>g.First()))
  {
   var filtered=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/queue?module="+op.Scope.Module);Assert.Equal(2,filtered.GetProperty("result").GetProperty("items").GetArrayLength());
   var envelope=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/cases/"+op.Id);var snapshot=envelope.GetProperty("result");var fingerprint=snapshot.GetProperty("evidenceFingerprint").GetString();Assert.True(snapshot.GetProperty("requiresReview").GetBoolean());
   Assert.Equal(HttpStatusCode.BadRequest,(await reviewer.PostAsJsonAsync("/api/financials/mock-review/notes",new{operationId=op.Id,evidenceFingerprint=fingerprint,note="Observed.",idempotencyKey="bad-"+op.Id,holdReleased=true})).StatusCode);
   var export=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/cases/"+op.Id+"/evidence-export?evidenceFingerprint="+fingerprint);var artifact=export.GetProperty("result").GetProperty("artifactJson").GetString()!;Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(artifact))),export.GetProperty("result").GetProperty("sha256").GetString());using(var document=JsonDocument.Parse(artifact)){Assert.Equal(op.Id,document.RootElement.GetProperty("operationId").GetGuid());Assert.Equal(snapshot.GetProperty("evidenceJson").GetString(),document.RootElement.GetProperty("evidenceJson").GetString());Assert.False(document.RootElement.GetProperty("includesNotes").GetBoolean());Assert.False(document.RootElement.GetProperty("approvalGranted").GetBoolean());}Assert.Equal(HttpStatusCode.Conflict,(await reviewer.GetAsync("/api/financials/mock-review/cases/"+op.Id+"/evidence-export?evidenceFingerprint="+new string('A',64))).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await reviewer.GetAsync("/api/financials/mock-review/cases/"+op.Id+"/evidence-export?evidenceFingerprint=bad")).StatusCode);if(op.Scope.Module!=FinancialModule.BlissBot)Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/financials/mock-review/cases/"+op.Id+"/evidence-export?evidenceFingerprint="+fingerprint)).StatusCode);
   var request=new{operationId=op.Id,evidenceFingerprint=fingerprint,note="Fixture evidence observed; owner review pending.",idempotencyKey=Guid.NewGuid().ToString()};var response=await reviewer.PostAsJsonAsync("/api/financials/mock-review/notes",request);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var saved=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.False(saved.GetProperty("approvalGranted").GetBoolean());Assert.False(saved.GetProperty("holdReleased").GetBoolean());Assert.False(saved.GetProperty("paymentCompleted").GetBoolean());var noteId=saved.GetProperty("result").GetProperty("id").GetGuid();Assert.Equal("test-ReviewQA",saved.GetProperty("result").GetProperty("actorSubject").GetString());
   var notesPage=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/cases/"+op.Id+"/notes?limit=1");Assert.Equal(noteId,notesPage.GetProperty("result").GetProperty("items")[0].GetProperty("id").GetGuid());Assert.Equal(JsonValueKind.Null,notesPage.GetProperty("result").GetProperty("nextAfter").ValueKind);
   Assert.Equal(HttpStatusCode.BadRequest,(await reviewer.GetAsync("/api/financials/mock-review/cases/"+op.Id+"/notes?limit=101")).StatusCode);
   if(op.Scope.Module!=FinancialModule.BlissBot)Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/financials/mock-review/cases/"+op.Id+"/notes")).StatusCode);
   var retry=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/cases/"+op.Id+"/note-retries/"+request.idempotencyKey);Assert.Equal(noteId,retry.GetProperty("result").GetProperty("id").GetGuid());var absent=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/cases/"+op.Id+"/note-retries/"+Guid.NewGuid());Assert.Equal(JsonValueKind.Null,absent.GetProperty("result").ValueKind);
   if(op.Scope.Module!=FinancialModule.BlissBot)Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/financials/mock-review/cases/"+op.Id+"/note-retries/"+request.idempotencyKey)).StatusCode);
   var replay=await reviewer.PostAsJsonAsync("/api/financials/mock-review/notes",request);Assert.Equal(noteId,(await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("result").GetProperty("id").GetGuid());
   var historical=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/notes/"+noteId);Assert.Equal(snapshot.GetProperty("evidenceJson").GetString(),historical.GetProperty("result").GetProperty("evidenceJson").GetString());
   if(op.Scope.Module!=FinancialModule.BlissBot)Assert.Equal(HttpStatusCode.Forbidden,(await restricted.GetAsync("/api/financials/mock-review/notes/"+noteId)).StatusCode);
   var unchanged=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/cases/"+op.Id);Assert.True(unchanged.GetProperty("result").GetProperty("requiresReview").GetBoolean());Assert.Null(await new MockPaymentCaptureStore(data).Captured(op.Id));
  }
  using var noCsrf=await Client("ReviewQA","bliss.operator");noCsrf.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");var first=operations[0];var firstView=await reviewer.GetFromJsonAsync<JsonElement>("/api/financials/mock-review/cases/"+first.Id);Assert.Equal(HttpStatusCode.BadRequest,(await noCsrf.PostAsJsonAsync("/api/financials/mock-review/notes",new{operationId=first.Id,evidenceFingerprint=firstView.GetProperty("result").GetProperty("evidenceFingerprint").GetString(),note="Missing CSRF",idempotencyKey="missing-csrf"})).StatusCode);
 }
 [Fact]public async Task Disabled_sandbox_and_live_review_routes_refuse_before_database_access()
 {
  foreach(var config in new[]{(Enabled:false,Mode:"Mock"),(Enabled:true,Mode:"Sandbox"),(Enabled:true,Mode:"Live")})
  {
   await using var app=factory.WithWebHostBuilder(b=>b.ConfigureServices(s=>{s.RemoveAll<FinancialPreparationOptions>();s.AddSingleton(new FinancialPreparationOptions{Enabled=config.Enabled,Mode=config.Mode});}));using var client=app.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new("https://localhost"),HandleCookies=true});client.DefaultRequestHeaders.Add("X-Test-Name","ModeReviewQA");client.DefaultRequestHeaders.Add("X-Test-Roles","bliss.operator");var session=await client.GetFromJsonAsync<SessionResponse>("/api/auth/session");client.DefaultRequestHeaders.Add("X-CSRF-TOKEN",session!.CsrfToken);
   Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync("/api/financials/mock-review/cases/"+Guid.NewGuid()+"/notes")).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await client.GetAsync("/api/financials/mock-review/queue")).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync("/api/financials/mock-review/notes",new{operationId=Guid.NewGuid(),evidenceFingerprint=new string('A',64),note="Mock only",idempotencyKey="mode"})).StatusCode);
  }
 }
}
