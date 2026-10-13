using System.Security.Claims;
using System.Text;
using Bliss.Api.Security;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Api.Financials;
public sealed record ModuleFinancialReading(PreparedPaymentOperation Operation,bool SourceApprovalCurrent,bool SimulatedCaptureRecorded,bool PaymentCompleted=false);
public sealed class FinancialMockWorkflow(FinancialPreparationOptions options,BlissAuthenticationOptions auth,AcceptedFinancialQuoteImporter importer)
{
 private NpgsqlDataSource Data(ClaimsPrincipal user,bool staff)
 {
  PaymentOperationStore.AuthenticatedSubject(user);
  if(!auth.Enabled||user.Identity?.IsAuthenticated!=true||!(staff?(user.IsInRole(auth.OperatorRole)||user.IsInRole(auth.AdminRole)):user.IsInRole(auth.AdvertiserRole)))throw new UnauthorizedAccessException("Financial role required.");
  if(!options.Enabled||options.Mode!="Mock")throw new InvalidOperationException("Mock financials are disabled.");var c=new NpgsqlConnectionStringBuilder(options.FixtureConnection);if(c.Host!="127.0.0.1"||c.Database!="financial_test")throw new InvalidOperationException("Loopback fixture required.");return NpgsqlDataSource.Create(c.ConnectionString);
 }
 private FundingBinding Binding(PreparedPaymentOperation op)
 {var binding=options.Bindings.Where(x=>x.SourceVersionId==op.QuoteId&&x.Scope==op.Scope&&x.RecipientAccountId==op.RecipientAccountId).ToArray();if(binding.Length!=1)throw new UnauthorizedAccessException("Funding operation outside registered authority.");return binding[0];}
 public async Task<Guid> Webhook(ClaimsPrincipal user,Guid operationId,string rawJson,string signature,CancellationToken ct)
 {
  await using var data=Data(user,true);var store=new MockPaymentCaptureStore(data);var op=await store.Operation(operationId,ct);var b=Binding(op);var current=await importer.Read(b,ct);
  if(current.AmountMinor!=op.AmountMinor||b.PayerAccountId is null||b.RecipientOwnerId is null||string.IsNullOrWhiteSpace(options.MockSigningSecret))throw new InvalidOperationException("Current mock authority/accounts/signature configuration required.");
  var payment=StripeSandboxVerifier.VerifyPayment(Encoding.UTF8.GetBytes(rawJson),signature,options.MockSigningSecret,DateTimeOffset.UtcNow,new("pi_mock_"+op.Id.ToString("N"),op.Scope,op.Currency,op.AmountMinor));return await store.Capture(op,payment,b.PayerAccountId.Value,b.RecipientOwnerId.Value,ct);
 }
 public async Task<ReservedRefund> Refund(ClaimsPrincipal user,Guid operationId,long amount,string key,CancellationToken ct)
 {await using var data=Data(user,true);var store=new MockPaymentCaptureStore(data);var op=await store.Operation(operationId,ct);Binding(op);return await new RefundReconciliationStore(data,auth.OperatorRole,auth.AdminRole).Reserve(user,op.Scope,"pi_mock_"+op.Id.ToString("N"),amount,key,ct);}
 public async Task<Guid?> Reconcile(ClaimsPrincipal user,Guid refund,string status,CancellationToken ct)
 {await using var data=Data(user,true);var store=new MockPaymentCaptureStore(data);var op=await store.Operation(await store.OperationForRefund(refund,ct),ct);Binding(op);return await new RefundReconciliationStore(data,auth.OperatorRole,auth.AdminRole).Reconcile(user,op.Scope,refund,"re_mock_"+refund.ToString("N"),status,ct);}
 public async Task<IReadOnlyList<ModuleFinancialReading>> Read(ClaimsPrincipal user,FinancialModule module,CancellationToken ct)
 {
  await using var data=Data(user,false);if(!Guid.TryParse(user.FindFirst(auth.AdvertiserIdClaimType)?.Value,out var advertiser))throw new UnauthorizedAccessException("Advertiser binding required.");var subject=PaymentOperationStore.AuthenticatedSubject(user);var store=new MockPaymentCaptureStore(data);var results=new List<ModuleFinancialReading>();
  foreach(var op in await store.Owned(subject,module,ct)){var b=Binding(op);if(b.AdvertiserId!=advertiser||b.AdvertiserSubject!=subject)continue;var approved=true;try{await importer.Read(b,ct);}catch(InvalidOperationException){approved=false;}results.Add(new(op,approved,await store.Captured(op.Id,ct)!=null));}return results;
 }
}
