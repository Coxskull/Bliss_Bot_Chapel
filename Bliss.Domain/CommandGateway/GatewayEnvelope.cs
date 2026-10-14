using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Bliss.Domain.CommandGateway;

public enum GatewayMessageType { TaskAssignment, Ack, EvidenceSubmission, DefectReport, FixProposal, TestRequest, TestResult, ReviewVerdict, ApprovalRequest, ApprovalDecision, Cancel }
public sealed record GatewayEnvelope(string Protocol, Guid MessageId, Guid CorrelationId, Guid? CausationId, string TaskId, Guid TenantId, Guid SenderAgentId, Guid RecipientAgentId, GatewayMessageType Type, string Environment, string PolicyRef, string IdempotencyKey, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, string PayloadJson);
// These grants come from a trusted adapter, never the submitted envelope or payload.
public sealed record GatewayPrincipal(bool Authenticated, Guid TenantId, Guid AgentId);
public sealed record GatewayPolicy(bool Enabled, Guid TenantId, string PolicyRef, IReadOnlySet<Guid> Recipients, IReadOnlySet<GatewayMessageType> Types);
public sealed record ValidatedGatewayMessage(GatewayEnvelope Envelope, string CanonicalJson, string ContentHash);

public static class GatewayEnvelopeValidator
{
 private static readonly HashSet<string> ForbiddenKeys=new(StringComparer.OrdinalIgnoreCase){"permissions","credentials","secret","shell","command","toolArguments","approvalGranted","holdReleased"};
 public static ValidatedGatewayMessage Validate(GatewayEnvelope e, GatewayPrincipal principal, GatewayPolicy policy, DateTimeOffset now)
 {
  if(!policy.Enabled)throw new InvalidOperationException("Local gateway preparation is disabled.");
  if(!principal.Authenticated||principal.TenantId==Guid.Empty||principal.AgentId==Guid.Empty||principal.TenantId!=policy.TenantId||e.TenantId!=principal.TenantId||e.SenderAgentId!=principal.AgentId||!policy.Recipients.Contains(e.RecipientAgentId)||!policy.Types.Contains(e.Type))throw new UnauthorizedAccessException("Registered workload identity and message grants required.");
  if(e.Protocol!="AGM-A2A/1"||e.Environment!="LOCAL_MOCK"||e.MessageId==Guid.Empty||e.CorrelationId==Guid.Empty||e.RecipientAgentId==Guid.Empty||e.CausationId==Guid.Empty||!Enum.IsDefined(e.Type)||e.PolicyRef!=policy.PolicyRef||!Bounded(e.TaskId,80)||!Bounded(e.IdempotencyKey,120)||e.CreatedAt>now.AddSeconds(30)||e.ExpiresAt<=now||e.ExpiresAt<=e.CreatedAt||e.ExpiresAt-e.CreatedAt>TimeSpan.FromMinutes(30))throw new ArgumentException("Invalid bounded local envelope.");
  if(e.CausationId is null && e.Type is not (GatewayMessageType.TaskAssignment or GatewayMessageType.DefectReport))throw new ArgumentException("Follow-up messages require causation.");
  if(e.Type is GatewayMessageType.ApprovalDecision)throw new UnauthorizedAccessException("Owner approval is not an agent message permission.");
  if(e.PayloadJson is null||Encoding.UTF8.GetByteCount(e.PayloadJson)>16384)throw new ArgumentException("Bounded payload required.");
  using var payload=JsonDocument.Parse(e.PayloadJson,new JsonDocumentOptions{MaxDepth=8});if(payload.RootElement.ValueKind!=JsonValueKind.Object)throw new ArgumentException("Structured payload required.");
  using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream)){
   writer.WriteStartObject();writer.WriteString("protocol",e.Protocol);writer.WriteString("messageId",e.MessageId);writer.WriteString("correlationId",e.CorrelationId);if(e.CausationId is Guid cause)writer.WriteString("causationId",cause);else writer.WriteNull("causationId");writer.WriteString("taskId",e.TaskId);writer.WriteString("tenantId",e.TenantId);writer.WriteString("senderAgentId",e.SenderAgentId);writer.WriteString("recipientAgentId",e.RecipientAgentId);writer.WriteString("type",e.Type.ToString());writer.WriteString("environment",e.Environment);writer.WriteString("policyRef",e.PolicyRef);writer.WriteString("idempotencyKey",e.IdempotencyKey);writer.WriteString("createdAt",e.CreatedAt.ToUniversalTime().ToString("O"));writer.WriteString("expiresAt",e.ExpiresAt.ToUniversalTime().ToString("O"));writer.WritePropertyName("payload");Canonical(payload.RootElement,writer);writer.WriteEndObject();}
  var bytes=stream.ToArray();return new(e,Encoding.UTF8.GetString(bytes),Convert.ToHexString(SHA256.HashData(bytes)));
 }
 private static bool Bounded(string? s,int max)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=max&&!s.Any(char.IsControl);
 private static void Canonical(JsonElement value,Utf8JsonWriter writer)
 {
  switch(value.ValueKind){
   case JsonValueKind.Object:
    var properties=value.EnumerateObject().ToArray();if(properties.Length>64||properties.Select(p=>p.Name).Distinct(StringComparer.Ordinal).Count()!=properties.Length)throw new ArgumentException("Duplicate or oversized object.");writer.WriteStartObject();foreach(var p in properties.OrderBy(p=>p.Name,StringComparer.Ordinal)){if(!Bounded(p.Name,80)||ForbiddenKeys.Contains(p.Name))throw new ArgumentException("Payload cannot carry execution permissions.");writer.WritePropertyName(p.Name);Canonical(p.Value,writer);}writer.WriteEndObject();break;
   case JsonValueKind.Array:if(value.GetArrayLength()>64)throw new ArgumentException("Bounded array required.");writer.WriteStartArray();foreach(var item in value.EnumerateArray())Canonical(item,writer);writer.WriteEndArray();break;
   case JsonValueKind.String:var text=value.GetString()!;if(text.Length>4096)throw new ArgumentException("Bounded text required.");writer.WriteStringValue(text);break;
   case JsonValueKind.Number:if(!value.TryGetInt64(out var integer))throw new ArgumentException("Payload numbers must be exact integers; use strings for amounts.");writer.WriteNumberValue(integer);break;
   case JsonValueKind.True:writer.WriteBooleanValue(true);break;
   case JsonValueKind.False:writer.WriteBooleanValue(false);break;
   case JsonValueKind.Null:writer.WriteNullValue();break;
   default:throw new ArgumentException("Unsupported payload value.");
  }
 }
}
