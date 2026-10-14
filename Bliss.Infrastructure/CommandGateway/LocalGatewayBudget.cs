using Bliss.Domain.CommandGateway;
using Npgsql;
namespace Bliss.Infrastructure.CommandGateway;
public sealed record GatewayControlPolicy(bool Enabled,Guid TenantId,Guid AgentId,bool CanStop,bool CanReconcile);
// Unregistered local fixture only. No actual currency, billing or spending authorization.
public sealed class LocalGatewayBudget
{
 private readonly NpgsqlDataSource data;
 public LocalGatewayBudget(NpgsqlDataSource data){var c=new NpgsqlConnectionStringBuilder(data.ConnectionString);if(c.Host!="127.0.0.1"||c.Database!="gateway_test")throw new InvalidOperationException("Loopback gateway_test required.");this.data=data;}
 private static void Authorize(GatewayPrincipal p,GatewayControlPolicy policy){if(!policy.Enabled||!p.Authenticated||p.TenantId==Guid.Empty||p.AgentId==Guid.Empty||policy.TenantId!=p.TenantId||policy.AgentId!=p.AgentId)throw new UnauthorizedAccessException("Trusted local control grant required.");}
 internal static async Task<(bool Stopped,long Epoch,Guid Owner)> Lock(NpgsqlConnection c,NpgsqlTransaction tx,Guid tenant,CancellationToken ct)
 {
  await using var cmd=new NpgsqlCommand("SELECT stopped,epoch,owner_agent_id FROM command_gateway.execution_control WHERE tenant_id=@tenant FOR UPDATE",c,tx);cmd.Parameters.AddWithValue("tenant",tenant);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Explicit synthetic control/quota required.");return(r.GetBoolean(0),r.GetInt64(1),r.GetGuid(2));
 }
 private static async Task Event(NpgsqlConnection c,NpgsqlTransaction tx,GatewayPrincipal p,long epoch,string type,Guid? id,long? fence,long units,CancellationToken ct)
 {
  await using var cmd=new NpgsqlCommand("INSERT INTO command_gateway.control_events(id,tenant_id,actor_agent_id,epoch,event_type,message_id,fence,units) VALUES(@event,@tenant,@actor,@epoch,@type,@id,@fence,@units)",c,tx);cmd.Parameters.AddWithValue("event",Guid.NewGuid());cmd.Parameters.AddWithValue("tenant",p.TenantId);cmd.Parameters.AddWithValue("actor",p.AgentId);cmd.Parameters.AddWithValue("epoch",epoch);cmd.Parameters.AddWithValue("type",type);cmd.Parameters.AddWithValue("id",NpgsqlTypes.NpgsqlDbType.Uuid,(object?)id??DBNull.Value);cmd.Parameters.AddWithValue("fence",NpgsqlTypes.NpgsqlDbType.Bigint,(object?)fence??DBNull.Value);cmd.Parameters.AddWithValue("units",units);await cmd.ExecuteNonQueryAsync(ct);
 }
 // Caller must lock tenant control before message state; one reservation per lease attempt.
 internal static async Task Reserve(NpgsqlConnection c,NpgsqlTransaction tx,GatewayPrincipal p,Guid id,long fence,long units,long epoch,CancellationToken ct)
 {
  if(units<=0)throw new ArgumentException("Positive server-controlled synthetic reservation required.");
  await using(var quota=new NpgsqlCommand("UPDATE command_gateway.execution_control SET held=held+@units WHERE tenant_id=@tenant AND NOT stopped AND @units<=capacity-spent-held",c,tx)){quota.Parameters.AddWithValue("tenant",p.TenantId);quota.Parameters.AddWithValue("units",units);if(await quota.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Synthetic quota exhausted or stopped.");}
  await using(var reserve=new NpgsqlCommand("INSERT INTO command_gateway.budget_reservations(message_id,fence,tenant_id,actor_agent_id,units) VALUES(@id,@fence,@tenant,@actor,@units)",c,tx)){reserve.Parameters.AddWithValue("id",id);reserve.Parameters.AddWithValue("fence",fence);reserve.Parameters.AddWithValue("tenant",p.TenantId);reserve.Parameters.AddWithValue("actor",p.AgentId);reserve.Parameters.AddWithValue("units",units);await reserve.ExecuteNonQueryAsync(ct);}
  await Event(c,tx,p,epoch,"RESERVED",id,fence,units,ct);
 }
 public async Task<bool> Stop(GatewayPrincipal p,GatewayControlPolicy policy,CancellationToken ct=default)
 {
  Authorize(p,policy);if(!policy.CanStop)throw new UnauthorizedAccessException("Stop grant required.");await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);await Limits(c,tx,ct);var control=await Lock(c,tx,p.TenantId,ct);if(control.Owner!=p.AgentId)throw new UnauthorizedAccessException("Registered control owner required.");if(control.Stopped)return false;
  await using(var cmd=new NpgsqlCommand("UPDATE command_gateway.execution_control SET stopped=true,epoch=epoch+1 WHERE tenant_id=@tenant",c,tx)){cmd.Parameters.AddWithValue("tenant",p.TenantId);await cmd.ExecuteNonQueryAsync(ct);}
  await Event(c,tx,p,control.Epoch+1,"STOPPED",null,null,0,ct);await tx.CommitAsync(ct);return true;
 }
 // null means unknown: reservation stays held. Known actual requires an explicit reconciliation grant.
 // This is synthetic caller evidence, not a provider receipt. Allowed after stop to account for in-flight work.
 public async Task<bool> Reconcile(Guid id,long fence,long? actual,GatewayPrincipal p,GatewayControlPolicy policy,CancellationToken ct=default)
 {
  Authorize(p,policy);if(!policy.CanReconcile)throw new UnauthorizedAccessException("Reconciliation grant required.");if(actual<0)throw new ArgumentException("Nonnegative actual required.");await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);await Limits(c,tx,ct);var control=await Lock(c,tx,p.TenantId,ct);
  await using var query=new NpgsqlCommand("SELECT units,actual,state FROM command_gateway.budget_reservations WHERE message_id=@id AND fence=@fence AND tenant_id=@tenant AND actor_agent_id=@actor FOR UPDATE",c,tx);query.Parameters.AddWithValue("id",id);query.Parameters.AddWithValue("fence",fence);query.Parameters.AddWithValue("tenant",p.TenantId);query.Parameters.AddWithValue("actor",p.AgentId);long units;long? prior;string state;await using(var r=await query.ExecuteReaderAsync(ct)){if(!await r.ReadAsync(ct))throw new UnauthorizedAccessException("Owned reservation required.");units=r.GetInt64(0);prior=r.IsDBNull(1)?null:r.GetInt64(1);state=r.GetString(2);}
  if(actual>units)throw new InvalidOperationException("Actual exceeds synthetic reservation; manual review required.");if(state=="SETTLED"){if(actual!=prior)throw new InvalidOperationException("Changed settlement retry refused.");return false;}if(state=="UNKNOWN"&&actual is null)return false;
  if(actual.HasValue){await using var quota=new NpgsqlCommand("UPDATE command_gateway.execution_control SET held=held-@units,spent=spent+@actual WHERE tenant_id=@tenant",c,tx);quota.Parameters.AddWithValue("tenant",p.TenantId);quota.Parameters.AddWithValue("units",units);quota.Parameters.AddWithValue("actual",actual.Value);await quota.ExecuteNonQueryAsync(ct);}
  var next=actual.HasValue?"SETTLED":"UNKNOWN";await using(var update=new NpgsqlCommand("UPDATE command_gateway.budget_reservations SET state=@state,actual=@actual WHERE message_id=@id AND fence=@fence",c,tx)){update.Parameters.AddWithValue("state",next);update.Parameters.AddWithValue("actual",NpgsqlTypes.NpgsqlDbType.Bigint,(object?)actual??DBNull.Value);update.Parameters.AddWithValue("id",id);update.Parameters.AddWithValue("fence",fence);await update.ExecuteNonQueryAsync(ct);}
  await Event(c,tx,p,control.Epoch,next,id,fence,actual??units,ct);await tx.CommitAsync(ct);return true;
 }
 private static async Task Limits(NpgsqlConnection c,NpgsqlTransaction tx,CancellationToken ct){await using var cmd=new NpgsqlCommand("SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='5s'",c,tx);await cmd.ExecuteNonQueryAsync(ct);}
}
