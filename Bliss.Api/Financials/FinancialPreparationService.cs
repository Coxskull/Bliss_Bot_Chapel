using System.Security.Claims;
using Bliss.Api.Security;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Api.Financials;

public sealed class FinancialPreparationOptions
{
 public bool Enabled {get;set;}
 public string Mode {get;set;}="Mock";
 public string MockSigningSecret {get;set;}="";
 public string FixtureConnection {get;set;}="";
 public List<FundingBinding> Bindings {get;set;}=[];
}
public interface IFinancialPreparation
{
 Task<PreparedPaymentOperation> Prepare(ClaimsPrincipal user,Guid sourceQuoteId,string retryKey,CancellationToken ct);
}
public sealed class FinancialPreparationService(AcceptedFinancialQuoteImporter importer,FinancialPreparationOptions options,BlissAuthenticationOptions auth):IFinancialPreparation
{
 public async Task<PreparedPaymentOperation> Prepare(ClaimsPrincipal user,Guid sourceQuoteId,string retryKey,CancellationToken ct)
 {
  if(!auth.Enabled||user.Identity?.IsAuthenticated!=true||!user.IsInRole(auth.AdvertiserRole))throw new UnauthorizedAccessException("Authenticated advertiser required.");
  if(!options.Enabled||options.Mode!="Mock")throw new InvalidOperationException("Financial initiation is disabled; only isolated mock preparation is implemented.");
  var subject=PaymentOperationStore.AuthenticatedSubject(user);
  if(string.IsNullOrWhiteSpace(subject)||!Guid.TryParse(user.FindFirst(auth.AdvertiserIdClaimType)?.Value,out var advertiser))throw new UnauthorizedAccessException("Trusted subject and advertiser binding required.");
  var bindings=options.Bindings.Where(x=>x.SourceQuoteId==sourceQuoteId&&x.AdvertiserSubject==subject&&x.AdvertiserId==advertiser).ToArray();
  if(bindings.Length!=1)throw new UnauthorizedAccessException("Quote unavailable to advertiser.");
  var connection=new NpgsqlConnectionStringBuilder(options.FixtureConnection);
  if(connection.Host!="127.0.0.1"||connection.Database!="financial_test")throw new InvalidOperationException("Only explicit loopback financial fixture is supported.");
  var snapshot=await importer.Read(bindings[0],ct);
  await using var data=NpgsqlDataSource.Create(connection.ConnectionString);var store=new PaymentOperationStore(data);
  await store.FreezeQuote(snapshot,ct);return await store.Prepare(user,snapshot.Id,retryKey,ct);
 }
}
