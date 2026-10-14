using System.Security.Claims;
using Bliss.Api.Security;
using Bliss.Domain.Financials;
using Bliss.Infrastructure.Financials;
using Npgsql;
namespace Bliss.Api.Financials;
public sealed class FinancialMockReviewWorkflow(FinancialPreparationOptions options,BlissAuthenticationOptions auth)
{
 private (NpgsqlDataSource Data,MockReviewAuthority[] Authorities,string Subject) Context(ClaimsPrincipal user)
 {
  var subject=PaymentOperationStore.AuthenticatedSubject(user);
  if(!auth.Enabled||user.Identity?.IsAuthenticated!=true||!(user.IsInRole(auth.OperatorRole)||user.IsInRole(auth.AdminRole)))throw new UnauthorizedAccessException("Authenticated staff reviewer required.");
  if(!options.Enabled||options.Mode!="Mock")throw new InvalidOperationException("Mock review is disabled.");
  var grants=options.MockReviewers.Where(x=>x.Subject==subject).Select(x=>x.Scope).ToHashSet();
  var authorities=options.Bindings.Where(x=>grants.Contains(x.Scope)).Select(x=>new MockReviewAuthority(x.SourceVersionId,x.Scope,x.RecipientAccountId,x.AdvertiserSubject)).Distinct().ToArray();
  if(authorities.Length is <1 or >128)throw new UnauthorizedAccessException("Explicit registered review scopes required.");
  var c=new NpgsqlConnectionStringBuilder(options.FixtureConnection);if(c.Host!="127.0.0.1"||c.Database!="financial_test")throw new InvalidOperationException("Only explicit loopback review fixture is supported.");return(NpgsqlDataSource.Create(c.ConnectionString),authorities,subject);
 }
 public async Task<MockReviewPage> Queue(ClaimsPrincipal user,string? module,int limit,Guid? after,CancellationToken ct)
 {
  var context=Context(user);await using var data=context.Data;FinancialModule? selected=null;
  if(module is not null){if(!Enum.TryParse<FinancialModule>(module,false,out var parsed)||!Enum.IsDefined(parsed))throw new ArgumentException("Unknown financial module.");selected=parsed;}
  return await new MockFinancialReviewStore(data).Queue(context.Authorities,selected,limit,after,ct);
 }
 public async Task<MockReviewCase> Read(ClaimsPrincipal user,Guid operation,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).Read(operation,context.Authorities,ct);}
 public async Task<MockReviewNotesPage> Notes(ClaimsPrincipal user,Guid operation,int limit,Guid? after,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).Notes(operation,context.Authorities,limit,after,ct);}
 public async Task<MockReviewNoteEvidence> NoteEvidence(ClaimsPrincipal user,Guid note,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).ReadNote(note,context.Authorities,ct);}
 public async Task<MockReviewNote> AddNote(ClaimsPrincipal user,Guid operation,string fingerprint,string note,string key,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).AddNote(operation,context.Authorities,context.Subject,fingerprint,note,key,ct);}
}
