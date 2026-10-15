using Bliss.Domain.CommandGateway;
using Bliss.Infrastructure.CommandGateway;
using Npgsql;
namespace Bliss.Tests.CommandGateway;
public sealed class GatewayPostgresFactAttribute:FactAttribute{public GatewayPostgresFactAttribute(){if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BLISS_GATEWAY_TEST_POSTGRES")))Skip="Explicit isolated gateway PostgreSQL fixture required.";}}
[CollectionDefinition("Gateway inbox isolation",DisableParallelization=true)]public sealed class GatewayInboxIsolation{}
[Collection("Gateway inbox isolation")]
public sealed class GatewayInboxTests
{
 private sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
 private static NpgsqlDataSource Data()=>NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_GATEWAY_TEST_POSTGRES")!);
 private static async Task<(GatewayEnvelope Envelope,GatewayPrincipal Principal,GatewayPolicy Policy)> Fixture(NpgsqlDataSource data)
 {
  var tenant=Guid.NewGuid();var sender=Guid.NewGuid();var recipient=Guid.NewGuid();await using var seed=data.CreateCommand("INSERT INTO command_gateway.agents VALUES(@tenant,@sender),(@tenant,@recipient)");seed.Parameters.AddWithValue("tenant",tenant);seed.Parameters.AddWithValue("sender",sender);seed.Parameters.AddWithValue("recipient",recipient);await seed.ExecuteNonQueryAsync();var now=DateTimeOffset.UtcNow;
  return(new("AGM-A2A/1",Guid.NewGuid(),Guid.NewGuid(),null,"AGM-LAUNCH-011",tenant,sender,recipient,GatewayMessageType.TaskAssignment,"LOCAL_MOCK","local-v1",Guid.NewGuid().ToString(),now,now.AddMinutes(10),"{\"fixture\":\"no execution\"}"),new(true,tenant,sender),new(true,tenant,sender,"local-v1",new HashSet<Guid>{recipient},new HashSet<GatewayMessageType>{GatewayMessageType.TaskAssignment,GatewayMessageType.Ack}));
 }
 private static async Task<long> Count(NpgsqlDataSource data,string table,Guid id){await using var cmd=data.CreateCommand($"SELECT count(*) FROM command_gateway.{table} WHERE message_id=@id");cmd.Parameters.AddWithValue("id",id);return (long)(await cmd.ExecuteScalarAsync())!;}
 [GatewayPostgresFact]public async Task Concurrent_retries_commit_one_message_audit_and_intent_and_survive_adapter_restart()
 {
  await using var data=Data();var f=await Fixture(data);var inbox=new LocalGatewayInbox(data);var receipts=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>inbox.Accept(f.Envelope,f.Principal,f.Policy)));Assert.Single(receipts,r=>!r.Replay);Assert.All(receipts,r=>Assert.Equal(f.Envelope.MessageId,r.MessageId));foreach(var table in new[]{"messages","acceptance_audit","outbox_intents"})Assert.Equal(1,await Count(data,table,f.Envelope.MessageId));
  await using var restarted=Data();Assert.True((await new LocalGatewayInbox(restarted).Accept(f.Envelope,f.Principal,f.Policy)).Replay);await Assert.ThrowsAsync<InvalidOperationException>(()=>inbox.Accept(f.Envelope with{PayloadJson="{\"fixture\":\"changed\"}"},f.Principal,f.Policy));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>inbox.Accept(f.Envelope,f.Principal with{AgentId=Guid.NewGuid()},f.Policy));
 }
 [GatewayPostgresFact]public async Task Causation_requires_same_task_and_correlation_and_audit_failure_rolls_back_all_writes()
 {
  await using var data=Data();var f=await Fixture(data);var inbox=new LocalGatewayInbox(data);await inbox.Accept(f.Envelope,f.Principal,f.Policy);
  var follow=f.Envelope with{MessageId=Guid.NewGuid(),Type=GatewayMessageType.Ack,CausationId=f.Envelope.MessageId,IdempotencyKey=Guid.NewGuid().ToString()};await inbox.Accept(follow,f.Principal,f.Policy);
  await Assert.ThrowsAsync<InvalidOperationException>(()=>inbox.Accept(follow with{MessageId=Guid.NewGuid(),TaskId="different",IdempotencyKey=Guid.NewGuid().ToString()},f.Principal,f.Policy));
  await Assert.ThrowsAsync<InvalidOperationException>(()=>inbox.Accept(follow with{MessageId=Guid.NewGuid(),CorrelationId=Guid.NewGuid(),IdempotencyKey=Guid.NewGuid().ToString()},f.Principal,f.Policy));
  var doomed=f.Envelope with{MessageId=Guid.NewGuid(),IdempotencyKey=Guid.NewGuid().ToString()};
  // Transaction-local fault injection. The check is removed even on failure; tests are isolated.
  await using(var check=data.CreateCommand("ALTER TABLE command_gateway.acceptance_audit ADD CONSTRAINT fixture_reject_audit CHECK(message_id<>'"+doomed.MessageId+"') NOT VALID"))await check.ExecuteNonQueryAsync();
  try{await Assert.ThrowsAsync<PostgresException>(()=>inbox.Accept(doomed,f.Principal,f.Policy));foreach(var table in new[]{"messages","acceptance_audit","outbox_intents"})Assert.Equal(0,await Count(data,table,doomed.MessageId));}
  finally{await using var drop=data.CreateCommand("ALTER TABLE command_gateway.acceptance_audit DROP CONSTRAINT fixture_reject_audit");await drop.ExecuteNonQueryAsync();}
 }
 [GatewayPostgresFact]public async Task Accepted_audit_and_outbox_are_immutable_and_expired_replay_is_refused()
 {
  await using var data=Data();var f=await Fixture(data);await new LocalGatewayInbox(data).Accept(f.Envelope,f.Principal,f.Policy);
  foreach(var table in new[]{"acceptance_audit","outbox_intents"})foreach(var statement in new[]{"DELETE FROM command_gateway."+table,"TRUNCATE command_gateway."+table,"UPDATE command_gateway."+table+" SET tenant_id=tenant_id"}){await using var mutate=data.CreateCommand(statement);await Assert.ThrowsAsync<PostgresException>(async()=>await mutate.ExecuteNonQueryAsync());}
  await Assert.ThrowsAsync<ArgumentException>(()=>new LocalGatewayInbox(data,new FixedClock(f.Envelope.ExpiresAt.AddSeconds(1))).Accept(f.Envelope,f.Principal,f.Policy));
 }
 [GatewayPostgresFact]public async Task Outbox_failure_rolls_back_audit_and_incomplete_historical_receipts_are_not_acknowledged()
 {
  await using var data=Data();var f=await Fixture(data);var inbox=new LocalGatewayInbox(data);
  await using(var check=data.CreateCommand("ALTER TABLE command_gateway.outbox_intents ADD CONSTRAINT fixture_reject_outbox CHECK(message_id<>'"+f.Envelope.MessageId+"') NOT VALID"))await check.ExecuteNonQueryAsync();
  try{await Assert.ThrowsAsync<PostgresException>(()=>inbox.Accept(f.Envelope,f.Principal,f.Policy));foreach(var table in new[]{"messages","acceptance_audit","outbox_intents"})Assert.Equal(0,await Count(data,table,f.Envelope.MessageId));}
  finally{await using var drop=data.CreateCommand("ALTER TABLE command_gateway.outbox_intents DROP CONSTRAINT fixture_reject_outbox");await drop.ExecuteNonQueryAsync();}
  var valid=GatewayEnvelopeValidator.Validate(f.Envelope,f.Principal,f.Policy,DateTimeOffset.UtcNow);
  await using var raw=data.CreateCommand("INSERT INTO command_gateway.messages(message_id,tenant_id,sender_agent_id,recipient_agent_id,task_id,correlation_id,idempotency_key,content_hash,canonical_json,environment) VALUES(@id,@tenant,@sender,@recipient,@task,@correlation,@key,@hash,@json,'LOCAL_MOCK')");
  raw.Parameters.AddWithValue("id",f.Envelope.MessageId);raw.Parameters.AddWithValue("tenant",f.Principal.TenantId);raw.Parameters.AddWithValue("sender",f.Principal.AgentId);raw.Parameters.AddWithValue("recipient",f.Envelope.RecipientAgentId);raw.Parameters.AddWithValue("task",f.Envelope.TaskId);raw.Parameters.AddWithValue("correlation",f.Envelope.CorrelationId);raw.Parameters.AddWithValue("key",f.Envelope.IdempotencyKey);raw.Parameters.AddWithValue("hash",valid.ContentHash);raw.Parameters.AddWithValue("json",valid.CanonicalJson);await raw.ExecuteNonQueryAsync();
  await Assert.ThrowsAsync<InvalidOperationException>(()=>inbox.Accept(f.Envelope,f.Principal,f.Policy));Assert.Equal(0,await Count(data,"acceptance_audit",f.Envelope.MessageId));Assert.Equal(0,await Count(data,"outbox_intents",f.Envelope.MessageId));
 }
 [Fact]public void Hosted_or_wrong_database_store_is_refused(){foreach(var connection in new[]{"Host=database.example.test;Database=gateway_test","Host=127.0.0.1;Database=production"}){using var data=NpgsqlDataSource.Create(connection);Assert.Throws<InvalidOperationException>(()=>new LocalGatewayInbox(data));}}
}
