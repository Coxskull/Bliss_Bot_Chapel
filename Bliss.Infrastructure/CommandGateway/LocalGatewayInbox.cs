using Bliss.Domain.CommandGateway;
using Npgsql;
namespace Bliss.Infrastructure.CommandGateway;
public sealed record GatewayAcceptance(Guid MessageId,string ContentHash,DateTimeOffset AcceptedAt,bool Replay);
// LOCAL fixture adapter only. Not registered in DI, HTTP routes or workers.
public sealed class LocalGatewayInbox
{
 private readonly NpgsqlDataSource data;
 private readonly TimeProvider clock;
 public LocalGatewayInbox(NpgsqlDataSource data,TimeProvider? clock=null)
 {
  var connection=new NpgsqlConnectionStringBuilder(data.ConnectionString);
  if(connection.Host!="127.0.0.1"||connection.Database!="gateway_test")throw new InvalidOperationException("Explicit loopback gateway_test fixture required.");
  this.data=data;this.clock=clock??TimeProvider.System;
 }
 public async Task<GatewayAcceptance> Accept(GatewayEnvelope envelope,GatewayPrincipal principal,GatewayPolicy policy,CancellationToken ct=default)
 {
  var valid=GatewayEnvelopeValidator.Validate(envelope,principal,policy,clock.GetUtcNow());
  await using var connection=await data.OpenConnectionAsync(ct);await using var tx=await connection.BeginTransactionAsync(ct);
  await using(var limits=new NpgsqlCommand("SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='5s'",connection,tx))await limits.ExecuteNonQueryAsync(ct);
  await using(var gate=new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",connection,tx)){gate.Parameters.AddWithValue("key",$"gateway:{principal.TenantId}:{principal.AgentId}:{envelope.IdempotencyKey}");await gate.ExecuteNonQueryAsync(ct);}
  // Revalidate after waiting for the key: expiry/permissions cannot be bypassed by lock delay.
  valid=GatewayEnvelopeValidator.Validate(envelope,principal,policy,clock.GetUtcNow());
  await using(var prior=new NpgsqlCommand("SELECT message_id,content_hash,canonical_json,accepted_at FROM command_gateway.messages WHERE tenant_id=@tenant AND sender_agent_id=@sender AND idempotency_key=@key",connection,tx))
  {
   prior.Parameters.AddWithValue("tenant",principal.TenantId);prior.Parameters.AddWithValue("sender",principal.AgentId);prior.Parameters.AddWithValue("key",envelope.IdempotencyKey);
   await using var reader=await prior.ExecuteReaderAsync(ct);if(await reader.ReadAsync(ct)){if(reader.GetString(1)!=valid.ContentHash||reader.GetString(2)!=valid.CanonicalJson)throw new InvalidOperationException("Retry conflicts with immutable envelope.");return new(reader.GetGuid(0),reader.GetString(1),new DateTimeOffset(reader.GetDateTime(3)),true);}
  }
  if(envelope.CausationId is Guid cause)
  {
   await using var parent=new NpgsqlCommand("SELECT task_id,correlation_id FROM command_gateway.messages WHERE tenant_id=@tenant AND message_id=@cause",connection,tx);parent.Parameters.AddWithValue("tenant",principal.TenantId);parent.Parameters.AddWithValue("cause",cause);await using var reader=await parent.ExecuteReaderAsync(ct);
   if(!await reader.ReadAsync(ct)||reader.GetString(0)!=envelope.TaskId||reader.GetGuid(1)!=envelope.CorrelationId)throw new InvalidOperationException("Causation must belong to the same task/correlation/tenant.");
  }
  await using var insert=new NpgsqlCommand("INSERT INTO command_gateway.messages(message_id,tenant_id,sender_agent_id,recipient_agent_id,task_id,correlation_id,causation_id,idempotency_key,content_hash,canonical_json,environment) VALUES(@id,@tenant,@sender,@recipient,@task,@correlation,@cause,@key,@hash,@json,'LOCAL_MOCK') RETURNING accepted_at",connection,tx);
  insert.Parameters.AddWithValue("id",envelope.MessageId);insert.Parameters.AddWithValue("tenant",principal.TenantId);insert.Parameters.AddWithValue("sender",principal.AgentId);insert.Parameters.AddWithValue("recipient",envelope.RecipientAgentId);insert.Parameters.AddWithValue("task",envelope.TaskId);insert.Parameters.AddWithValue("correlation",envelope.CorrelationId);insert.Parameters.AddWithValue("cause",NpgsqlTypes.NpgsqlDbType.Uuid,(object?)envelope.CausationId??DBNull.Value);insert.Parameters.AddWithValue("key",envelope.IdempotencyKey);insert.Parameters.AddWithValue("hash",valid.ContentHash);insert.Parameters.AddWithValue("json",valid.CanonicalJson);
  var accepted=new DateTimeOffset((DateTime)(await insert.ExecuteScalarAsync(ct))!);
  await using var evidence=new NpgsqlCommand("INSERT INTO command_gateway.acceptance_audit(message_id,tenant_id,actor_agent_id,event_type,content_hash) VALUES(@id,@tenant,@sender,'MESSAGE_ACCEPTED',@hash); INSERT INTO command_gateway.outbox_intents(message_id,tenant_id,recipient_agent_id) VALUES(@id,@tenant,@recipient)",connection,tx);
  evidence.Parameters.AddWithValue("id",envelope.MessageId);evidence.Parameters.AddWithValue("tenant",principal.TenantId);evidence.Parameters.AddWithValue("sender",principal.AgentId);evidence.Parameters.AddWithValue("recipient",envelope.RecipientAgentId);evidence.Parameters.AddWithValue("hash",valid.ContentHash);await evidence.ExecuteNonQueryAsync(ct);
  await tx.CommitAsync(ct);return new(envelope.MessageId,valid.ContentHash,accepted,false);
 }
}
