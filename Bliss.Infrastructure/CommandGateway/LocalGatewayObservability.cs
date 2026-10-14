using System.Data;
using Bliss.Domain.CommandGateway;
using Npgsql;
namespace Bliss.Infrastructure.CommandGateway;

public sealed record GatewayObservePolicy(bool Enabled,Guid TenantId,Guid AgentId,bool CanObserve);
public sealed record GatewayMetric(string Name,long Count);
public sealed record GatewayRecentControlEvent(Guid Id,long Epoch,string Type,long Units,DateTimeOffset RecordedAt);
public sealed record GatewayOperationalSnapshot(Guid TenantId,string Unit,long Capacity,long Held,long Spent,long Available,
 bool Stopped,long Epoch,DateTimeOffset ObservedAt,IReadOnlyList<GatewayMetric> Reservations,
 IReadOnlyList<GatewayMetric> Deliveries,IReadOnlyList<GatewayMetric> Tasks,IReadOnlyList<GatewayRecentControlEvent> RecentControlEvents,
 bool ApprovalGranted=false,bool ExecutionAuthorized=false,bool ResumeAvailable=false);

// Unregistered, local, read-only fixture. No resume, approval, execution or notification authority.
public sealed class LocalGatewayObservability
{
 private readonly NpgsqlDataSource data;
 public LocalGatewayObservability(NpgsqlDataSource data){var c=new NpgsqlConnectionStringBuilder(data.ConnectionString);if(c.Host!="127.0.0.1"||c.Database!="gateway_test")throw new InvalidOperationException("Loopback gateway_test required.");this.data=data;}
 public async Task<GatewayOperationalSnapshot> Read(GatewayPrincipal p,GatewayObservePolicy policy,CancellationToken ct=default)
 {
  if(!policy.Enabled||!policy.CanObserve||!p.Authenticated||p.TenantId==Guid.Empty||p.AgentId==Guid.Empty||policy.TenantId!=p.TenantId||policy.AgentId!=p.AgentId)throw new UnauthorizedAccessException("Trusted tenant observer grant required.");
  await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(IsolationLevel.RepeatableRead,ct);
  await using(var limits=new NpgsqlCommand("SET TRANSACTION READ ONLY; SET LOCAL statement_timeout='5s'",c,tx))await limits.ExecuteNonQueryAsync(ct);
  await using(var registered=new NpgsqlCommand("SELECT count(*) FROM command_gateway.agents WHERE tenant_id=@tenant AND agent_id=@actor",c,tx)){registered.Parameters.AddWithValue("tenant",p.TenantId);registered.Parameters.AddWithValue("actor",p.AgentId);if((long)(await registered.ExecuteScalarAsync(ct))! !=1)throw new UnauthorizedAccessException("Registered tenant observer required.");}
  string unit;long capacity,held,spent,epoch;bool stopped;DateTimeOffset observed;
  await using(var cmd=new NpgsqlCommand("SELECT unit,capacity,held,spent,stopped,epoch,clock_timestamp() FROM command_gateway.execution_control WHERE tenant_id=@tenant",c,tx)){cmd.Parameters.AddWithValue("tenant",p.TenantId);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Explicit tenant control required.");unit=r.GetString(0);capacity=r.GetInt64(1);held=r.GetInt64(2);spent=r.GetInt64(3);stopped=r.GetBoolean(4);epoch=r.GetInt64(5);observed=new(r.GetDateTime(6));}
  async Task<IReadOnlyList<GatewayMetric>> Counts(string sql)
  {var values=new List<GatewayMetric>();await using var cmd=new NpgsqlCommand(sql,c,tx);cmd.Parameters.AddWithValue("tenant",p.TenantId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))values.Add(new(r.GetString(0),r.GetInt64(1)));if(values.Count>16)throw new InvalidOperationException("Bounded metric categories required.");return values.AsReadOnly();}
  var reservations=await Counts("SELECT state,count(*) FROM command_gateway.budget_reservations WHERE tenant_id=@tenant GROUP BY state ORDER BY state");
  var deliveries=await Counts("SELECT state,count(*) FROM command_gateway.delivery_state WHERE tenant_id=@tenant GROUP BY state ORDER BY state");
  var tasks=await Counts("SELECT stage,count(*) FROM command_gateway.task_runs WHERE tenant_id=@tenant GROUP BY stage ORDER BY stage");
  var events=new List<GatewayRecentControlEvent>();await using(var cmd=new NpgsqlCommand("SELECT id,epoch,event_type,units,recorded_at FROM command_gateway.control_events WHERE tenant_id=@tenant ORDER BY recorded_at DESC,id DESC LIMIT 20",c,tx)){cmd.Parameters.AddWithValue("tenant",p.TenantId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))events.Add(new(r.GetGuid(0),r.GetInt64(1),r.GetString(2),r.GetInt64(3),new(r.GetDateTime(4))));}
  await tx.CommitAsync(ct);return new(p.TenantId,unit,capacity,held,spent,capacity-held-spent,stopped,epoch,observed,reservations,deliveries,tasks,events.AsReadOnly());
 }
}
