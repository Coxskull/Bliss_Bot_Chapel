using Bliss.Domain.CommandGateway;
using Npgsql;
namespace Bliss.Infrastructure.CommandGateway;
public sealed record GatewayDeliveryPolicy(bool Enabled,Guid TenantId,Guid AgentId,IReadOnlySet<Guid> AllowedPeers,bool CanCancel);
public sealed record GatewayDeliveryLease(Guid MessageId,Guid WorkerId,long Fence,int Attempt,DateTimeOffset LeaseUntil,string ExecutionKey,string CanonicalJson);
public sealed record GatewayDeliveryReceipt(Guid MessageId,string State,int Attempts,long Fence);
public enum GatewayDeliveryOutcome { Delivered, Retry, Unknown }
// Local state machine only: no network dispatcher, tool execution or DI registration.
public sealed class LocalGatewayDelivery
{
 private readonly NpgsqlDataSource data;
 public LocalGatewayDelivery(NpgsqlDataSource data){var c=new NpgsqlConnectionStringBuilder(data.ConnectionString);if(c.Host!="127.0.0.1"||c.Database!="gateway_test")throw new InvalidOperationException("Loopback gateway_test required.");this.data=data;}
 private static void Authorize(GatewayPrincipal p,GatewayDeliveryPolicy policy){if(!policy.Enabled||!p.Authenticated||p.TenantId==Guid.Empty||p.AgentId==Guid.Empty||policy.TenantId!=p.TenantId||policy.AgentId!=p.AgentId)throw new UnauthorizedAccessException("Registered local delivery identity/policy required.");}
 private static async Task<(Guid Sender,Guid Recipient,string Json,DateTimeOffset Expiry)> Message(NpgsqlConnection c,NpgsqlTransaction? tx,Guid id,GatewayPrincipal p,GatewayDeliveryPolicy policy,bool recipientOnly,CancellationToken ct)
 {
  Authorize(p,policy);await using var cmd=new NpgsqlCommand("SELECT m.sender_agent_id,m.recipient_agent_id,m.canonical_json,(m.canonical_json::jsonb->>'expiresAt')::timestamptz FROM command_gateway.messages m JOIN command_gateway.acceptance_audit a ON a.message_id=m.message_id AND a.tenant_id=m.tenant_id AND a.actor_agent_id=m.sender_agent_id AND a.content_hash=m.content_hash JOIN command_gateway.outbox_intents o ON o.message_id=m.message_id AND o.tenant_id=m.tenant_id AND o.recipient_agent_id=m.recipient_agent_id WHERE m.message_id=@id AND m.tenant_id=@tenant AND (m.recipient_agent_id=@actor OR (NOT @only AND m.sender_agent_id=@actor))",c,tx);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("tenant",p.TenantId);cmd.Parameters.AddWithValue("actor",p.AgentId);cmd.Parameters.AddWithValue("only",recipientOnly);await using var r=await cmd.ExecuteReaderAsync(ct);
  if(!await r.ReadAsync(ct)||r.IsDBNull(3))throw new UnauthorizedAccessException("Authorized complete acceptance proof required.");var sender=r.GetGuid(0);var recipient=r.GetGuid(1);if(!policy.AllowedPeers.Contains(p.AgentId==recipient?sender:recipient))throw new UnauthorizedAccessException("Registered peer required.");return(sender,recipient,r.GetString(2),new DateTimeOffset(r.GetDateTime(3)));
 }
 private static async Task Limits(NpgsqlConnection c,NpgsqlTransaction tx,CancellationToken ct){await using var cmd=new NpgsqlCommand("SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='5s'",c,tx);await cmd.ExecuteNonQueryAsync(ct);}
 private static async Task<GatewayDeliveryReceipt> Locked(NpgsqlConnection c,NpgsqlTransaction tx,Guid id,Guid tenant,CancellationToken ct)
 {
  await using(var seed=new NpgsqlCommand("INSERT INTO command_gateway.delivery_state(message_id,tenant_id) VALUES(@id,@tenant) ON CONFLICT DO NOTHING",c,tx)){seed.Parameters.AddWithValue("id",id);seed.Parameters.AddWithValue("tenant",tenant);await seed.ExecuteNonQueryAsync(ct);}
  await using var cmd=new NpgsqlCommand("SELECT state,attempts,fence FROM command_gateway.delivery_state WHERE message_id=@id AND tenant_id=@tenant FOR UPDATE",c,tx);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("tenant",tenant);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new UnauthorizedAccessException();return new(id,r.GetString(0),r.GetInt32(1),r.GetInt64(2));
 }
 private static async Task Event(NpgsqlConnection c,NpgsqlTransaction tx,Guid id,GatewayPrincipal p,long fence,string type,CancellationToken ct){await using var cmd=new NpgsqlCommand("INSERT INTO command_gateway.delivery_events(id,message_id,tenant_id,actor_agent_id,fence,event_type) VALUES(@event,@id,@tenant,@actor,@fence,@type)",c,tx);cmd.Parameters.AddWithValue("event",Guid.NewGuid());cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("tenant",p.TenantId);cmd.Parameters.AddWithValue("actor",p.AgentId);cmd.Parameters.AddWithValue("fence",fence);cmd.Parameters.AddWithValue("type",type);await cmd.ExecuteNonQueryAsync(ct);}
 private static async Task Set(NpgsqlConnection c,NpgsqlTransaction tx,Guid id,string state,int attempts,long fence,Guid? worker,DateTimeOffset? until,CancellationToken ct){await using var cmd=new NpgsqlCommand("UPDATE command_gateway.delivery_state SET state=@state,attempts=@attempts,fence=@fence,worker_id=@worker,lease_until=@until WHERE message_id=@id",c,tx);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("state",state);cmd.Parameters.AddWithValue("attempts",attempts);cmd.Parameters.AddWithValue("fence",fence);cmd.Parameters.AddWithValue("worker",NpgsqlTypes.NpgsqlDbType.Uuid,(object?)worker??DBNull.Value);cmd.Parameters.AddWithValue("until",NpgsqlTypes.NpgsqlDbType.TimestampTz,(object?)until??DBNull.Value);await cmd.ExecuteNonQueryAsync(ct);}
 public async Task<GatewayDeliveryLease?> Claim(Guid id,GatewayPrincipal p,GatewayDeliveryPolicy policy,Guid worker,CancellationToken ct=default)
 {
  Authorize(p,policy);if(worker==Guid.Empty)throw new ArgumentException("Trusted worker instance required.");await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);await Limits(c,tx,ct);var m=await Message(c,tx,id,p,policy,true,ct);var s=await Locked(c,tx,id,p.TenantId,ct);if(s.State is not ("PENDING" or "LEASED"))return null;
  await using var time=new NpgsqlCommand("SELECT clock_timestamp(),lease_until FROM command_gateway.delivery_state WHERE message_id=@id",c,tx);time.Parameters.AddWithValue("id",id);DateTimeOffset now;DateTimeOffset? prior;await using(var r=await time.ExecuteReaderAsync(ct)){await r.ReadAsync(ct);now=new(r.GetDateTime(0));prior=r.IsDBNull(1)?null:new DateTimeOffset(r.GetDateTime(1));}
  if(m.Expiry<=now||s.Attempts>=3&&(prior is null||prior<=now)){var terminal=m.Expiry<=now?"EXPIRED":"DEAD_LETTER";await Set(c,tx,id,terminal,s.Attempts,s.Fence+1,null,null,ct);await Event(c,tx,id,p,s.Fence+1,terminal,ct);await tx.CommitAsync(ct);return null;}
  if(s.State=="LEASED"&&prior>now)return null;
  var fence=s.Fence+1;var until=now.AddSeconds(30);if(until>m.Expiry)until=m.Expiry;await Set(c,tx,id,"LEASED",s.Attempts+1,fence,worker,until,ct);await Event(c,tx,id,p,fence,"LEASED",ct);await tx.CommitAsync(ct);return new(id,worker,fence,s.Attempts+1,until,"gateway-delivery:"+id.ToString("N"),m.Json);
 }
 public async Task<bool> Finish(GatewayDeliveryLease lease,GatewayPrincipal p,GatewayDeliveryPolicy policy,GatewayDeliveryOutcome outcome,CancellationToken ct=default)
 {
  Authorize(p,policy);if(!Enum.IsDefined(outcome))throw new ArgumentException("Known outcome required.");await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);await Limits(c,tx,ct);await Message(c,tx,lease.MessageId,p,policy,true,ct);var s=await Locked(c,tx,lease.MessageId,p.TenantId,ct);
  await using var held=new NpgsqlCommand("SELECT count(*) FROM command_gateway.delivery_state WHERE message_id=@id AND state='LEASED' AND fence=@fence AND worker_id=@worker AND lease_until>clock_timestamp()",c,tx);held.Parameters.AddWithValue("id",lease.MessageId);held.Parameters.AddWithValue("fence",lease.Fence);held.Parameters.AddWithValue("worker",lease.WorkerId);if((long)(await held.ExecuteScalarAsync(ct))! !=1)return false;
  var next=outcome==GatewayDeliveryOutcome.Delivered?"DELIVERED":outcome==GatewayDeliveryOutcome.Unknown?"UNKNOWN":s.Attempts>=3?"DEAD_LETTER":"PENDING";await Set(c,tx,lease.MessageId,next,s.Attempts,s.Fence,null,null,ct);await Event(c,tx,lease.MessageId,p,s.Fence,next,ct);await tx.CommitAsync(ct);return true;
 }
 public async Task<bool> Cancel(Guid id,GatewayPrincipal p,GatewayDeliveryPolicy policy,CancellationToken ct=default)
 {
  Authorize(p,policy);if(!policy.CanCancel)throw new UnauthorizedAccessException("Cancellation grant required.");await using var c=await data.OpenConnectionAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);await Limits(c,tx,ct);await Message(c,tx,id,p,policy,false,ct);var s=await Locked(c,tx,id,p.TenantId,ct);if(s.State is "DELIVERED" or "CANCELED" or "DEAD_LETTER" or "EXPIRED")return false;await Set(c,tx,id,"CANCELED",s.Attempts,s.Fence+1,null,null,ct);await Event(c,tx,id,p,s.Fence+1,"CANCELED",ct);await tx.CommitAsync(ct);return true;
 }
 public async Task<GatewayDeliveryReceipt> Receipt(Guid id,GatewayPrincipal p,GatewayDeliveryPolicy policy,CancellationToken ct=default)
 {
  Authorize(p,policy);await using var c=await data.OpenConnectionAsync(ct);await Message(c,null,id,p,policy,false,ct);await using var cmd=new NpgsqlCommand("SELECT state,attempts,fence FROM command_gateway.delivery_state WHERE message_id=@id AND tenant_id=@tenant",c);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("tenant",p.TenantId);await using var r=await cmd.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?new(id,r.GetString(0),r.GetInt32(1),r.GetInt64(2)):new(id,"PENDING",0,0);
 }
}
