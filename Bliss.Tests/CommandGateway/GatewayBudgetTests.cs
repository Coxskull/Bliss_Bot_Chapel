using Bliss.Domain.CommandGateway;
using Bliss.Infrastructure.CommandGateway;
using Npgsql;
namespace Bliss.Tests.CommandGateway;
[Collection("Gateway inbox isolation")]
public sealed class GatewayBudgetTests
{
 private static NpgsqlDataSource Data()=>NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_GATEWAY_TEST_POSTGRES")!);
 private static GatewayControlPolicy Owner(GatewayDeliveryTests.Fixture f)=>new(true,f.Sender.TenantId,f.Sender.AgentId,true,false);
 private static GatewayControlPolicy Reconciler(GatewayDeliveryTests.Fixture f)=>new(true,f.Recipient.TenantId,f.Recipient.AgentId,false,true);
 private static async Task<long> Scalar(NpgsqlDataSource data,string expression,Guid tenant){await using var cmd=data.CreateCommand("SELECT "+expression+" FROM command_gateway.execution_control WHERE tenant_id=@tenant");cmd.Parameters.AddWithValue("tenant",tenant);return (long)(await cmd.ExecuteScalarAsync())!;}
 private static async Task<GatewayEnvelope> Another(NpgsqlDataSource data,GatewayDeliveryTests.Fixture f){var m=f.Envelope with{MessageId=Guid.NewGuid(),IdempotencyKey=Guid.NewGuid().ToString()};await new LocalGatewayInbox(data).Accept(m,f.Sender,new(true,f.Sender.TenantId,f.Sender.AgentId,"fixture-v1",new HashSet<Guid>{f.Recipient.AgentId},new HashSet<GatewayMessageType>{GatewayMessageType.TaskAssignment}));return m;}
 [GatewayPostgresFact]public async Task Concurrent_distinct_messages_cannot_over_reserve_and_claim_retry_does_not_charge_again()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);await using(var cmd=data.CreateCommand("UPDATE command_gateway.execution_control SET capacity=5 WHERE tenant_id=@tenant")){cmd.Parameters.AddWithValue("tenant",f.Sender.TenantId);await cmd.ExecuteNonQueryAsync();}
  var messages=new List<GatewayEnvelope>{f.Envelope};for(var i=1;i<12;i++)messages.Add(await Another(data,f));var delivery=new LocalGatewayDelivery(data);var policy=f.RecipientPolicy with{ReservationUnits=2};
  var claims=await Task.WhenAll(messages.Select(async m=>{try{return await delivery.Claim(m.MessageId,f.Recipient,policy,Guid.NewGuid());}catch(InvalidOperationException){return null;}}));Assert.Equal(2,claims.OfType<GatewayDeliveryLease>().Count());Assert.Equal(4,await Scalar(data,"held",f.Sender.TenantId));Assert.Equal(0,await Scalar(data,"spent",f.Sender.TenantId));var lease=claims.OfType<GatewayDeliveryLease>().First();Assert.Null(await delivery.Claim(lease.MessageId,f.Recipient,policy,Guid.NewGuid()));Assert.Equal(4,await Scalar(data,"held",f.Sender.TenantId));
 }
 [GatewayPostgresFact]public async Task Unknown_stays_held_and_known_cost_reconciliation_is_scoped_and_idempotent()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var delivery=new LocalGatewayDelivery(data);var budget=new LocalGatewayBudget(data);var lease=(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy with{ReservationUnits=7},Guid.NewGuid()))!;Assert.True(await delivery.Finish(lease,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Unknown));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>budget.Reconcile(lease.MessageId,lease.Fence,3,f.Recipient,Reconciler(f) with{CanReconcile=false}));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>budget.Reconcile(lease.MessageId,lease.Fence,3,f.Sender,Owner(f) with{CanReconcile=true}));var foreign=await GatewayDeliveryTests.Prepare(data);await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>budget.Reconcile(lease.MessageId,lease.Fence,3,foreign.Recipient,Reconciler(foreign)));
  Assert.True(await budget.Reconcile(lease.MessageId,lease.Fence,null,f.Recipient,Reconciler(f)));Assert.False(await budget.Reconcile(lease.MessageId,lease.Fence,null,f.Recipient,Reconciler(f)));Assert.Equal(7,await Scalar(data,"held",f.Sender.TenantId));await Assert.ThrowsAsync<InvalidOperationException>(()=>budget.Reconcile(lease.MessageId,lease.Fence,8,f.Recipient,Reconciler(f)));Assert.Equal(7,await Scalar(data,"held",f.Sender.TenantId));
  var settlements=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>budget.Reconcile(lease.MessageId,lease.Fence,3,f.Recipient,Reconciler(f))));Assert.Single(settlements,x=>x);Assert.Equal(0,await Scalar(data,"held",f.Sender.TenantId));Assert.Equal(3,await Scalar(data,"spent",f.Sender.TenantId));await Assert.ThrowsAsync<InvalidOperationException>(()=>budget.Reconcile(lease.MessageId,lease.Fence,4,f.Recipient,Reconciler(f)));
 }
 [GatewayPostgresFact]public async Task Stop_is_owner_only_fences_active_completion_preserves_holds_and_other_tenants()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var other=await GatewayDeliveryTests.Prepare(data);var delivery=new LocalGatewayDelivery(data);var budget=new LocalGatewayBudget(data);var lease=(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()))!;
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>budget.Stop(f.Sender,Owner(f) with{CanStop=false}));await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>budget.Stop(f.Recipient,Reconciler(f) with{CanStop=true}));Assert.True(await budget.Stop(f.Sender,Owner(f)));Assert.False(await budget.Stop(f.Sender,Owner(f)));Assert.False(await delivery.Finish(lease,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Delivered));Assert.Null(await delivery.Claim(lease.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));Assert.Equal(1,await Scalar(data,"held",f.Sender.TenantId));Assert.NotNull(await delivery.Claim(other.Envelope.MessageId,other.Recipient,other.RecipientPolicy,Guid.NewGuid()));Assert.True(await budget.Reconcile(lease.MessageId,lease.Fence,null,f.Recipient,Reconciler(f)));Assert.Equal(1,await Scalar(data,"held",f.Sender.TenantId));
 }
 [GatewayPostgresFact]public async Task Concurrent_stop_and_claims_leave_no_post_stop_dispatch_or_completion()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var delivery=new LocalGatewayDelivery(data);var budget=new LocalGatewayBudget(data);var messages=new List<GatewayEnvelope>{f.Envelope};for(var i=1;i<12;i++)messages.Add(await Another(data,f));
  await using var gate=await data.OpenConnectionAsync();await using var tx=await gate.BeginTransactionAsync();await using(var cmd=new NpgsqlCommand("SELECT tenant_id FROM command_gateway.execution_control WHERE tenant_id=@tenant FOR UPDATE",gate,tx)){cmd.Parameters.AddWithValue("tenant",f.Sender.TenantId);await cmd.ExecuteScalarAsync();}
  var claims=messages.Select(m=>delivery.Claim(m.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid())).ToArray();var stop=budget.Stop(f.Sender,Owner(f));await tx.CommitAsync();var leases=await Task.WhenAll(claims);Assert.True(await stop);
  foreach(var lease in leases.OfType<GatewayDeliveryLease>())Assert.False(await delivery.Finish(lease,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Delivered));foreach(var m in messages)Assert.Null(await delivery.Claim(m.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));Assert.Equal(leases.OfType<GatewayDeliveryLease>().LongCount(),await Scalar(data,"held",f.Sender.TenantId));
 }
 [GatewayPostgresFact]public async Task Budget_and_stop_audit_faults_roll_back_and_control_history_rejects_mutation()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var delivery=new LocalGatewayDelivery(data);var budget=new LocalGatewayBudget(data);
  await using(var cmd=data.CreateCommand("ALTER TABLE command_gateway.control_events ADD CONSTRAINT fixture_control_fault CHECK(tenant_id<>'"+f.Sender.TenantId+"') NOT VALID"))await cmd.ExecuteNonQueryAsync();
  try{await Assert.ThrowsAsync<PostgresException>(()=>delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));Assert.Equal(0,await Scalar(data,"held",f.Sender.TenantId));Assert.Equal(0,(await delivery.Receipt(f.Envelope.MessageId,f.Sender,f.SenderPolicy)).Attempts);await Assert.ThrowsAsync<PostgresException>(()=>budget.Stop(f.Sender,Owner(f)));Assert.Equal(0,await Scalar(data,"epoch",f.Sender.TenantId));}
  finally{await using var cmd=data.CreateCommand("ALTER TABLE command_gateway.control_events DROP CONSTRAINT fixture_control_fault");await cmd.ExecuteNonQueryAsync();}
  var lease=(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()))!;Assert.True(await budget.Stop(f.Sender,Owner(f)));foreach(var sql in new[]{"UPDATE command_gateway.control_events SET units=units","DELETE FROM command_gateway.control_events","TRUNCATE command_gateway.control_events"}){await using var cmd=data.CreateCommand(sql);await Assert.ThrowsAsync<PostgresException>(async()=>await cmd.ExecuteNonQueryAsync());}Assert.Equal(1,await Scalar(data,"held",f.Sender.TenantId));
 }
 [GatewayPostgresFact]public async Task Missing_control_and_invalid_units_fail_closed()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var delivery=new LocalGatewayDelivery(data);await Assert.ThrowsAsync<ArgumentException>(()=>delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy with{ReservationUnits=0},Guid.NewGuid()));Assert.Equal(0,await Scalar(data,"held",f.Sender.TenantId));await using(var cmd=data.CreateCommand("DELETE FROM command_gateway.execution_control WHERE tenant_id=@tenant")){cmd.Parameters.AddWithValue("tenant",f.Sender.TenantId);await cmd.ExecuteNonQueryAsync();}await Assert.ThrowsAsync<InvalidOperationException>(()=>delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()));Assert.Equal(0,(await delivery.Receipt(f.Envelope.MessageId,f.Sender,f.SenderPolicy)).Attempts);
 }
 [Fact]public void Budget_store_rejects_hosted_connection(){using var data=NpgsqlDataSource.Create("Host=database.example.test;Database=gateway_test");Assert.Throws<InvalidOperationException>(()=>new LocalGatewayBudget(data));}
}
