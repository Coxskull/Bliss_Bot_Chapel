using System.Security.Cryptography;
using System.Text.Json;
namespace Bliss.Domain.CommandGateway;
public sealed record GatewaySignedRequest(GatewayEnvelope Envelope,string KeyId,DateTimeOffset IssuedAt,DateTimeOffset ExpiresAt,string Signature);
public sealed record GatewayWorkloadRegistration(string KeyId,string PublicKeyPem,GatewayPolicy Policy);
public sealed record GatewayRequestIdentity(GatewayPrincipal Principal,GatewayPolicy Policy,ValidatedGatewayMessage Message);
public sealed record GatewayReceiptBody(Guid MessageId,string ContentHash,DateTimeOffset AcceptedAt,bool Replay,Guid TenantId,Guid SenderAgentId,Guid RecipientAgentId,Guid GatewayAgentId);
public sealed record GatewaySignedReceipt(GatewayReceiptBody Body,string KeyId,DateTimeOffset IssuedAt,DateTimeOffset ExpiresAt,string Signature);
public sealed record GatewayReceiptTrust(string KeyId,string PublicKeyPem,Guid TenantId,Guid GatewayAgentId,bool Enabled);
// Local detached-signature contract, not JWT/OIDC, TLS or a hosted endpoint.
public static class GatewayTransportCodec
{
 public static byte[] RequestBytes(GatewaySignedRequest request,string contentHash,string audience)=>JsonSerializer.SerializeToUtf8Bytes(new{protocol="AGM-A2A-SIGNATURE/1",kind="REQUEST",algorithm="RS256",audience,method="POST",path="/local/agm-a2a/inbox",request.KeyId,issuedAt=request.IssuedAt.ToUniversalTime().ToString("O"),expiresAt=request.ExpiresAt.ToUniversalTime().ToString("O"),contentHash});
 public static byte[] ReceiptBytes(GatewaySignedReceipt receipt,string audience)=>JsonSerializer.SerializeToUtf8Bytes(new{protocol="AGM-A2A-SIGNATURE/1",kind="ACCEPTANCE_RECEIPT",algorithm="RS256",audience,receipt.KeyId,issuedAt=receipt.IssuedAt.ToUniversalTime().ToString("O"),expiresAt=receipt.ExpiresAt.ToUniversalTime().ToString("O"),status="ACCEPTED",receipt.Body.MessageId,receipt.Body.ContentHash,acceptedAt=receipt.Body.AcceptedAt.ToUniversalTime().ToString("O"),receipt.Body.Replay,receipt.Body.TenantId,receipt.Body.SenderAgentId,receipt.Body.RecipientAgentId,receipt.Body.GatewayAgentId});
 public static void Window(DateTimeOffset issued,DateTimeOffset expires,DateTimeOffset now){if(issued>now.AddSeconds(5)||expires<=now||expires<=issued||expires-issued>TimeSpan.FromMinutes(2))throw new UnauthorizedAccessException("Valid bounded signature window required.");}
 public static void Verify(byte[] bytes,string signature,string pem)
 {
  if(string.IsNullOrWhiteSpace(signature)||signature.Length>1024)throw new UnauthorizedAccessException("Bounded signature required.");
  try{using var rsa=RSA.Create();rsa.ImportFromPem(pem);if(rsa.KeySize<2048||rsa.KeySize>4096||!rsa.VerifyData(bytes,Convert.FromBase64String(signature),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))throw new UnauthorizedAccessException("Pinned workload signature required.");}
  catch(Exception e) when(e is CryptographicException or FormatException or ArgumentException){throw new UnauthorizedAccessException("Pinned workload signature required.",e);}
 }
 public static void Audience(string audience){if(string.IsNullOrWhiteSpace(audience)||audience.Length>120||audience.Any(char.IsControl))throw new ArgumentException("Explicit local audience required.");}
}
public sealed class GatewayRequestVerifier
{
 private readonly string audience;private readonly Func<string,GatewayWorkloadRegistration?> lookup;
 public GatewayRequestVerifier(string audience,Func<string,GatewayWorkloadRegistration?> lookup){GatewayTransportCodec.Audience(audience);this.audience=audience;this.lookup=lookup;}
 public GatewayRequestIdentity Verify(GatewaySignedRequest request,DateTimeOffset now)
 {
  if(string.IsNullOrWhiteSpace(request.KeyId)||request.KeyId.Length>80||request.KeyId.Any(char.IsControl))throw new UnauthorizedAccessException("Registered key ID required.");
  var registration=lookup(request.KeyId);if(registration is null||registration.KeyId!=request.KeyId)throw new UnauthorizedAccessException("Registered key required.");GatewayTransportCodec.Window(request.IssuedAt,request.ExpiresAt,now);if(request.ExpiresAt>request.Envelope.ExpiresAt)throw new UnauthorizedAccessException("Signature cannot outlive envelope.");
  var policy=registration.Policy;var principal=new GatewayPrincipal(true,policy.TenantId,policy.AgentId);var message=GatewayEnvelopeValidator.Validate(request.Envelope,principal,policy,now);GatewayTransportCodec.Verify(GatewayTransportCodec.RequestBytes(request,message.ContentHash,audience),request.Signature,registration.PublicKeyPem);return new(principal,policy,message);
 }
}
public static class GatewayReceiptVerifier
{
 public static GatewayReceiptBody Verify(GatewaySignedReceipt receipt,GatewayReceiptTrust trust,string audience,ValidatedGatewayMessage expected,DateTimeOffset now)
 {
  GatewayTransportCodec.Audience(audience);GatewayTransportCodec.Window(receipt.IssuedAt,receipt.ExpiresAt,now);var b=receipt.Body;var e=expected.Envelope;
  if(!trust.Enabled||receipt.KeyId!=trust.KeyId||trust.TenantId!=e.TenantId||trust.GatewayAgentId==Guid.Empty||b.GatewayAgentId!=trust.GatewayAgentId||b.MessageId!=e.MessageId||b.ContentHash!=expected.ContentHash||b.TenantId!=e.TenantId||b.SenderAgentId!=e.SenderAgentId||b.RecipientAgentId!=e.RecipientAgentId||b.AcceptedAt<e.CreatedAt.AddSeconds(-30)||b.AcceptedAt>=e.ExpiresAt||b.AcceptedAt>receipt.IssuedAt.AddSeconds(5))throw new UnauthorizedAccessException("Receipt must bind trusted gateway and exact submission.");
  GatewayTransportCodec.Verify(GatewayTransportCodec.ReceiptBytes(receipt,audience),receipt.Signature,trust.PublicKeyPem);return b;
 }
}
