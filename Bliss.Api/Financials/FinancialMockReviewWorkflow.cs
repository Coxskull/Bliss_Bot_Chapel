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
 public async Task<object> RecoveryContext(ClaimsPrincipal user)
 {var context=Context(user);await using var data=context.Data;return new{actorKey=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("financial-review-recovery-v1\0"+context.Subject)))};}
 public async Task<MockReviewPage> Queue(ClaimsPrincipal user,string? module,int limit,Guid? after,CancellationToken ct)
 {
  var context=Context(user);await using var data=context.Data;FinancialModule? selected=null;
  if(module is not null){if(!Enum.TryParse<FinancialModule>(module,false,out var parsed)||!Enum.IsDefined(parsed))throw new ArgumentException("Unknown financial module.");selected=parsed;}
  return await new MockFinancialReviewStore(data).Queue(context.Authorities,selected,limit,after,ct);
 }
 public async Task<MockReviewCase> Read(ClaimsPrincipal user,Guid operation,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).Read(operation,context.Authorities,ct);}
 public async Task<object> ExportEvidence(ClaimsPrincipal user,Guid operation,string version,CancellationToken ct)
 {
  if(version is null||version.Length!=64||version.Any(c=>!(c>='0'&&c<='9'||c>='A'&&c<='F')))throw new ArgumentException("Bounded evidence version required.");
  var snapshot=await Read(user,operation,ct);if(snapshot.EvidenceFingerprint!=version)throw new InvalidOperationException("Evidence changed; refresh case before export.");
  var artifact=System.Text.Json.JsonSerializer.Serialize(new{schema="BLISS-MOCK-CASE-EVIDENCE/1",mode="MOCK",stripeAccountStatus=FinancialAccountPolicy.StripeAccountStatus,paymentCompleted=false,approvalGranted=false,holdReleased=false,operationId=snapshot.OperationId,evidenceFingerprint=snapshot.EvidenceFingerprint,evidenceJson=snapshot.EvidenceJson,includesNotes=false});
  return new{artifactJson=artifact,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(artifact)))};
 }
 public async Task<MockReviewNotesPage> Notes(ClaimsPrincipal user,Guid operation,int limit,Guid? after,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).Notes(operation,context.Authorities,limit,after,ct);}
 public async Task<MockReviewNoteEvidence> NoteEvidence(ClaimsPrincipal user,Guid note,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).ReadNote(note,context.Authorities,ct);}
 public async Task<MockReviewNote?> FindRetry(ClaimsPrincipal user,Guid operation,Guid retryKey,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).FindRetry(operation,context.Authorities,context.Subject,retryKey,ct);}
 public async Task<MockReviewNote> AddNote(ClaimsPrincipal user,Guid operation,string fingerprint,string note,string key,CancellationToken ct)
 {var context=Context(user);await using var data=context.Data;return await new MockFinancialReviewStore(data).AddNote(operation,context.Authorities,context.Subject,fingerprint,note,key,ct);}
}
