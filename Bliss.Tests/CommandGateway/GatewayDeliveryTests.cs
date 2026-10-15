using Bliss.Domain.CommandGateway;
using Bliss.Infrastructure.CommandGateway;
using Npgsql;
namespace Bliss.Tests.CommandGateway;
[Collection("Gateway inbox isolation")]
public sealed class GatewayDeliveryTests
{
 private sealed class FixedClock(DateTimeOffset now):TimeProvider{public override DateTimeOffset GetUtcNow()=>now;}
 internal sealed record Fixture(GatewayEnvelope Envelope,GatewayPrincipal Sender,GatewayPrincipal Recipient,GatewayDeliveryPolicy SenderPolicy,GatewayDeliveryPolicy RecipientPolicy);
 private static NpgsqlDataSource Data()=>NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_GATEWAY_TEST_POSTGRES")!);
 internal static async Task<Fixture> Prepare(NpgsqlDataSource data,bool expired=false)
 {
  var tenant=Guid.NewGuid();var sender=Guid.NewGuid();var recipient=Guid.NewGuid();await using var seed=data.CreateCommand("INSERT INTO command_gateway.agents VALUES(@tenant,@sender),(@tenant,@recipient)");seed.Parameters.AddWithValue("tenant",tenant);seed.Parameters.AddWithValue("sender",sender);seed.Parameters.AddWithValue("recipient",recipient);await seed.ExecuteNonQueryAsync();await using(var control=data.CreateCommand("INSERT INTO command_gateway.execution_control(tenant_id,owner_agent_id,capacity) VALUES(@tenant,@owner,100)")){control.Parameters.AddWithValue("tenant",tenant);control.Parameters.AddWithValue("owner",sender);await control.ExecuteNonQueryAsync();}var now=expired?DateTimeOffset.UtcNow.AddMinutes(-2):DateTimeOffset.UtcNow;
  var envelope=new GatewayEnvelope("AGM-A2A/1",Guid.NewGuid(),Guid.NewGuid(),null,"AGM-LAUNCH-011",tenant,sender,recipient,GatewayMessageType.TaskAssignment,"LOCAL_MOCK","fixture-v1",Guid.NewGuid().ToString(),now,expired?now.AddMinutes(1):now.AddMinutes(10),"{\"fixture\":\"local only\"}");var principal=new GatewayPrincipal(true,tenant,sender);var policy=new GatewayPolicy(true,tenant,sender,"fixture-v1",new HashSet<Guid>{recipient},new HashSet<GatewayMessageType>{GatewayMessageType.TaskAssignment});await new LocalGatewayInbox(data,expired?new FixedClock(now):null).Accept(envelope,principal,policy);
  return new(envelope,principal,new(true,tenant,recipient),new(true,tenant,sender,new HashSet<Guid>{recipient},true),new(true,tenant,recipient,new HashSet<Guid>{sender},true));
 }
 private static async Task ExpireLease(NpgsqlDataSource data,Guid id){await using var cmd=data.CreateCommand("UPDATE command_gateway.delivery_state SET lease_until=clock_timestamp()-interval '1 second' WHERE message_id=@id");cmd.Parameters.AddWithValue("id",id);await cmd.ExecuteNonQueryAsync();}
 [GatewayPostgresFact]public async Task Concurrent_claim_has_one_winner_and_expired_lease_recovery_fences_old_worker()
 {
  await using var data=Data();var f=await Prepare(data);var delivery=new LocalGatewayDelivery(data);var claims=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid())));var first=Assert.Single(claims.OfType<GatewayDeliveryLease>());Assert.Equal(1,first.Attempt);await ExpireLease(data,first.MessageId);
  await using var reopened=Data();var recovered=(await new LocalGatewayDelivery(reopened).Claim(first.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()))!;Assert.True(recovered.Fence>first.Fence);Assert.Equal(first.ExecutionKey,recovered.ExecutionKey);Assert.Equal(2,recovered.Attempt);Assert.False(await delivery.Finish(first,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Delivered));Assert.True(await delivery.Finish(recovered,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Delivered));Assert.False(await delivery.Finish(recovered,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Delivered));Assert.Null(await delivery.Claim(first.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));Assert.False(await delivery.Cancel(first.MessageId,f.Sender,f.SenderPolicy));
 }
 [GatewayPostgresFact]public async Task Cancellation_invalidates_active_lease_and_scope_and_peer_denials_precede_changes()
 {
  await using var data=Data();var f=await Prepare(data);var delivery=new LocalGatewayDelivery(data);var lease=(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()))!;
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>delivery.Cancel(lease.MessageId,f.Sender,f.SenderPolicy with{CanCancel=false}));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>delivery.Claim(lease.MessageId,f.Sender,f.SenderPolicy,Guid.NewGuid()));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>delivery.Receipt(lease.MessageId,f.Recipient,f.RecipientPolicy with{AllowedPeers=new HashSet<Guid>()}));
  var foreign=await Prepare(data);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>delivery.Receipt(lease.MessageId,foreign.Recipient,foreign.RecipientPolicy));Assert.True(await delivery.Cancel(lease.MessageId,f.Sender,f.SenderPolicy));Assert.False(await delivery.Finish(lease,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Delivered));Assert.Null(await delivery.Claim(lease.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));Assert.Equal("CANCELED",(await delivery.Receipt(lease.MessageId,f.Recipient,f.RecipientPolicy)).State);
 }
 [GatewayPostgresFact]public async Task Three_attempt_limit_dead_letters_and_unknown_outcome_requires_reconciliation()
 {
  await using var data=Data();var f=await Prepare(data);var delivery=new LocalGatewayDelivery(data);for(var i=1;i<=3;i++){var lease=(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()))!;Assert.Equal(i,lease.Attempt);Assert.True(await delivery.Finish(lease,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Retry));}Assert.Equal("DEAD_LETTER",(await delivery.Receipt(f.Envelope.MessageId,f.Sender,f.SenderPolicy)).State);Assert.Null(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));
  var timedOut=await Prepare(data);for(var i=1;i<=3;i++){var lease=(await delivery.Claim(timedOut.Envelope.MessageId,timedOut.Recipient,timedOut.RecipientPolicy,Guid.NewGuid()))!;Assert.Equal(i,lease.Attempt);await ExpireLease(data,lease.MessageId);}Assert.Null(await delivery.Claim(timedOut.Envelope.MessageId,timedOut.Recipient,timedOut.RecipientPolicy,Guid.NewGuid()));Assert.Equal("DEAD_LETTER",(await delivery.Receipt(timedOut.Envelope.MessageId,timedOut.Sender,timedOut.SenderPolicy)).State);
  var unknown=await Prepare(data);var held=(await delivery.Claim(unknown.Envelope.MessageId,unknown.Recipient,unknown.RecipientPolicy,Guid.NewGuid()))!;Assert.True(await delivery.Finish(held,unknown.Recipient,unknown.RecipientPolicy,GatewayDeliveryOutcome.Unknown));Assert.Equal("UNKNOWN",(await delivery.Receipt(held.MessageId,unknown.Sender,unknown.SenderPolicy)).State);Assert.Null(await delivery.Claim(held.MessageId,unknown.Recipient,unknown.RecipientPolicy,Guid.NewGuid()));
 }
 [GatewayPostgresFact]public async Task Expired_message_has_readable_receipt_but_no_delivery_lease()
 {
  await using var data=Data();var f=await Prepare(data,true);var delivery=new LocalGatewayDelivery(data);Assert.Null(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));var receipt=await delivery.Receipt(f.Envelope.MessageId,f.Sender,f.SenderPolicy);Assert.Equal("EXPIRED",receipt.State);Assert.Equal(0,receipt.Attempts);
 }
 [GatewayPostgresFact]public async Task Delivery_audit_failure_rolls_back_state_and_event_history_is_immutable()
 {
  await using var data=Data();var f=await Prepare(data);var delivery=new LocalGatewayDelivery(data);
  await using(var add=data.CreateCommand("ALTER TABLE command_gateway.delivery_events ADD CONSTRAINT fixture_event_fault CHECK(message_id<>'"+f.Envelope.MessageId+"') NOT VALID"))await add.ExecuteNonQueryAsync();
  try{await Assert.ThrowsAsync<PostgresException>(()=>delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));Assert.Equal(0,(await delivery.Receipt(f.Envelope.MessageId,f.Sender,f.SenderPolicy)).Attempts);}
  finally{await using var drop=data.CreateCommand("ALTER TABLE command_gateway.delivery_events DROP CONSTRAINT fixture_event_fault");await drop.ExecuteNonQueryAsync();}
  await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid());foreach(var sql in new[]{"UPDATE command_gateway.delivery_events SET fence=fence","DELETE FROM command_gateway.delivery_events","TRUNCATE command_gateway.delivery_events"}){await using var cmd=data.CreateCommand(sql);await Assert.ThrowsAsync<PostgresException>(async()=>await cmd.ExecuteNonQueryAsync());}
 }
 [Fact]public void Delivery_store_rejects_hosted_connection(){using var data=NpgsqlDataSource.Create("Host=database.example.test;Database=gateway_test");Assert.Throws<InvalidOperationException>(()=>new LocalGatewayDelivery(data));}
}
