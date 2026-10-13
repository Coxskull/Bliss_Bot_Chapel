using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bliss.Domain.Financials;
using Npgsql;
namespace Bliss.Infrastructure.Financials;

public sealed record MockReviewAuthority(Guid QuoteVersionId,FinancialScope Scope,Guid RecipientAccountId,string AdvertiserSubject);
public sealed record MockReviewQueueItem(Guid OperationId,FinancialScope Scope,string Currency,[property:JsonNumberHandling(JsonNumberHandling.WriteAsString|JsonNumberHandling.AllowReadingFromString)]long AmountMinor,string Reason,long NoteCount);
public sealed record MockReviewPage(IReadOnlyList<MockReviewQueueItem> Items,Guid? NextAfter);
public sealed record MockReviewNote(Guid Id,Guid OperationId,string ActorSubject,string EvidenceFingerprint,string Note,DateTimeOffset RecordedAt);
public sealed record MockReviewCase(Guid OperationId,bool RequiresReview,string Reason,string EvidenceFingerprint,string EvidenceJson,IReadOnlyList<MockReviewNote> Notes,bool NotesTruncated=false);
public sealed record MockReviewNoteEvidence(MockReviewNote Note,string EvidenceJson);

// All authorities/actors are resolved by the authenticated application adapter, never payment DTOs.
public sealed class MockFinancialReviewStore(NpgsqlDataSource data)
{
 private static string Scope(NpgsqlCommand command,IReadOnlyList<MockReviewAuthority> authorities)
 {
  if(authorities.Count is <1 or >128)throw new UnauthorizedAccessException("Bounded registered review authority required.");
  var conditions=new List<string>();
  for(var i=0;i<authorities.Count;i++)
  {
   var a=authorities[i];conditions.Add($"(q.id=@v{i} AND q.source_version=@v{i} AND q.merchant_id=@m{i} AND q.module=@module{i} AND q.campaign_id=@c{i} AND q.recipient_account=@r{i} AND q.advertiser_subject=@s{i})");
   command.Parameters.AddWithValue("v"+i,a.QuoteVersionId);command.Parameters.AddWithValue("m"+i,a.Scope.MerchantId);command.Parameters.AddWithValue("module"+i,a.Scope.Module.ToString());command.Parameters.AddWithValue("c"+i,a.Scope.CampaignId);command.Parameters.AddWithValue("r"+i,a.RecipientAccountId);command.Parameters.AddWithValue("s"+i,a.AdvertiserSubject);
  }
  return "("+string.Join(" OR ",conditions)+")";
 }
 public async Task<MockReviewPage> Queue(IReadOnlyList<MockReviewAuthority> authorities,FinancialModule? module,int limit,Guid? after,CancellationToken ct=default)
 {
  if(limit is <1 or >100||module is not null&&!Enum.IsDefined(module.Value))throw new ArgumentException("Bounded queue page/module required.");
  await using var command=data.CreateCommand();var scope=Scope(command,authorities);
  command.CommandText=$"""
   SELECT o.id,q.merchant_id,q.module,q.campaign_id,q.currency,q.amount_minor,
    CASE WHEN EXISTS(SELECT 1 FROM financials.mock_payment_status_events WHERE operation_id=o.id AND state='CANCELED') OR EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=o.id AND provider_status='canceled') THEN 'CANCELLATION_REVIEW' ELSE 'AGED_UNRESOLVED_REVIEW' END,
    (SELECT count(*) FROM financials.mock_review_notes WHERE operation_id=o.id)
   FROM financials.payment_operations o JOIN financials.authorized_quotes q ON q.id=o.quote_id
   WHERE {scope} AND (@module IS NULL OR q.module=@module) AND (@after IS NULL OR o.id>@after)
    AND (EXISTS(SELECT 1 FROM financials.mock_payment_status_events WHERE operation_id=o.id AND state='CANCELED')
     OR EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=o.id AND provider_status='canceled')
     OR (EXISTS(SELECT 1 FROM financials.payment_dispatch_attempts WHERE operation_id=o.id AND started_at<=statement_timestamp()-interval '20 hours')
      AND NOT EXISTS(SELECT 1 FROM financials.payment_dispatch_results WHERE operation_id=o.id AND state='BOUND')
      AND NOT EXISTS(SELECT 1 FROM financials.mock_payment_recoveries WHERE operation_id=o.id)))
   ORDER BY o.id LIMIT @count
   """;
  command.Parameters.AddWithValue("module",NpgsqlTypes.NpgsqlDbType.Text,(object?)module?.ToString()??DBNull.Value);command.Parameters.AddWithValue("after",NpgsqlTypes.NpgsqlDbType.Uuid,(object?)after??DBNull.Value);command.Parameters.AddWithValue("count",limit+1);
  var items=new List<MockReviewQueueItem>();await using var reader=await command.ExecuteReaderAsync(ct);
  while(await reader.ReadAsync(ct))items.Add(new(reader.GetGuid(0),new(reader.GetGuid(1),Enum.Parse<FinancialModule>(reader.GetString(2)),reader.GetGuid(3)),reader.GetString(4),reader.GetInt64(5),reader.GetString(6),reader.GetInt64(7)));
  var more=items.Count>limit;if(more)items.RemoveAt(items.Count-1);return new(items,more?items[^1].OperationId:null);
 }
 public async Task<MockReviewCase> Read(Guid operation,IReadOnlyList<MockReviewAuthority> authorities,CancellationToken ct=default)
 {
  await using var connection=await data.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
  var evidence=await Evidence(connection,tx,operation,authorities,ct);var notes=new List<MockReviewNote>();
  await using(var command=new NpgsqlCommand("SELECT id,operation_id,actor_subject,evidence_fingerprint,note_text,recorded_at FROM financials.mock_review_notes WHERE operation_id=@i ORDER BY recorded_at,id LIMIT 201",connection,tx)){command.Parameters.AddWithValue("i",operation);await using var r=await command.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))notes.Add(Note(r));}
  var truncated=notes.Count>200;if(truncated)notes.RemoveAt(notes.Count-1);await tx.CommitAsync(ct);return Case(operation,evidence,notes) with{NotesTruncated=truncated};
 }
 public async Task<MockReviewNoteEvidence> ReadNote(Guid id,IReadOnlyList<MockReviewAuthority> authorities,CancellationToken ct=default)
 {
  await using var command=data.CreateCommand();var scope=Scope(command,authorities);command.CommandText=$"SELECT n.id,n.operation_id,n.actor_subject,n.evidence_fingerprint,n.note_text,n.recorded_at,n.evidence_json::text FROM financials.mock_review_notes n JOIN financials.payment_operations o ON o.id=n.operation_id JOIN financials.authorized_quotes q ON q.id=o.quote_id WHERE n.id=@i AND {scope}";command.Parameters.AddWithValue("i",id);await using var r=await command.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new UnauthorizedAccessException("Note outside registered authority.");return new(Note(r),r.GetString(6));
 }
 public async Task<MockReviewNote> AddNote(Guid operation,IReadOnlyList<MockReviewAuthority> authorities,string actor,string version,string note,string retryKey,CancellationToken ct=default)
 {
  if(string.IsNullOrWhiteSpace(actor)||actor.Length>200||actor.Any(char.IsControl)||string.IsNullOrWhiteSpace(version)||version.Length!=64||version.Any(x=>!Uri.IsHexDigit(x))||string.IsNullOrWhiteSpace(note)||note.Length>2000||note.Any(x=>char.IsControl(x)&&x is not ('\n' or '\r' or '\t'))||string.IsNullOrWhiteSpace(retryKey)||retryKey.Length>120||retryKey.Any(char.IsControl))throw new ArgumentException("Bounded versioned annotation required.");
  version=version.ToUpperInvariant();await using var connection=await data.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(ct);
  // Authorize before looking up a subject-scoped retry key, to avoid leaking another case.
  await Evidence(connection,tx,operation,authorities,ct);
  await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@k,0))",connection,tx)){gate.Parameters.AddWithValue("k","review-note:"+actor+":"+retryKey);await gate.ExecuteNonQueryAsync(ct);}
  await using(var prior=new NpgsqlCommand("SELECT id,operation_id,actor_subject,evidence_fingerprint,note_text,recorded_at FROM financials.mock_review_notes WHERE actor_subject=@s AND retry_key=@k",connection,tx))
  {prior.Parameters.AddWithValue("s",actor);prior.Parameters.AddWithValue("k",retryKey);await using var r=await prior.ExecuteReaderAsync(ct);if(await r.ReadAsync(ct)){var existing=Note(r);if(existing.OperationId!=operation||existing.EvidenceFingerprint!=version||existing.Note!=note)throw new InvalidOperationException("Note retry conflicts with immutable evidence.");return existing;}}
  // Mock-only conservative freeze. NOWAIT returns a safe conflict during active financial writes.
  // Notes are excluded so independent annotations on the same evidence do not invalidate each other.
  await using(var freeze=new NpgsqlCommand("LOCK TABLE financials.authorized_quotes,financials.payment_operations,financials.payment_retry_keys,financials.payment_dispatch_attempts,financials.payment_dispatch_results,financials.payment_captures,financials.mock_provider_bindings,financials.mock_provider_events,financials.mock_payment_recoveries,financials.mock_payment_status_events,financials.journals,financials.lines,financials.stripe_receipts,financials.refund_requests,financials.refund_results,financials.refund_provider_ids IN SHARE MODE NOWAIT",connection,tx))await freeze.ExecuteNonQueryAsync(ct);
  var evidence=await Evidence(connection,tx,operation,authorities,ct);var snapshot=Case(operation,evidence,[]);
  if(!snapshot.RequiresReview||snapshot.EvidenceFingerprint!=version)throw new InvalidOperationException("Evidence changed or no current review exception; refresh the case.");
  var id=Guid.NewGuid();await using var insert=new NpgsqlCommand("INSERT INTO financials.mock_review_notes(id,operation_id,actor_subject,retry_key,evidence_fingerprint,evidence_json,note_text) VALUES(@id,@i,@s,@k,@v,@e,@n) RETURNING id,operation_id,actor_subject,evidence_fingerprint,note_text,recorded_at",connection,tx);
  insert.Parameters.AddWithValue("id",id);insert.Parameters.AddWithValue("i",operation);insert.Parameters.AddWithValue("s",actor);insert.Parameters.AddWithValue("k",retryKey);insert.Parameters.AddWithValue("v",version);insert.Parameters.AddWithValue("e",NpgsqlTypes.NpgsqlDbType.Jsonb,evidence);insert.Parameters.AddWithValue("n",note);MockReviewNote created;
  await using(var r=await insert.ExecuteReaderAsync(ct)){await r.ReadAsync(ct);created=Note(r);}await tx.CommitAsync(ct);return created;
 }
 private static MockReviewNote Note(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetString(3),r.GetString(4),new DateTimeOffset(r.GetDateTime(5)));
 private static MockReviewCase Case(Guid operation,string evidence,IReadOnlyList<MockReviewNote> notes)
 {using var document=JsonDocument.Parse(evidence);return new(operation,document.RootElement.GetProperty("review_required").GetBoolean(),document.RootElement.GetProperty("reason").GetString()!,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))),evidence,notes);}
 private static async Task<string> Evidence(NpgsqlConnection connection,NpgsqlTransaction tx,Guid operation,IReadOnlyList<MockReviewAuthority> authorities,CancellationToken ct)
 {
  await using var command=new NpgsqlCommand("",connection,tx);var scope=Scope(command,authorities);command.CommandText=$"SELECT financials.mock_review_evidence(o.id)::text FROM financials.payment_operations o JOIN financials.authorized_quotes q ON q.id=o.quote_id WHERE o.id=@i AND {scope}";command.Parameters.AddWithValue("i",operation);return await command.ExecuteScalarAsync(ct) as string??throw new UnauthorizedAccessException("Review case outside registered authority.");
 }
}
