using Bliss.Domain.CommandGateway;
using Bliss.Infrastructure.CommandGateway;
using Npgsql;
namespace Bliss.Tests.CommandGateway;
[Collection("Gateway inbox isolation")]
public sealed class GatewayObservabilityTests
{
 private static NpgsqlDataSource Data()=>NpgsqlDataSource.Create(Environment.GetEnvironmentVariable("BLISS_GATEWAY_TEST_POSTGRES")!);
 private static GatewayObservePolicy Policy(GatewayPrincipal p,bool allowed=true)=>new(true,p.TenantId,p.AgentId,allowed);
 [GatewayPostgresFact]public async Task Snapshot_is_tenant_scoped_read_only_bounded_and_reports_synthetic_control()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var other=await GatewayDeliveryTests.Prepare(data);var delivery=new LocalGatewayDelivery(data);var lease=(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()))!;
  var reader=new LocalGatewayObservability(data);var snapshot=await reader.Read(f.Recipient,Policy(f.Recipient));
  Assert.Equal(f.Recipient.TenantId,snapshot.TenantId);Assert.Equal("SYNTHETIC_TEST_UNIT",snapshot.Unit);Assert.Equal(100,snapshot.Capacity);Assert.Equal(1,snapshot.Held);Assert.Equal(0,snapshot.Spent);Assert.Equal(99,snapshot.Available);Assert.False(snapshot.Stopped);
  Assert.Contains(snapshot.Reservations,x=>x==new GatewayMetric("RESERVED",1));Assert.Contains(snapshot.Deliveries,x=>x==new GatewayMetric("LEASED",1));Assert.Empty(snapshot.Tasks);Assert.Single(snapshot.RecentControlEvents);Assert.Equal("RESERVED",snapshot.RecentControlEvents[0].Type);
  Assert.False(snapshot.ApprovalGranted);Assert.False(snapshot.ExecutionAuthorized);Assert.False(snapshot.ResumeAvailable);
  var foreign=await reader.Read(other.Sender,Policy(other.Sender));Assert.Empty(foreign.Reservations);Assert.Empty(foreign.Deliveries);Assert.Empty(foreign.RecentControlEvents);
  Assert.Equal("LEASED",(await delivery.Receipt(lease.MessageId,f.Sender,f.SenderPolicy)).State);
 }
 [GatewayPostgresFact]public async Task Observer_scope_registration_and_control_are_required_without_mutation()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var reader=new LocalGatewayObservability(data);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read(f.Sender with{Authenticated=false},Policy(f.Sender)));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read(f.Sender,Policy(f.Sender,false)));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read(f.Sender,Policy(f.Sender) with{TenantId=Guid.NewGuid()}));
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>reader.Read(f.Sender with{AgentId=Guid.NewGuid()},Policy(f.Sender) with{AgentId=Guid.NewGuid()}));
  await using(var delete=data.CreateCommand("DELETE FROM command_gateway.execution_control WHERE tenant_id=@tenant")){delete.Parameters.AddWithValue("tenant",f.Sender.TenantId);await delete.ExecuteNonQueryAsync();}
  await Assert.ThrowsAsync<InvalidOperationException>(()=>reader.Read(f.Sender,Policy(f.Sender)));
 }
 [GatewayPostgresFact]public async Task Stop_and_reconciliation_are_visible_but_observer_cannot_resume_or_authorize()
 {
  await using var data=Data();var f=await GatewayDeliveryTests.Prepare(data);var delivery=new LocalGatewayDelivery(data);var budget=new LocalGatewayBudget(data);var lease=(await delivery.Claim(f.Envelope.MessageId,f.Recipient,f.RecipientPolicy,Guid.NewGuid()))!;Assert.True(await delivery.Finish(lease,f.Recipient,f.RecipientPolicy,GatewayDeliveryOutcome.Delivered));
  Assert.True(await budget.Reconcile(lease.MessageId,lease.Fence,1,f.Recipient,new(true,f.Recipient.TenantId,f.Recipient.AgentId,false,true)));Assert.True(await budget.Stop(f.Sender,new(true,f.Sender.TenantId,f.Sender.AgentId,true,false)));
  var snapshot=await new LocalGatewayObservability(data).Read(f.Sender,Policy(f.Sender));Assert.True(snapshot.Stopped);Assert.Equal(1,snapshot.Epoch);Assert.Equal(0,snapshot.Held);Assert.Equal(1,snapshot.Spent);Assert.Equal(99,snapshot.Available);
  Assert.Contains(snapshot.Reservations,x=>x==new GatewayMetric("SETTLED",1));Assert.Contains(snapshot.Deliveries,x=>x==new GatewayMetric("DELIVERED",1));Assert.Equal(new[]{"STOPPED","SETTLED","RESERVED"},snapshot.RecentControlEvents.Select(x=>x.Type).ToArray());Assert.False(snapshot.ResumeAvailable);Assert.False(snapshot.ApprovalGranted);Assert.False(snapshot.ExecutionAuthorized);
 }
 [Fact]public void Observability_rejects_hosted_or_wrong_database(){using var hosted=NpgsqlDataSource.Create("Host=database.example.test;Database=gateway_test");Assert.Throws<InvalidOperationException>(()=>new LocalGatewayObservability(hosted));using var wrong=NpgsqlDataSource.Create("Host=127.0.0.1;Database=postgres");Assert.Throws<InvalidOperationException>(()=>new LocalGatewayObservability(wrong));}
}
