using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bliss.Domain.CommandGateway;
using Npgsql;
namespace Bliss.Infrastructure.CommandGateway;

public sealed record GatewayMissionEvent(Guid MessageId, Guid ActorAgentId, GatewayTaskSnapshot Snapshot);
public sealed record GatewayOwnerReviewRequest(string Schema, string Mode, string TaskId, Guid CorrelationId,
 Guid RequestMessageId, string CommitHash, string EvidenceHash, int ReportedPassed, int ReportedFailed,
 bool ApprovalGranted = false, bool ExecutionAuthorized = false, bool DeliveredToOwner = false);
public sealed record GatewayMissionStatus(Guid TenantId, string TaskId, Guid CorrelationId, GatewayTaskSnapshot Snapshot,
 bool TenantStopped, IReadOnlyList<GatewayMissionEvent> Events, GatewayOwnerReviewRequest? OwnerReviewRequest);
public sealed record GatewayMissionPacket(GatewayMissionStatus Status, string ArtifactJson, string Sha256);

// Fixture-only read boundary. Unregistered: no API, notification, owner decision or execution.
public sealed class LocalGatewayMissionReview
{
 private readonly NpgsqlDataSource data;
 public LocalGatewayMissionReview(NpgsqlDataSource data)
 {
  var c = new NpgsqlConnectionStringBuilder(data.ConnectionString);
  if (c.Host != "127.0.0.1" || c.Database != "gateway_test")
   throw new InvalidOperationException("Loopback gateway_test required.");
  this.data = data;
 }
 public async Task<GatewayMissionPacket> Read(string taskId, Guid correlationId, GatewayPrincipal principal,
  GatewayTaskPolicy policy, CancellationToken ct = default)
 {
  GatewayTaskLoop.Authorize(principal, policy);
  if (string.IsNullOrWhiteSpace(taskId) || taskId.Length > 80 || taskId.Any(char.IsControl) || correlationId == Guid.Empty)
   throw new ArgumentException("Bounded task and correlation required.");
  await using var c = await data.OpenConnectionAsync(ct);
  await using var tx = await c.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
  await using (var limits = new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout='5s'", c, tx))
   await limits.ExecuteNonQueryAsync(ct);
  GatewayTaskSnapshot state; bool stopped;
  await using (var cmd = new NpgsqlCommand("""
   SELECT r.stage,r.turns,r.last_message_id,r.commit_hash,r.evidence_hash,r.passed,r.failed,r.verdict,x.stopped
   FROM command_gateway.task_runs r JOIN command_gateway.execution_control x ON x.tenant_id=r.tenant_id
   WHERE r.tenant_id=@tenant AND r.task_id=@task AND r.correlation_id=@correlation
    AND r.executive_agent_id=@executive AND r.engineering_agent_id=@engineering
   """, c, tx))
  {
   Bind(cmd, taskId, correlationId, principal.TenantId);
   cmd.Parameters.AddWithValue("executive", policy.ExecutiveAgentId);
   cmd.Parameters.AddWithValue("engineering", policy.EngineeringAgentId);
   await using var r = await cmd.ExecuteReaderAsync(ct);
   if (!await r.ReadAsync(ct)) throw new UnauthorizedAccessException("Scoped current task role binding required.");
   state = Snapshot(r); stopped = r.GetBoolean(8);
  }
  var events = new List<GatewayMissionEvent>();
  await using (var cmd = new NpgsqlCommand("""
   SELECT stage,turn,message_id,commit_hash,evidence_hash,passed,failed,verdict,actor_agent_id
   FROM command_gateway.task_events WHERE tenant_id=@tenant AND task_id=@task AND correlation_id=@correlation
   ORDER BY turn LIMIT 9
   """, c, tx))
  {
   Bind(cmd, taskId, correlationId, principal.TenantId);
   await using var r = await cmd.ExecuteReaderAsync(ct);
   while (await r.ReadAsync(ct)) events.Add(new(r.GetGuid(2), r.GetGuid(8), Snapshot(r)));
  }
  if (state.Turns < 1 || state.Turns > 8 || events.Count != state.Turns ||
   events.Where((e, i) => e.Snapshot.Turns != i + 1).Any() || events[^1].Snapshot != state)
   throw new InvalidOperationException("Task snapshot and bounded immutable history must agree.");
  GatewayOwnerReviewRequest? request = null;
  if (!stopped && state.Stage == "APPROVAL_REQUESTED" && state.Turns == 8 && state.Verdict == "PASS" &&
   state.Failed == 0 && state.Passed > 0 && state.LastMessageId is Guid message &&
   state.CommitHash is string commit && state.EvidenceHash is string evidence)
   request = new("AGM-OWNER-REVIEW-REQUEST/1", "LOCAL_MOCK", taskId, correlationId, message, commit, evidence,
    state.Passed.Value, state.Failed.Value);
  var status = new GatewayMissionStatus(principal.TenantId, taskId, correlationId, state, stopped, events.AsReadOnly(), request);
  var artifact = JsonSerializer.Serialize(new { schema = "AGM-MISSION-STATUS/1", mode = "LOCAL_MOCK", status,
   independentlyVerified = false, approvalGranted = false, executionAuthorized = false, deliveredToOwner = false });
  await tx.CommitAsync(ct);
  return new(status, artifact, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(artifact))));
 }
 private static GatewayTaskSnapshot Snapshot(NpgsqlDataReader r) => new(r.GetString(0), r.GetInt32(1),
  r.IsDBNull(2) ? null : r.GetGuid(2), r.IsDBNull(3) ? null : r.GetString(3),
  r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetInt32(5),
  r.IsDBNull(6) ? null : r.GetInt32(6), r.IsDBNull(7) ? null : r.GetString(7));
 private static void Bind(NpgsqlCommand cmd, string task, Guid correlation, Guid tenant)
 {
  cmd.Parameters.AddWithValue("tenant", tenant); cmd.Parameters.AddWithValue("task", task);
  cmd.Parameters.AddWithValue("correlation", correlation);
 }
}
